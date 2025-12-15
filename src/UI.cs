using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using System.Globalization;
using Microsoft.Extensions.Localization;

namespace CS2_HideTeammates
{
	internal class UI
	{
#nullable enable
		public static IStringLocalizer? Strlocalizer;
#nullable disable
		public static void CvarChangeNotify(string sCvarName, string sCvarValue, bool bClientNotify)
		{
			if (Strlocalizer == null) return;
			using (new WithTemporaryCulture(CultureInfo.GetCultureInfo(CoreConfig.ServerLanguage)))
			{
				PrintToConsole(Strlocalizer["Cvar.Notify", sCvarName, sCvarValue], 3);
			}

			if (bClientNotify)
			{
				Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(pl =>
				{
					ReplyToCommand(pl, false, "Cvar.Notify", sCvarName, sCvarValue);
				});
			}
		}
		public static void ReplyToCommand(CCSPlayerController player, bool bConsole, string sMessage, params object[] arg)
		{
			if (Strlocalizer == null) return;
			Server.NextWorldUpdate(() =>
			{
				if (player is { IsValid: true, IsBot: false, IsHLTV: false })
				{
					using (new WithTemporaryCulture(player.GetLanguage()))
					{
						if (!bConsole) player.PrintToChat($" \x0B[\x04HT\x0B]\x01 {ReplaceColorTags(Strlocalizer[sMessage, arg])}");
						else player.PrintToConsole($"[HT] {ReplaceColorTags(Strlocalizer[sMessage, arg], false)}");
					}
				}
			});
		}
		public static void PrintToConsole(string sMessage, int iColor = 1)
		{
			Console.ForegroundColor = (ConsoleColor)8;
			Console.Write("[");
			Console.ForegroundColor = (ConsoleColor)6;
			Console.Write("HT");
			Console.ForegroundColor = (ConsoleColor)8;
			Console.Write("] ");
			Console.ForegroundColor = (ConsoleColor)iColor;
			Console.WriteLine(sMessage, false);
			Console.ResetColor();
			/* Colors:
				* 0 - No color		1 - White		2 - Red-Orange		3 - Orange
				* 4 - Yellow		5 - Dark Green	6 - Green			7 - Light Green
				* 8 - Cyan			9 - Sky			10 - Light Blue		11 - Blue
				* 12 - Violet		13 - Pink		14 - Light Red		15 - Red */
		}
		public static string ReplaceColorTags(string input, bool bChat = true)
		{
			for (var i = 0; i < colorPatterns.Length; i++)
				input = input.Replace(colorPatterns[i], bChat ? colorReplacements[i] : "");

			return input;
		}
		static readonly string[] colorPatterns =
		[
			"{default}", "{darkred}", "{purple}", "{green}", "{lightgreen}", "{lime}", "{red}", "{grey}",
			"{olive}", "{a}", "{lightblue}", "{blue}", "{d}", "{pink}", "{darkorange}", "{orange}",
			"{white}", "{yellow}", "{magenta}", "{silver}", "{bluegrey}", "{lightred}", "{cyan}", "{gray}"
		];
		static readonly string[] colorReplacements =
		[
			"\x01", "\x02", "\x03", "\x04", "\x05", "\x06", "\x07", "\x08",
			"\x09", "\x0A", "\x0B", "\x0C", "\x0D", "\x0E", "\x0F", "\x10",
			"\x01", "\x09", "\x0E", "\x0A", "\x0D", "\x0F", "\x03", "\x08"
		];
	}
}
