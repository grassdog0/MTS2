# ModTheSpire2

ModTheSpire2 is a pre-launch mod selector and in-game companion manager for Slay the Spire 2.

Current version: `0.4.0`.

Repository URL:

```text
https://github.com/grassdog0/MTS2
```

## Current Test Build Notes

- Keeps the simple restart-manager workflow: change whole-mod enablement in the launcher, then start the game.
- Refreshes the in-game `ModTheSpire2 Launcher` button so it stays visible and interactive when other mod-setting UI layers are present.
- The launcher now uses clearer profile wording: after changing order, choose or type a profile name, then click `Save`.
- Added a regression check for overlapping mod ids/names, such as a disabled `Hina` entry next to enabled `TenshiHinanawi`, so `settings.save` enabled state must be read by exact id.
- Adds read-only Better Mod Menu grouping import from `ModGroups` or CSV exports when available.
- Adds an experimental read-only multiplayer mismatch helper with report, Workshop link, and copy-feedback actions.
- Adds a v0.111 Vanilla compatibility safeguard: the launcher snapshots the selected mods, writes every mod entry disabled for the Vanilla launch, and restores the snapshot after the game-side reset.

## Features

- Native Win32 launcher. Players do not need to install .NET to use the Windows launcher.
- First-pass lightweight Linux and macOS launch scripts for native Steam builds.
- Choose vanilla or selected mods before starting Slay the Spire 2.
- Dependency-aware mod list. Dependent mods are shown under required mods and cannot be selected until dependencies are selected.
- Compatible with standard mod JSON files and Workshop mods that only provide `mod_manifest.json`.
- Manual load order controls: Move Up, Move Down, numeric order entry, and named profile saving.
- Named profiles save both load order and enabled selections. Select an existing profile or type a new profile name, then click `Save`; existing names are updated and new names are created.
- Launcher default state follows the current game `settings.save` order and enabled mods for compatibility with other order managers.
- Selecting a named profile applies it immediately; selecting `Current settings.save` reloads from `settings.save`.
- Linux/macOS scripts can launch Vanilla, saved enabled mods, or named profiles from `ModTheSpire2Data`.
- Saved enabled-mod selections in `ModTheSpire2Data/enabled-mods.txt`.
- Vanilla launch writes every discovered `mod_list` entry as disabled while preserving order, and saves the selected mod ids before starting. This also handles v0.111, where the game may remove the old global switch and re-enable mod entries after a Vanilla launch.
- The v0.111 recovery state also snapshots the current order in `ModTheSpire2Data/vanilla-load-order.txt`, so the next launcher session restores both selection and order without overwriting the normal `load-order.txt`.
- In-game ModTheSpire2 management entry on the game's Mod Settings page.
- Clean in-game restart helper for changing whole-mod enablement through the launcher.
- Read-only multiplayer mismatch report helper in ModTheSpire2 Management.
- Restart-required treatment for DLL/PCK/UI/gameplay/unknown whole-mod changes.
- Safe Close and Open Launcher flow that avoids starting a second game instance while the first is still running.
- Close and Open Launcher forwards the current game command line, preserving renderer flags such as `--rendering-driver opengl3`.
- `settings.save` backups before ModTheSpire2 writes mod settings.

## Important Behavior

ModTheSpire2 itself is restart-required. It ships a DLL and patches game UI, so disabling it cannot unload the already-loaded code during the current session.

Whole-mod enablement is managed before startup. The in-game companion does not claim it can safely unload already-loaded DLL/PCK/content/UI mods inside the current game process. Use `Close and Open Launcher` to change enabled mods or profiles.

The Windows launcher's default combo entry is `Current settings.save`. It reads the current `settings.save` directly, including order and enabled state written by the base game or other mod-order tools. ModTheSpire2 only writes `settings.save` when the player launches Vanilla or Launch Selected, and named profiles remain explicit presets. After a Vanilla launch, the one-time recovery snapshot temporarily takes precedence so both the previous selection and the previous order can be restored.

