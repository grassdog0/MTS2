# ModTheSpire2

ModTheSpire2 is a pre-launch mod selector and in-game companion manager for Slay the Spire 2.

Current version: `0.4.0`.

## Current Test Build Notes

- Keeps the simple restart-manager workflow: change whole-mod enablement in the launcher, then start the game.
- Refreshes the in-game `ModTheSpire2 Launcher` button so it stays visible and interactive when other mod-setting UI layers are present.
- Places the in-game `Launcher` button beside `Installed Mods`, reuses the native `Get Mods` button style, and follows the game's typography and scaling.
- Improves the management dialog with a readable native-style layout, scroll support, reliable close/cancel behavior, and clearer launch-option text selection.
- The launcher now uses clearer profile wording: after changing order, choose or type a profile name, then click `Save`.
- Added a regression check for overlapping mod ids/names, such as a disabled `Hina` entry next to enabled `TenshiHinanawi`, so `settings.save` enabled state must be read by exact id.
- Adds read-only Better Mod Menu grouping import from `ModGroups` or CSV exports when available.
- Adds an experimental read-only multiplayer mismatch helper that can record host/local mod mismatch details, show Workshop links when known, and provide Open/Copy report actions.

## What This Mod Does

- Adds a `ModTheSpire2 Launcher` button under the game's mod settings screen when this mod is enabled.
- Provides a native Windows launcher: `ModTheSpire2Launcher.exe`.
- Adds first-pass lightweight native Steam launch scripts for Linux and macOS:
  - `ModTheSpire2Launcher.sh`
  - `ModTheSpire2Launcher.command`
- Lets players choose vanilla launch or selected mods before starting the game.
- Groups dependent mods under their required mods in the Windows launcher.
- Blocks selecting a dependent mod until its required mod is selected in the Windows launcher.
- Detects standard mod JSON files, Workshop `mod_manifest.json` layouts, legacy `pck_name` manifests, and explicit-id `.manifest` sidecar mod manifests without creating duplicate entries.
- Resolves dependencies by mod id, unique display name, and local Workshop numeric folder id in the Windows launcher.
- Lets players move mods up/down, set a numeric order, and save named launcher profiles in the Windows launcher.
- Opens the Windows launcher from the current `settings.save` order and enabled state, making it friendlier with other order managers.
- Supports named launcher order profiles.
- The in-game `ModTheSpire2 Launcher` button opens the Close and Open Launcher confirmation so players can safely change whole-mod enablement before restart.
- The in-game management panel can show the latest multiplayer mod mismatch report status, open recorded Workshop links, and copy the report for feedback.
- Saves ModTheSpire2 state in `ModTheSpire2Data` inside this mod folder.
- Backs up `settings.save` before writing mod enablement or load-order changes.

## Important Safety Notes

- The launcher's live view is `Current settings.save`, read from the game's current `settings.save`.
- `Vanilla` starts the game with mods disabled for that launch while preserving the per-mod list and order for later modded launches.
- Named profiles are separate saved presets and are not deleted by Vanilla.
- `Launch Selected` on Windows saves the currently checked mods and then starts the game with those mods enabled.
- Linux/macOS scripts are first-pass lightweight launchers. They use saved enabled mods or named profiles rather than a full checkbox GUI.
- `Save` on Windows saves the current load order and enabled mod selection into the selected or typed profile name.
- `Close and Open Launcher` first closes the current game, then opens the launcher. This avoids running two Slay the Spire 2 instances at once.
- `Close and Open Launcher` forwards the current game command line to the launcher, so renderer flags such as `--rendering-driver opengl3` are preserved for the next launch.
- The standalone `Open Launcher` action was intentionally removed from the in-game overlay because launching another game instance while the current one is still running can risk settings/save conflicts.

## Restart-Based Mod Changes

ModTheSpire2 manages whole-mod enablement before the game starts. This is intentional.

Many Slay the Spire 2 mods load DLLs, PCK files, UI patches, save hooks, or gameplay content during startup. Once that code or content is loaded, ModTheSpire2 does not claim it can safely unload the entire mod inside the current game process.

To change enabled mods on Windows:

1. Open Settings / General / Mod Settings.
2. Click `ModTheSpire2 Launcher`.
3. Click `Close and Open Launcher`.
4. Confirm the restart dialog.
5. Select Vanilla or the desired mod profile in the launcher.
6. Start the game again.

ModTheSpire2 itself is restart-required. It changes the game's UI through a DLL/Harmony patch, so disabling it cannot unload the already-loaded code during the current session.

If another mod exposes its own runtime-safe configuration through BaseLib or a similar framework, that setting belongs to that mod. ModTheSpire2 does not replace BaseLib and does not make BaseLib a hard dependency.

## Dependency Compatibility

The Windows launcher reads dependency declarations from:

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
- Missing dependencies block unsafe selection in the Windows launcher.

Runtime data folders named `ModTheSpire2Data` are ignored during mod scanning. Logs, backups, profiles, and generated JSON stored there will not appear as fake mods.

Sidecar `.manifest` files are handled conservatively. A `.manifest` file must declare an explicit mod `id` before ModTheSpire2 treats it as a mod. Variant manifests such as RitsuLib's version-selection sidecar are ignored unless they declare their own mod identity.

## Load Order And Profiles

The Windows launcher can move mods up/down, apply a numeric order, and save named profiles. Dependency rules are enforced by the Windows launcher so required mods stay before mods that depend on them. Order-only `load_after` and `load_before` hints are also repaired when the referenced mod is installed.

The launcher's live `Current settings.save` view is read directly from the game's current `settings.save`. This means changes made by the game, RitsuLib, Better Mod Menu, or another order manager are reflected the next time ModTheSpire2 opens.

