# ModTheSpire2 Goal: Cross-Platform Lightweight Launcher

Use this file as the authoritative Goal-mode prompt for the next ModTheSpire2 development stage.

## Goal Statement

Build a lightweight launcher path that can be used on Windows, Linux, and macOS.

The first target is not a perfect feature-complete cross-platform UI. The first target is a reliable, small, usable launcher experience on all three desktop platforms so players can start Slay the Spire 2 with selected mods.

Current product direction:

- Keep the existing Windows launcher working.
- Add Linux and macOS launcher support.
- Prefer the smallest practical implementation that can launch the game with selected mods.
- Keep the existing clean restart-manager philosophy: whole-mod enablement is changed before startup.
- Do not revive broad whole-mod Hot-Apply as the default workflow.
- Preserve existing Windows behavior unless a change is required and verified.

## Ask-If Rule

Ask the user before proceeding if any of these are unclear and cannot be verified locally:

- Whether a platform should target native Slay the Spire 2, Proton/Wine, or both.
- The exact Linux/macOS Steam install paths to support.
- Whether Workshop packaging may include extra platform launcher files.
- Whether a platform-specific launcher may be a shell script first.
- Whether a change would alter the already-working Windows launcher behavior.
- Whether testing requires writing outside `C:\Users\HZDH\Desktop\tmp\Forimpro` or the approved live test folder.
- Whether a dependency/runtime is acceptable for normal players.

Prefer asking over guessing when the answer affects player setup, package contents, or platform behavior.

## Design Principle

Do not write three unrelated launchers if avoidable.

Aim for:

```text
shared launcher logic
  - discover game/mod folders
  - scan local and Workshop mods
  - parse manifests
  - validate dependencies
  - save enabled mods
  - save load order
  - write game settings
  - start Slay the Spire 2

platform entry points
  - Windows: existing ModTheSpire2Launcher.exe
  - Linux: lightweight shell/native entry point
  - macOS: lightweight .command/native entry point
```

For the first implementation, a small amount of duplicated platform script logic is acceptable if it gets Linux/macOS users working quickly. Long term, move shared behavior into a common data/logic layer.

## Platform Targets

### Windows

Preserve current behavior:

- `ModTheSpire2Launcher.exe` remains the Windows launcher.
- Steam launch option format remains:

```text
"<path to ModTheSpire2Launcher.exe>" -- %command%
```

- Extra game arguments must go after `%command%`, for example:

```text
"<path to ModTheSpire2Launcher.exe>" -- %command% --rendering-driver opengl3
```

- Do not change double-click behavior unless explicitly requested.

### Linux

First practical target:

- Provide a lightweight Linux entry point, likely `ModTheSpire2Launcher.sh`.
- Support Steam/Steam Deck players first.
- Determine whether the first version should target:
  - native Linux Slay the Spire 2
  - Windows build through Proton
  - both, if simple enough
- Detect common Steam library and Workshop paths.
- Launch the game through Steam-compatible commands when possible.
- Preserve extra game arguments passed after the original game command.

Avoid requiring users to install large runtimes.

### macOS

First practical target:

- Provide a lightweight macOS entry point, likely `ModTheSpire2Launcher.command`.
- Support Steam macOS players first.
- Detect common Steam library and Workshop paths.
- Launch Slay the Spire 2 using the native macOS app/Steam command where possible.
- Preserve extra game arguments when possible.

Avoid requiring Homebrew, .NET, Electron, or other extra installs for normal players unless the user explicitly approves.

## First-Version Scope

The cross-platform first version should be allowed to be simpler than the Windows UI.

Minimum useful behavior:

- Find the game folder.
- Find local `mods` and Workshop mod folders.
- Detect ModTheSpire2's own folder.
- Read enough manifest data to list mod ids/names.
- Let the player choose a saved profile or enabled-mod list.
- Save enabled mods in the same ModTheSpire2 data folder format when possible.
- Write the game's mod settings in the platform's active settings file.
- Start Slay the Spire 2 with the selected enabled state.
- Preserve extra launch arguments passed by Steam.

