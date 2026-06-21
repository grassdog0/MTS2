# ModTheSpire2 Goal: Clean Launcher And Restart-Based Mod Manager

Use this file as the authoritative Goal-mode prompt for the next ModTheSpire2 development stage.

## Goal Statement

Continue ModTheSpire2 development from the latest confirmed usable build, but move away from broad Hot-Apply experimentation.

The new product direction is:

- ModTheSpire2 should be a clean native launcher and quick restart helper.
- Players should manage whole-mod enablement before game startup.
- In-game UI should provide a simple way to close the current game and reopen the launcher.
- Whole-mod Hot-Apply for loaded content mods, DLL mods, PCK mods, framework mods, UI patch mods, and unknown mods is not part of this stage.
- The latest Hot-Apply build should be preserved as a future reference blueprint, especially for UI/layout experiments and safety notes, but it should not define the current player-facing workflow.

Current known usable reference build:

- Snapshot: `dist\snapshots\hot-reload\20260621-2017-top-restart-safe-hotapply`
- Manual test package: `dist\TestPackages\ModTheSpire2-20260621-2017-top-restart-safe-hotapply`
- Version: `0.4.0`
- Clean package size: about `551.92 KB`
- DLL SHA256: `2EDB315C92E13317AAD27737A6F88A29BBC67C74767BB070DC7FCF313D67DA0E`
- Launcher SHA256: `7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606`

## Product Philosophy

The player-friendly path is not to pretend every mod can be toggled safely while the game is running.

Many Slay the Spire 2 mods are content, framework, DLL, or PCK mods. They may register cards, characters, relics, events, UI patches, assets, save hooks, or startup-time state. Once those effects are loaded, ModTheSpire2 should not claim it can cleanly unload or disable them in the current game process.

Instead, ModTheSpire2 should make the restart workflow painless:

1. Player starts Slay the Spire 2 through Steam.
2. If the Steam launch option is configured, ModTheSpire2 launcher appears first.
3. Player chooses Vanilla or a saved mod profile.
4. Player adjusts enabled mods and load order if needed.
5. Launcher starts the game with the selected mod list.
6. If the player is already in-game and wants to change enabled mods, they open ModTheSpire2 management and click `Close and Open Launcher`.
7. The current game closes, the launcher opens, and the player changes mods before restarting.

This is the main workflow. Optimize for reliability, clarity, and fewer confusing choices.

## Important Design Decision

Do not build a universal Hot-Apply manager now.

Allowed:

- Keep Hot-Apply research documents for future reference.
- Keep rollback-safe code as internal reference if it does not appear in the normal player workflow.
- Let individual mods or frameworks expose their own runtime-safe configuration.
- Learn UI ideas from BaseLib-style configuration pages.

Not allowed in this stage:

- Do not present DLL/PCK content mods as safely Hot-Apply-able.
- Do not hot-unload arbitrary loaded mods.
- Do not whole-mod disable arbitrary loaded mods in-process.
- Do not promise that unchecking a loaded mod removes its effects without restart.
- Do not make BaseLib, RitsuLib, or any other framework a hard dependency for ModTheSpire2.

`wuwancients` / WuWa Ancients is a useful example of the distinction:

- It may have runtime-safe settings inside its own configuration UI.
- It should not be treated as a whole-mod Hot-Apply candidate by ModTheSpire2.
- Whole-mod enable/disable should remain restart-required.

## Main Objectives

### 1. Clean Native Launcher

Keep the native launcher as the main mod management surface.

Required behavior:

- Detect local mods and Workshop mods reliably.
- Detect non-standard Workshop mods where possible, including payload-backed manifests.
- Show mod name, id, source, dependency status, enabled state, and load order clearly.
- Support Vanilla launch as a one-time launch action.
- Vanilla launch must not erase the normal saved mod selection.
- Save enabled mod choices for normal modded launch.
- Save custom load order.
- Keep named profiles if they are already working.
- New mods should default to a safe position after existing ordered mods unless dependency repair requires otherwise.
- Dependency validation should remain conservative:
  - missing dependency blocks selection
  - too-old dependency blocks selection
  - minimum game version mismatch blocks selection
  - dependency order is repaired or clearly explained

Avoid adding features that make the launcher feel like a full mod configuration framework. The launcher is for startup choices and load order.

### 2. Simple In-Game Management UI