For v0.111 Vanilla compatibility, a successful Vanilla launch leaves a small pending marker and a temporary order snapshot under `ModTheSpire2Data`. The next launcher session restores the saved enabled-mod selection and load order even if the game rewrote every `is_enabled` value to `true`. The snapshot is separate from named profiles and the normal `load-order.txt`; it is cleared when a modded launch is written, so normal external settings changes remain visible when no Vanilla recovery is pending.

Use the `Save` button to save the currently visible launcher order and checked mods into the selected or typed profile name. `Current settings.save` is a live view and cannot be overwritten as a named profile.

Steam Workshop cannot set Steam launch options automatically. Players who want Steam's Play button to open ModTheSpire2 first must set:

```text
"<path to ModTheSpire2Launcher.exe>" -- %command%
```

Keep `%command%` exactly as written. Do not replace or remove it. If the game needs extra launch arguments, add them after `%command%`, for example:

```text
"<path to ModTheSpire2Launcher.exe>" -- %command% --rendering-driver opengl3
```

The in-game Close and Open Launcher confirmation dialog includes a copy button for this launch option.

Linux native Steam launch option:

```text
"/path/to/ModTheSpire2Launcher.sh" -- %command%
```

macOS native Steam launch option:

```text
"/path/to/ModTheSpire2Launcher.command" -- %command%
```

The Linux/macOS launchers are lightweight first-pass scripts. They do not yet provide the full Windows checkbox GUI.

## Better Mod Menu Grouping

The Windows launcher includes a read-only `Group` column. It tries Better Mod Menu `ModGroups` first, then Better Mod Menu CSV exports, then ModTheSpire2 fallback categories. It does not write Better Mod Menu files and does not require Better Mod Menu.

## Multiplayer Mismatch Helper

The multiplayer helper is experimental and read-only.

When the game reports a multiplayer `ModMismatch`, ModTheSpire2 attempts to append help text to the existing error, record missing local/host mod details, and map known entries to Workshop links.

Latest report:

```text
ModTheSpire2Data/multiplayer-mismatch-last.txt
```

In ModTheSpire2 Management:

- `Open Missing Mod Links` opens Workshop links from the latest report.
- `Copy Mismatch Report` copies the full report for feedback.

This does not force join, bypass mismatch checks, auto-subscribe Workshop items, or change lobby/network state.

## Repository Layout

```text
dist/WorkshopUpload/ModTheSpire2Content-Clean/  Uploadable mod content
NativeLauncher/                                  Win32 launcher source
NativeLauncher/ModTheSpire2Launcher.sh           Shared Linux/macOS lightweight script source
NativeLauncher/ModTheSpire2Launcher.command      macOS command wrapper
ModTheSpire2Companion/ModTheSpire2Entry.HotManage.cs
                                                 Current in-game companion source
Tools/                                           Verification and helper scripts
TestMods/                                        Controlled manifests for scanner and dependency tests
MANUAL_TEST_0.4.0.md                             Manual test checklist
GOAL_CLEAN_RESTART_MANAGER.md                    Current design/goal notes
GOAL_HOT_RELOAD_PROGRESS.md                      Development log
```

## Build Notes

The native launcher is built with MinGW:

```powershell
x86_64-w64-mingw32-gcc NativeLauncher\ModTheSpire2Launcher.c -municode -mwindows -O2 -Wall -Wextra -o NativeLauncher\ModTheSpire2Launcher.exe -lcomctl32 -lshell32 -lole32 -luuid -luxtheme
```

The in-game companion DLL is built from `ModTheSpire2Entry.HotManage.cs` with Roslyn `csc` against the Slay the Spire 2 managed DLL set. The local `.csproj` is not the authoritative build path because historical prototype files are kept in the folder.

## Verification

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1
```

If the game is currently running and locking the live test DLL, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

`-SkipLive` validates clean Workshop content and uploader content only. Final local release testing should run without `-SkipLive` after the game exits.

## Runtime Data

ModTheSpire2 writes runtime data under:

```text
ModTheSpire2Data/
```

This folder is intentionally not part of the Workshop upload. It may contain:

- `load-order.txt`
- `enabled-mods.txt`
- `order-profiles/`
- `settings-backups/`
- `launcher.log`
- `companion.log`
- `multiplayer-mismatch-last.txt`