If a full checkbox GUI is too large for the first pass, a simple terminal/menu/script-based launcher is acceptable as a temporary first usable version, but document the limitation clearly.

## Preserve From Current Build

Do not regress:

- Windows launcher starts correctly from Steam launch options.
- Windows launcher forwards extra game arguments after `%command%`.
- Windows launcher detects local and Workshop mods.
- Windows launcher saves enabled selections and load order.
- Windows launcher handles dependencies conservatively.
- Vanilla launch remains one-time and does not erase normal saved selections.
- Existing in-game `Close and Open Launcher` flow remains usable on Windows.
- Workshop package stays clean and avoids runtime data/log folders.

## Packaging Direction

The current five-file Windows package may need to expand for cross-platform support.

Potential future upload content:

```text
ModTheSpire2.dll
ModTheSpire2.json
ModTheSpire2.pck
ModTheSpire2Launcher.exe
ModTheSpire2Launcher.sh
ModTheSpire2Launcher.command
README.md
```

Do not change the package file list without updating:

- Workshop README
- GitHub README
- uploader `workshop.json`
- package verifier
- manual test checklist

Do not include:

- runtime data
- logs
- snapshots
- test packages
- local build output
- uploader binaries

## Documentation Requirements

Update docs with platform-specific instructions:

- Windows Steam launch option.
- Linux/Steam Deck setup.
- macOS setup.
- How to keep `%command%` or the platform equivalent.
- How to pass extra game arguments such as `--rendering-driver opengl3`.
- Known limitations of the first Linux/macOS implementation.

Keep development-facing UI/docs in English first to avoid encoding issues. Add other languages only after behavior stabilizes.

## Verification Requirements

For each usable build:

- Run current Windows package verification.
- Add Linux/macOS script/static verification where possible.
- Confirm package contains only intended upload files.
- Save a rollback snapshot for meaningful progress.
- Update manual test notes.
- Keep GitHub source export clean.

Manual testing should explicitly cover:

- Windows Steam launch option still works.
- Windows extra launch arguments still pass through.
- Linux launcher starts or reaches a clear actionable error.
- macOS launcher starts or reaches a clear actionable error.
- The launcher does not create two running game instances.

## File Access And Safety Boundaries

All download, edit, generate, delete, and build work must stay inside:

```text
C:\Users\HZDH\Desktop\tmp\Forimpro
```

The only external writable live test target is:

```text
E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2
```

Everything else under the game directory, Steam library, Workshop cache, and other mod folders is read-only unless the user explicitly gives new permission.

## Explicit Non-Goals

- No universal whole-mod Hot-Apply.
- No arbitrary DLL/PCK hot-unload.
- No large runtime dependency for normal players unless explicitly approved.
- No Electron app unless explicitly approved.
- No hard dependency on BaseLib/RitsuLib.
- No rewrite that breaks the current Windows launcher.
- No claim of Linux/macOS support until at least a launcher entry point exists and is manually testable.

## Acceptance Criteria

This stage is ready when:

- Windows launcher still passes existing verification.
- A Linux entry point exists and is documented.
- A macOS entry point exists and is documented.
- Platform instructions are clear enough for a player to set up without guessing.
- Package verification covers the new files.
- Manual test package is produced.
- Workshop metadata is updated but not uploaded until the user confirms manual testing.
- GitHub beta source export is updated.

## First Practical Steps

1. Read this file completely.
2. Inspect the current Windows launcher and docs.
3. Ask the user for unresolved Linux/macOS target assumptions before implementation if local evidence is insufficient.
4. Decide whether the first Linux/macOS implementation is script-based or native.
5. Add the smallest usable Linux and macOS entry points.
6. Update package verifier and docs.
7. Build/sync a manual test package.
8. Wait for user manual testing before Workshop upload.