The in-game UI should be a small, readable restart helper, not a full Hot-Apply screen.

Required UI:

- A visible title.
- A visible `Close and Open Launcher` button.
- A visible top-right `X` close button.
- A simple current mod/status list.
- A short explanation that changing enabled mods requires closing the current game and reopening the launcher.
- Layout must stay inside the game window.
- Layout must be scroll-safe at small window sizes.
- No important action should be hidden below an unscrollable area.

Remove or hide player-facing Hot-Apply sections unless they are clearly config-only, proven safe, and not confusing. For this stage, prefer no visible Hot-Apply controls in the default UI.

### 3. BaseLib-Style Configuration As Future Reference

BaseLib-style configuration is useful, but it is not the current goal.

Future idea:

- Content mods can depend on BaseLib or another configuration framework.
- Those mods can expose their own runtime-safe toggles inside their own config pages.
- ModTheSpire2 may later provide links or guidance to those pages.

Current stage:

- Do not require BaseLib.
- Do not build a generalized per-mod setting editor.
- Do not replace the game's existing mod settings page.
- Do not try to infer every mod's internal runtime-safe settings.

### 4. UI Polish

Improve polish only where it supports the simple workflow.

Priorities:

- Launcher should be readable and modern enough for normal players.
- In-game button and dialog should fit the game style better than a raw utility popup.
- Text should be concise.
- Avoid duplicated buttons with the same meaning.
- Avoid scary or overly technical language.
- Use English during development to reduce encoding risk.
- Add other languages only after the simplified workflow is stable.

### 5. Packaging And Release Hygiene

Workshop upload content must remain exactly five files:

- `ModTheSpire2.dll`
- `ModTheSpire2.json`
- `ModTheSpire2.pck`
- `ModTheSpire2Launcher.exe`
- `README.md`

Do not include:

- `ModTheSpire2Data`
- logs
- test packages
- snapshots
- uploader binaries
- old Lite folders
- temporary build output
- local diagnostic files

For each usable build:

- Run `Tools\VerifyModTheSpire2Package.ps1`.
- Update manual test notes.
- Save a rollback snapshot for meaningful progress.
- Keep release/GitHub source export clean.

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

## Preserve From Current Usable Build

Do not regress:

- Steam launch option opens the native launcher first.
- Launcher can start Vanilla or selected mods.
- Vanilla launch is one-time only and does not erase saved selections.
- Saved enabled mod selections are preserved.
- Custom load order remains usable.
- Named profiles remain usable if already implemented.
- Dependency detection remains conservative.
- Workshop mod detection continues to handle Act4Heart and other non-standard manifests that were previously fixed.
- In-game UI has a visible `Close and Open Launcher`.
- In-game UI has a visible top-right `X`.
- In-game UI is scroll-safe.
- `Close and Open Launcher` does not create two running STS2 instances.
- Workshop package remains exactly five files.

## Explicit Non-Goals

- No universal Hot-Apply system.
- No whole-mod Hot-Apply for loaded content mods.
- No arbitrary DLL/PCK hot-unload.
- No attempt to replace BaseLib configuration.
- No hard dependency on BaseLib.
- No hard dependency on RitsuLib.
- No external runtime requirement such as .NET for normal players.
- No large UI rewrite unless it directly improves the launcher/restart workflow.

## Acceptance Criteria

This stage is ready when:

- Launcher detects current local and Workshop mods reliably.
- Launcher can save enabled selections and load order.
- Launcher can load saved profiles if that feature is kept.
- Vanilla launch does not mutate normal saved mod selections.
- Dependencies are shown and enforced conservatively.
- In-game UI clearly offers `Close and Open Launcher`.
- In-game UI no longer suggests that loaded content mods can be safely disabled without restart.
- UI remains usable in small game windows.
- Package verification passes.
- A clean test package is produced.
- A clean Workshop package is produced.
- A clean GitHub source export is produced.

## First Practical Steps

1. Read this file completely before implementation.
2. Establish the current baseline by running the package verifier.
3. Preserve the latest Hot-Apply build as a reference snapshot.
4. Simplify the in-game UI to the restart-helper workflow.
5. Remove or hide player-facing whole-mod Hot-Apply controls.
6. Keep the launcher stable and avoid touching startup flow unless required.
7. Update documentation to explain the simple launcher/restart workflow.
8. Build, verify, sync the live test folder, and create a rollback snapshot.
