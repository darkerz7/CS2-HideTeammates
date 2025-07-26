# Hide Teammates for CounterStrikeSharp
Hides Teammates on the entire map or distance

## Required packages:
1. [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/)
2. [PlayerSettingsCS2](https://github.com/NickFox007/PlayerSettingsCS2) (0.9.3)

## Installation:
1. Install `PlayerSettingsCS2`
2. Compile or copy CS2-HideTeammates to `counterstrikesharp/plugins/CS2-HideTeammates` folger
3. Restart server

## CVARs:
Cvar | Parameter | Description
--- | --- | ---
`css_ht_enabled` | <0/1> | Enable/Disable plugin
`css_ht_maximum` | <1000-8000> | The maximum distance a player can choose
`css_ht_hidecomm` | <0/1> | Enable/Disable use of hide word for commands
`css_ht_hideia` | <0/1> | Enable/Disable ignoring player attachments (ex. prop leader glow)

## Commands:
Client Command | Description
--- | ---
`css_ht/css_hide [<-1-CVAR_MAX_Distance>]` | (-1 - Disable, 0 - Enable on the entire map, 1-CVAR_MAX_Distance - Enable ont the Distance)
`css_ht/css_hide` | Toggle hide teammates on the entire map. Maybe replaced by menu later
`css_htall/css_hideall` | Toggle hide teammates on the entire map
