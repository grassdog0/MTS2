# ModTheSpire2

ModTheSpire2 is a Windows pre-launch mod selector and in-game companion manager for Slay the Spire 2.

Current version: `0.4.0`.

## What This Mod Does

- Adds a `ModTheSpire2 Launcher` button under the game's mod settings screen when this mod is enabled.
- Provides a native Windows launcher: `ModTheSpire2Launcher.exe`.
- Lets players choose vanilla launch or selected mods before starting the game.
- Groups dependent mods under their required mods in the launcher.
- Blocks selecting a dependent mod until its required mod is selected.
- Detects both standard mod JSON files and Workshop mods that only provide `mod_manifest.json`.
- Lets players move mods up/down, set a numeric order, save a custom load order, and reset that order.
- Saves enabled mod selections so a one-time vanilla launch does not erase the player's normal mod setup.
- Supports named launcher order profiles.
- The in-game ModTheSpire2 management overlay can also save a launcher load order for enabled mods.
- Saves ModTheSpire2 state in `ModTheSpire2Data` inside this mod folder.
- Backs up `settings.save` before writing mod enablement or load-order changes.

## Important Safety Notes

- `Vanilla` in the launcher is a one-time vanilla start. It does not clear the saved ModTheSpire2 enabled-mod list.
- `Launch Selected` saves the currently checked mods and then starts the game with those mods enabled.
- `Save Order` saves both load order and enabled mod selection.
- `Close and Open Launcher` first closes the current game, then opens the launcher. This avoids running two Slay the Spire 2 instances at once.
- The standalone `Open Launcher` action was intentionally removed from the in-game overlay because launching another game instance while the current one is still running can risk settings/save conflicts.

## Hot Apply

The in-game ModTheSpire2 management window separates mods into:

- `Hot-Apply candidates`: conservative setting/config entries that can be saved during the current session.
- `Restart Required`: DLL/PCK/gameplay/unknown mods that still need a restart through the launcher.

Hot apply does not true-load a previously unloaded DLL or PCK. It only saves safe settings state. If a hot-apply attempt fails or takes too long, ModTheSpire2 rolls back the settings state and keeps the game running when possible.

ModTheSpire2 itself is restart-required. It changes the game's UI through a DLL/Harmony patch, so disabling it cannot unload the already-loaded code during the current session.

## Load Order

The launcher can move mods up/down, apply a numeric order, and save a custom load order. The in-game management overlay can also save the same order file:

```text
ModTheSpire2Data/load-order.txt
```

This file stores one mod id per line. Dependency rules are enforced so required mods stay before mods that depend on them. The saved order is applied the next time the game is launched through ModTheSpire2.

Enabled mod selections are stored separately:

```text
ModTheSpire2Data/enabled-mods.txt
```

This file stores one enabled mod id per line. It is updated by `Save Order` and `Launch Selected`, but not by `Vanilla`.

Named order profiles are stored under:

```text
ModTheSpire2Data/order-profiles/
```

## Steam Launch Options

Steam Workshop cannot automatically edit a player's launch options. To make the Steam Play button open ModTheSpire2 before the game:

1. In Steam, right-click `Slay the Spire 2`.
2. Choose `Properties`.
3. Open the `General` page.
4. Find `Launch Options`.
5. Paste this line, adjusting the path to your own install location:

```text
"E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2\ModTheSpire2Launcher.exe" -- %command%
```

If this mod is installed from Workshop, the launcher may be under:

```text
E:\SteamLibrary\steamapps\workshop\content\2868840\<workshop item id>\ModTheSpire2Launcher.exe
```

Use the actual path to `ModTheSpire2Launcher.exe` on your computer.

The in-game `Close and Open Launcher` confirmation dialog includes a copy button for the launch option.

## Source Code

GitHub:

```text
https://github.com/grassdog0/MTS2
```

## No Extra Runtime

The launcher is a native Windows program. It does not require players to install .NET.

## File Safety

The install folder intentionally contains only one JSON file: `ModTheSpire2.json`. Extra runtime JSON files can be mistaken for broken mods by the game.

## Included Files

```text
ModTheSpire2.dll
ModTheSpire2.json
ModTheSpire2.pck
ModTheSpire2Launcher.exe
README.md
```

Do not upload runtime folders such as `ModTheSpire2Data`.
