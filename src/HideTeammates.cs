using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Cvars.Validators;
using CounterStrikeSharp.API.Modules.Timers;
using PlayerSettings;
using static CounterStrikeSharp.API.Core.Listeners;

namespace CS2_HideTeammates
{
	[MinimumApiVersion(330)]
	public class HideTeammates : BasePlugin
	{
		readonly float TIMERTIME = 0.3f;
#nullable enable
		private ISettingsApi? _PlayerSettingsAPI;
		private readonly PluginCapability<ISettingsApi?> _PlayerSettingsAPICapability = new("settings:nfcore");
#nullable disable
		bool g_bEnable = true;
		int g_iMaxDistance = 8000;
		bool g_bHideComm = false;
		bool g_bHideIgnoreAttachments = false;
		bool[] g_bHide = new bool[65];
		int[] g_iDistance = new int[65];
		bool[] g_bRMB = new bool[65];
        bool[] g_bHideCrash = new bool[65];
        List<CCSPlayerController>[] g_Target = new List<CCSPlayerController>[65];
		CounterStrikeSharp.API.Modules.Timers.Timer g_Timer;

		public FakeConVar<bool> Cvar_Enable = new("css_ht_enabled", "Disabled/enabled [0/1]", true, flags: ConVarFlags.FCVAR_NOTIFY, new RangeValidator<bool>(false, true));
		public FakeConVar<int> Cvar_MaxDistance = new("css_ht_maximum", "The maximum distance a player can choose [1000-8000]", 8000, flags: ConVarFlags.FCVAR_NOTIFY, new RangeValidator<int>(1000, 8000));
		public FakeConVar<bool> Cvar_HideComm = new("css_ht_hidecomm", "Disabled/enabled use of hide word for commands [0/1]", false, flags: ConVarFlags.FCVAR_NOTIFY, new RangeValidator<bool>(false, true));
		public FakeConVar<bool> Cvar_HideIgnoreAttachments = new("css_ht_hideia", "Disabled/enabled ignoring player attachments (ex. prop leader glow) [0/1]", false, flags: ConVarFlags.FCVAR_NOTIFY, new RangeValidator<bool>(false, true));
		public override string ModuleName => "Hide Teammates";
		public override string ModuleDescription => "A plugin that can !hide with individual distances";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.9";
		public override void OnAllPluginsLoaded(bool hotReload)
		{
			_PlayerSettingsAPI = _PlayerSettingsAPICapability.Get();
			if (_PlayerSettingsAPI == null)
				UI.PrintToConsole("PlayerSettings core not found...");

			if (hotReload)
			{
				Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
				{
					GetValue(player);
				});
			}
		}
		public override void Load(bool hotReload)
		{
			for (int i = 0; i < 65; i++) g_Target[i] = [];
			UI.Strlocalizer = Localizer;

			g_bEnable = Cvar_Enable.Value;
			Cvar_Enable.ValueChanged += (sender, value) =>
			{
				g_bEnable = value;
				UI.CvarChangeNotify(Cvar_Enable.Name, value.ToString(), Cvar_Enable.Flags.HasFlag(ConVarFlags.FCVAR_NOTIFY));
			};

			g_iMaxDistance = Cvar_MaxDistance.Value;
			Cvar_MaxDistance.ValueChanged += (sender, value) =>
			{
				if (value >= 1000 && value <= 8000) g_iMaxDistance = value;
				else g_iMaxDistance = 8000;
				UI.CvarChangeNotify(Cvar_MaxDistance.Name, value.ToString(), Cvar_MaxDistance.Flags.HasFlag(ConVarFlags.FCVAR_NOTIFY));
			};

			g_bHideComm = Cvar_HideComm.Value;
			Cvar_HideComm.ValueChanged += (sender, value) =>
			{
				g_bHideComm = value;
				UI.CvarChangeNotify(Cvar_HideComm.Name, value.ToString(), Cvar_HideComm.Flags.HasFlag(ConVarFlags.FCVAR_NOTIFY));
			};

			g_bHideIgnoreAttachments = Cvar_HideComm.Value;
			Cvar_HideIgnoreAttachments.ValueChanged += (sender, value) =>
			{
				g_bHideIgnoreAttachments = value;
				UI.CvarChangeNotify(Cvar_HideIgnoreAttachments.Name, value.ToString(), Cvar_HideIgnoreAttachments.Flags.HasFlag(ConVarFlags.FCVAR_NOTIFY));
			};

			RegisterFakeConVars(typeof(ConVar));

			RegisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
			RegisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
			RegisterListener<OnMapStart>(OnMapStart_Listener);
			RegisterListener<OnMapEnd>(OnMapEnd_Listener);
			RegisterListener<CheckTransmit>(OnTransmit);
			RegisterListener<OnPlayerButtonsChanged>(OnOnPlayerButtonsChanged_Listener);

			CreateTimer();
		}

