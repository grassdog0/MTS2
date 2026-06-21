# ModTheSpire2

ModTheSpire2 is a Windows pre-launch mod selector and in-game companion manager for Slay the Spire 2.

Current version: `0.4.0`.

## What This Mod Does

- Adds a `ModTheSpire2 Launcher` button under the game's mod settings screen when this mod is enabled.
- Provides a native Windows launcher: `ModTheSpire2Launcher.exe`.
- Lets players choose vanilla launch or selected mods before starting the game.
- Groups dependent mods under their required mods in the launcher.
- Blocks selecting a dependent mod until its required mod is selected.
- Detects standard mod JSON files, Workshop `mod_manifest.json` layouts, legacy `pck_name` manifests, and explicit-id `.manifest` sidecar mod manifests without creating duplicate entries.
- Resolves dependencies by mod id, unique display name, and local Workshop numeric folder id.
- Lets players move mods up/down, set a numeric order, save a custom load order, and reset that order.
- Saves enabled mod selections so a one-time vanilla launch does not erase the player's normal mod setup.
- Supports named launcher order profiles.
- The in-game ModTheSpire2 management overlay shows the current mod state and can close the game, then reopen the launcher for mod changes.
- Saves ModTheSpire2 state in `ModTheSpire2Data` inside this mod folder.
- Backs up `settings.save` before writing mod enablement or load-order changes.
- If multiple Steam user `settings.save` files exist, the launcher uses the newest discovered file under the game's normal roaming settings folder instead of a hardcoded Steam account path.

## Important Safety Notes

- `Vanilla` in the launcher is a one-time vanilla start. It does not clear the saved ModTheSpire2 enabled-mod list.
- `Launch Selected` saves the currently checked mods and then starts the game with those mods enabled.
- `Save Order` saves both load order and enabled mod selection.
- `Close and Open Launcher` first closes the current game, then opens the launcher. This avoids running two Slay the Spire 2 instances at once.
- The standalone `Open Launcher` action was intentionally removed from the in-game overlay because launching another game instance while the current one is still running can risk settings/save conflicts.

## Restart-Based Mod Changes

ModTheSpire2 manages whole-mod enablement before the game starts. This is intentional.

Many Slay the Spire 2 mods load DLLs, PCK files, UI patches, save hooks, or gameplay content during startup. Once that code or content is loaded, ModTheSpire2 does not claim it can safely unload the entire mod inside the current game process.

To change enabled mods:

1. Open Settings / General / Mod Settings.
2. Click `ModTheSpire2 Launcher`.
3. Click `Close and Open Launcher`.
4. Confirm the restart dialog.
5. Select Vanilla or the desired mod profile in the launcher.
6. Start the game again.

The in-game overlay still shows useful status:

- mods enabled for the current launch
- mods loaded in the current session
- disabled mods detected in local or Workshop folders
- load-order information that will be used by the launcher

ModTheSpire2 itself is restart-required. It changes the game's UI through a DLL/Harmony patch, so disabling it cannot unload the already-loaded code during the current session.

If another mod exposes its own runtime-safe configuration through BaseLib or a similar framework, that setting belongs to that mod. ModTheSpire2 does not replace BaseLib and does not make BaseLib a hard dependency.

## Dependency Compatibility

ModTheSpire2 reads dependency declarations from:

```text
dependencies
requires
required_mods
requiredMods
```

Dependency objects may use:

```text
id
mod_id
modId
workshop_id
workshopId
steam_id
steamId
published_file_id
publishedFileId
```

Dependency fields may be arrays, a single string, or a single dependency object.

Dependency objects marked as optional are not treated as hard requirements. ModTheSpire2 currently recognizes `optional: true`, `is_optional: true`, `isOptional: true`, and `required: false` on dependency objects. Optional relationships do not block launcher selection and do not appear as missing dependencies.

Order-only fields `load_after`, `loadAfter`, `load_before`, and `loadBefore` are treated as load-order hints, not hard dependencies. They can move a mod after or before another installed mod during order repair, but they do not block selection and do not appear as missing dependencies.

Resolution is conservative:

- Exact mod id matches win.
- A dependency that uniquely matches a discovered mod's display `name` is resolved to that mod id.
- A dependency that uniquely matches a discovered local Workshop numeric folder id is resolved to that mod id.
- Ambiguous display names or unknown dependencies remain unresolved and are shown as missing.
- Missing dependencies block unsafe selection in the launcher.

Runtime data folders named `ModTheSpire2Data` are ignored during mod scanning. Logs, backups, profiles, and generated JSON stored there will not appear as fake mods.

Sidecar `.manifest` files are handled conservatively. A `.manifest` file must declare an explicit mod `id` before ModTheSpire2 treats it as a mod. Variant manifests such as RitsuLib's version-selection sidecar are ignored unless they declare their own mod identity.

## Load Order

The launcher can move mods up/down, apply a numeric order, and save a custom load order. The in-game management overlay can also save the same order file:

```text
ModTheSpire2Data/load-order.txt
```

This file stores one mod id per line. Dependency rules are enforced so required mods stay before mods that depend on them. Order-only `load_after` and `load_before` hints are also repaired when the referenced mod is installed. The saved order is applied the next time the game is launched through ModTheSpire2.

Enabled mod selections are stored separately:

```text
ModTheSpire2Data/enabled-mods.txt
```

This file stores one enabled mod id per line. It is updated by `Save Order` and `Launch Selected`, but not by `Vanilla`.

Named order profiles are stored under:

```text
ModTheSpire2Data/order-profiles/
```

Before overwriting `load-order.txt`, `enabled-mods.txt`, or named order profile files, the launcher copies the previous file into:

```text
ModTheSpire2Data/file-backups
```

Before writing the game's `settings.save`, the launcher searches the normal Slay the Spire 2 roaming settings folder and selects the newest `settings.save` it can find. This avoids hardcoding one Steam user id on computers where more than one Steam account has played the game.

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