Named profiles are stored under:

```text
ModTheSpire2Data/order-profiles/
```

Each profile stores one mod id per line. Each named profile can also have a sidecar enabled-mod file:

```text
ModTheSpire2Data/order-profiles/<profile>.enabled.txt
```

Use the `Save` button to save the current visible order and checked mods into the selected or typed profile name. If the profile already exists, it is updated. If it does not exist, it is created.

The legacy saved enabled-mod list is still stored at:

```text
ModTheSpire2Data/enabled-mods.txt
```

The Linux/macOS scripts can launch Vanilla, the saved enabled-mod list, or a named profile that already exists.

On Windows, choosing a named profile from the launcher profile list applies it immediately. Choose `Current settings.save` to return to the live `settings.save` view.

## Better Mod Menu Grouping

The Windows launcher has a read-only `Group` column.

ModTheSpire2 first tries to read Better Mod Menu group data from the player's Better Mod Menu profile data. If `ModGroups` is empty or unavailable, it can also read Better Mod Menu CSV exports when present. If no Better Mod Menu grouping data is available, ModTheSpire2 falls back to simple built-in categories such as dependencies/libraries, gameplay content, UI/QoL, cosmetic, utility/tools, and unknown.

This is read-only. ModTheSpire2 does not write Better Mod Menu files and does not require Better Mod Menu.

## Multiplayer Mod Mismatch Helper

The multiplayer helper is experimental and read-only.

If Slay the Spire 2 reports a multiplayer `ModMismatch`, ModTheSpire2 attempts to append a help section to the game's existing error text. When the game exposes missing mod details, ModTheSpire2 records:

- mods the host has but the local player is missing;
- mods the local player has but the host is missing;
- Workshop links when a missing mod can be matched to local/subscribed metadata.

The latest report is saved at:

```text
ModTheSpire2Data/multiplayer-mismatch-last.txt
```

Open `ModTheSpire2 Management` to see whether a report is available. The management panel includes:

- `Open Missing Mod Links`: opens Workshop links recorded in the latest report, up to a safe limit.
- `Copy Mismatch Report`: copies the full report to the clipboard for feedback.

This helper does not bypass multiplayer checks, does not force join, does not auto-subscribe Workshop items, and does not alter lobby/network state.

Before overwriting `load-order.txt`, `enabled-mods.txt`, or named order profile files, the Windows launcher copies the previous file into:

```text
ModTheSpire2Data/file-backups
```

Before writing the game's `settings.save`, ModTheSpire2 creates a timestamped backup under:

```text
ModTheSpire2Data/settings-backups
```

## Windows Steam Launch Options

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

Keep the literal text `%command%` exactly as written. Do not replace or remove it. Steam uses `%command%` to pass the original Slay the Spire 2 launch command to ModTheSpire2.

If the game needs extra launch arguments, add them after `%command%`. For example, if your GPU needs the OpenGL renderer:

```text
"E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2\ModTheSpire2Launcher.exe" -- %command% --rendering-driver opengl3
```

The in-game `Close and Open Launcher` confirmation dialog includes a copy button for the launch option.

When the game is already running and you use `Close and Open Launcher`, ModTheSpire2 forwards the current game process command line to the launcher. This means a game that was started with extra renderer arguments, such as `--rendering-driver opengl3`, should keep those arguments after the restart flow.

## Linux / Steam Deck First-Pass Setup

The Linux launcher is a lightweight terminal script for the native Steam/Linux build:

```text
ModTheSpire2Launcher.sh
```

Suggested Steam launch option:

```text
"/path/to/ModTheSpire2Launcher.sh" -- %command%
```

If the file is not executable, run this once from a terminal:

```sh
chmod +x "/path/to/ModTheSpire2Launcher.sh"
```

The script tries to discover common Steam library folders, local mods, Workshop mods, and the newest `settings.save`. It then offers a small menu:

```text
1) Launch Vanilla once
2) Launch saved enabled mods
3+) Launch named profile, if profiles exist
d) Diagnostics
q) Quit
```

This first Linux version does not provide the Windows checkbox GUI. Create or edit saved enabled mods/profiles through the Windows launcher, a previous ModTheSpire2 profile, or by editing the text files under `ModTheSpire2Data`.

## macOS First-Pass Setup

The macOS launcher is a lightweight `.command` wrapper for the native macOS Steam build:

```text
ModTheSpire2Launcher.command
```

Suggested Steam launch option:

```text
"/path/to/ModTheSpire2Launcher.command" -- %command%
```

If macOS says the file is not executable, run this once from Terminal:

```sh
chmod +x "/path/to/ModTheSpire2Launcher.command"
chmod +x "/path/to/ModTheSpire2Launcher.sh"
```

The macOS first version uses the same lightweight menu as Linux and has the same limitation: no full checkbox GUI yet.

## Source Code

GitHub:

```text
https://github.com/grassdog0/MTS2
```

## No Extra Runtime

The Windows launcher is a native Win32 program. Linux/macOS first-pass launchers are shell scripts. Normal players should not need to install .NET, Electron, Homebrew, or another large runtime for the launcher.

## File Safety

The install folder intentionally contains only one JSON file: `ModTheSpire2.json`. Extra runtime JSON files can be mistaken for broken mods by the game.

## Included Files

```text
ModTheSpire2.dll
ModTheSpire2.json
ModTheSpire2.pck
ModTheSpire2Launcher.exe
ModTheSpire2Launcher.sh
ModTheSpire2Launcher.command
README.md
```

Do not upload runtime folders such as `ModTheSpire2Data`.