		public override void Unload(bool hotReload)
		{
			DeregisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
			DeregisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect);
			RemoveListener<OnMapStart>(OnMapStart_Listener);
			RemoveListener<OnMapEnd>(OnMapEnd_Listener);
			RemoveListener<CheckTransmit>(OnTransmit);
			RemoveListener<OnPlayerButtonsChanged>(OnOnPlayerButtonsChanged_Listener);

			CloseTimer();
		}

		private void OnOnPlayerButtonsChanged_Listener(CCSPlayerController player, PlayerButtons pressed, PlayerButtons released)
		{
			if (player != null && player.IsValid)
			{
				g_bRMB[player.Slot] = (player.Buttons & PlayerButtons.Attack2) != 0;
			}
		}
		HookResult OnEventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
#nullable enable
			CCSPlayerController? player = @event.Userid;
#nullable disable
			if (player != null && player.IsValid)
			{
				g_bHide[player.Slot] = false;
				g_iDistance[player.Slot] = 0;
				g_bHideCrash[player.Slot] = false;
            }
			return HookResult.Continue;
		}
		HookResult OnEventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
		{
			GetValue(@event.Userid);
			return HookResult.Continue;
		}
		void OnMapStart_Listener(string sMapName)
		{
			CreateTimer();
		}

		void OnMapEnd_Listener()
		{
			CloseTimer();
		}

		void OnTransmit(CCheckTransmitInfoList infoList)
		{
			if (!g_bEnable) return;
#nullable enable
			foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
#nullable disable
			{
				if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || !player.Pawn.IsValid || player.Pawn.Value == null) continue;

				foreach (CCSPlayerController targetPlayer in g_Target[player.Slot].ToList())
				{
					//Console.WriteLine($"Child: {targetPlayer.Pawn.Value.CBodyComponent.SceneNode.Child.Owner.DesignerName} Index: {targetPlayer.Pawn.Value.CBodyComponent.SceneNode.Child.Owner.Index}");
					//targetPlayer.Pawn.Value.CBodyComponent.SceneNode.Child.Child == null - is there any model attached to the player?
					//if (targetPlayer.IsValid && targetPlayer.Pawn.IsValid && targetPlayer.Pawn.Value != null && targetPlayer.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE && (g_bHideIgnoreAttachments || targetPlayer.Pawn.Value.CBodyComponent.SceneNode.Child.Child == null))
					if (targetPlayer.IsValid && targetPlayer.Pawn.IsValid && targetPlayer.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE && (g_bHideIgnoreAttachments || !targetPlayer.Pawn.Value.CBodyComponent.SceneNode.Child.Owner.DesignerName.Equals("prop_dynamic") && !targetPlayer.Pawn.Value.CBodyComponent.SceneNode.Child.NextSibling.Owner.DesignerName.Equals("prop_dynamic")))
						info.TransmitEntities.Remove(targetPlayer.Pawn.Value);
				}
			}
		}

		void OnTimer()
		{
			if (!g_bEnable) return;
			Utilities.GetPlayers().Where(p => p.IsValid && p.Pawn.IsValid).ToList().ForEach(player =>
			{
				g_Target[player.Slot].Clear();
				if (g_bHideCrash[player.Slot] && g_bHide[player.Slot] && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
				{
					Utilities.GetPlayers().Where(target => target != null && target.IsValid && target.Pawn.IsValid && !g_bRMB[player.Slot] && target.Slot != player.Slot && target.Team == player.Team && target.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE).ToList().ForEach(targetplayer =>
					{
						if (g_iDistance[player.Slot] == 0) g_Target[player.Slot].Add(targetplayer);
						else
						{
							if (Distance((System.Numerics.Vector3)targetplayer.Pawn.Value?.AbsOrigin, (System.Numerics.Vector3)player.Pawn.Value?.AbsOrigin) <= g_iDistance[player.Slot])
							{
								g_Target[player.Slot].Add(targetplayer);
							}
						}
					});
				}
			});
		}
#nullable enable
        [ConsoleCommand("css_htcrash", "Agree to use hide at your own risk")]
        [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnCommandHideCrash(CCSPlayerController? player, CommandInfo command)
#nullable disable
        {
            if (player == null || !player.IsValid) return;
            bool bConsole = command.CallingContext == CommandCallingContext.Console;
            if (!g_bEnable)
            {
                UI.ReplyToCommand(player, bConsole, "Reply.PluginDisabled");
                return;
            }
            g_bHideCrash[player.Slot] = !g_bHideCrash[player.Slot];
            SetValue(player);
            if (g_bHideCrash[player.Slot])
            {
                UI.ReplyToCommand(player, bConsole, "Reply.CrashEnable");
            }
            else
            {
                UI.ReplyToCommand(player, bConsole, "Reply.CrashDisable");
            }
        }
#nullable enable
        [ConsoleCommand("css_hidecrash", "Agree to use hide at your own risk")]
        [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnCommandHideCrashWord(CCSPlayerController? player, CommandInfo command)
#nullable disable
        {
            if (g_bHideComm) OnCommandHideCrash(player, command);
        }
#nullable enable
        [ConsoleCommand("css_ht", "Allows to hide players and choose the distance")]
		[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandHide(CCSPlayerController? player, CommandInfo command)
#nullable disable
		{
			if (player == null || !player.IsValid) return;
			bool bConsole = command.CallingContext == CommandCallingContext.Console;
			if (!g_bEnable)
			{
				UI.ReplyToCommand(player, bConsole, "Reply.PluginDisabled");
				return;
			}
            if (!g_bHideCrash[player.Slot])
            {
                UI.ReplyToCommand(player, bConsole, "Reply.HideCrash", g_bHideComm ? " (!hidecrash)" : "");
                return;
            }
            if (!Int32.TryParse(command.GetArg(1), out int customdistance)) customdistance = -2;
			if (customdistance >= 0 && customdistance <= g_iMaxDistance)
			{
				g_bHide[player.Slot] = true;
				g_iDistance[player.Slot] = customdistance;
				SetValue(player);
				if (g_iDistance[player.Slot] == 0) UI.ReplyToCommand(player, bConsole, "Reply.EnableAllMap");
				else UI.ReplyToCommand(player, bConsole, "Reply.Enable", g_iDistance[player.Slot]);
			} else if (customdistance < -2 || customdistance > g_iMaxDistance)
			{
				UI.ReplyToCommand(player, bConsole, "Reply.Wrong", g_iMaxDistance);
			} else if (customdistance == -1)
			{
				g_bHide[player.Slot] = false;
				SetValue(player);
				UI.ReplyToCommand(player, bConsole, "Reply.Disable");
			} else if (customdistance == -2) //Later can be replaced by a menu
			{
				g_bHide[player.Slot] = !g_bHide[player.Slot];
				SetValue(player);
				if (g_bHide[player.Slot])
				{
					if (g_iDistance[player.Slot] == 0) UI.ReplyToCommand(player, bConsole, "Reply.EnableAllMap");
					else UI.ReplyToCommand(player, bConsole, "Reply.Enable", g_iDistance[player.Slot]);
				} else
				{
					UI.ReplyToCommand(player, bConsole, "Reply.Disable");
				}
			}
		}
#nullable enable
		[ConsoleCommand("css_hide", "Allows to hide players and choose the distance")]
		[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandHideWord(CCSPlayerController? player, CommandInfo command)
#nullable disable
		{
			if (g_bHideComm) OnCommandHide(player, command);
		}
#nullable enable
		[ConsoleCommand("css_htall", "Allows to hide players and choose the distance")]
		[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandHideAll(CCSPlayerController? player, CommandInfo command)
#nullable disable
		{
			if (player == null || !player.IsValid) return;
			bool bConsole = command.CallingContext == CommandCallingContext.Console;
			if (!g_bEnable)
			{
				UI.ReplyToCommand(player, bConsole, "Reply.PluginDisabled");
				return;
			}
            if (!g_bHideCrash[player.Slot])
            {
                UI.ReplyToCommand(player, bConsole, "Reply.HideCrash", g_bHideComm ? " (!hidecrash)" : "");
                return;
            }

            g_bHide[player.Slot] = !g_bHide[player.Slot];
			SetValue(player);
			if (g_bHide[player.Slot])
			{
				if (g_iDistance[player.Slot] == 0) UI.ReplyToCommand(player, bConsole, "Reply.EnableAllMap");
				else UI.ReplyToCommand(player, bConsole, "Reply.Enable", g_iDistance[player.Slot]);
			}
			else
			{
				UI.ReplyToCommand(player, bConsole, "Reply.Disable");
			}
		}
#nullable enable
		[ConsoleCommand("css_hideall", "Allows to hide players and choose the distance")]
		[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandHideAllWord(CCSPlayerController? player, CommandInfo command)
#nullable disable
		{
			if (g_bHideComm) OnCommandHideAll(player, command);
		}
#nullable enable
		void GetValue(CCSPlayerController? player)
#nullable disable
		{
			if (player == null || !player.IsValid) return;
			if (_PlayerSettingsAPI != null)
			{
                string sHideCrash = _PlayerSettingsAPI.GetPlayerSettingsValue(player, "HT_Crash", "0");
                if (string.IsNullOrEmpty(sHideCrash) || !Int32.TryParse(sHideCrash, out int iHideCrash)) iHideCrash = 0;
                if (iHideCrash == 0) g_bHideCrash[player.Slot] = false;
                else g_bHideCrash[player.Slot] = true;

                string sHide = _PlayerSettingsAPI.GetPlayerSettingsValue(player, "HT_Hide", "0");
				if (string.IsNullOrEmpty(sHide) || !Int32.TryParse(sHide, out int iHide)) iHide = 0;
				if (iHide == 0) g_bHide[player.Slot] = false;
				else g_bHide[player.Slot] = true;

				string sDistance = _PlayerSettingsAPI.GetPlayerSettingsValue(player, "HT_Distance", "0");
				if (string.IsNullOrEmpty(sDistance) || !Int32.TryParse(sDistance, out int iDistance)) iDistance = 0;
				if (iDistance <= 0) iDistance = 0;
				else if (iDistance >= g_iMaxDistance) iDistance = g_iMaxDistance;
				g_iDistance[player.Slot] = iDistance;
			}
		}
#nullable enable
		void SetValue(CCSPlayerController? player)
#nullable disable
		{
			if (player == null || !player.IsValid) return;
			if (_PlayerSettingsAPI != null)
			{
                if (g_bHideCrash[player.Slot]) _PlayerSettingsAPI.SetPlayerSettingsValue(player, "HT_Crash", "1");
                else _PlayerSettingsAPI.SetPlayerSettingsValue(player, "HT_Crash", "0");

                if (g_bHide[player.Slot]) _PlayerSettingsAPI.SetPlayerSettingsValue(player, "HT_Hide", "1");
				else _PlayerSettingsAPI.SetPlayerSettingsValue(player, "HT_Hide", "0");

				_PlayerSettingsAPI.SetPlayerSettingsValue(player, "HT_Distance", g_iDistance[player.Slot].ToString());
			}
		}

		void CreateTimer()
		{
			CloseTimer();
			g_Timer = new CounterStrikeSharp.API.Modules.Timers.Timer(TIMERTIME, OnTimer, TimerFlags.REPEAT);
		}

		void CloseTimer()
		{
			if (g_Timer != null)
			{
				g_Timer.Kill();
				g_Timer = null;
			}
		}

		static float Distance(System.Numerics.Vector3 point1, System.Numerics.Vector3 point2)
		{
			float dx = point2.X - point1.X;
			float dy = point2.Y - point1.Y;
			float dz = point2.Z - point1.Z;

			return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
		}
	}
}
