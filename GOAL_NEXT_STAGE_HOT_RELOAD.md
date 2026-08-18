# ModTheSpire2 Next Goal: Ecosystem Compatibility And Management UX Hardening

## Goal Statement For Goal Mode

Continue development from the current confirmed ModTheSpire2 0.4.0 development baseline.

Current confirmed baseline:

- Steam Workshop item: `3747911678`
- GitHub repository: `https://github.com/grassdog0/MTS2`
- GitHub pushed merge commit: `f76964d`
- Clean Workshop content contains exactly:
  - `ModTheSpire2.dll`
  - `ModTheSpire2.json`
  - `ModTheSpire2.pck`
  - `ModTheSpire2Launcher.exe`
  - `README.md`
- Current confirmed usable development build:
  - snapshot `dist\snapshots\hot-reload\20260621-2017-top-restart-safe-hotapply`
  - manual test package `dist\TestPackages\ModTheSpire2-20260621-2017-top-restart-safe-hotapply`
  - version `0.4.0`
  - clean package size about `551.92 KB`
  - DLL SHA256 `2EDB315C92E13317AAD27737A6F88A29BBC67C74767BB070DC7FCF313D67DA0E`
  - launcher SHA256 `7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606`

The previous stage already implemented the first usable version of:

- Steam launch-option launcher flow.
- Pre-launch mod selection.
- Dependency-aware launcher grouping and selection.
- Detection of standard mod JSON files and Workshop mods that only provide `mod_manifest.json`.
- Saved enabled-mod selections.
- Custom load order, numeric order entry, Move Up, Move Down, Reset Order, Save Order, and named order profiles.
- In-game ModTheSpire2 management overlay.
- Hot-Apply candidates vs Restart Required grouping.
- Conservative hot-apply rollback/downgrade behavior.
- Safe Close and Open Launcher flow.
- GitHub and Workshop release packaging.
- Minimum dependency-version and minimum game-version compatibility checks.
- In-game management overlay with a top-bar `Close and Open Launcher` button, a top-right `X` close button, scroll-safe layout, and an inline `Apply` button for Hot-Apply candidates.

The new goal is not to rebuild those features from scratch. The new goal is to harden and improve them for the real Workshop mod ecosystem.

## New Stage Objective

Build the next development version of ModTheSpire2 focused on:

1. More reliable dependency detection across current Workshop mods.
2. More robust load-order validation and repair.
3. Better in-game and launcher UI polish.
4. Broader but more precise state-aware Hot-Apply behavior.
5. Better compatibility with framework/library mods such as BaseLib and RitsuLib, without making either one a hard dependency.

The target result should be a new usable build, likely `0.4.1` unless a different version is chosen during release preparation.

## Important Context From RitsuLib Inspection

Workshop item `3747602295` is `RitsuLib`.

Observed structure:

- `mod_manifest.json`
- `ritsulib-variants.manifest`
- root `STS2-RitsuLib.dll`
- versioned implementation under `lib/0.107.1/STS2-RitsuLib.dll`
- generated API XML documentation under `lib/0.107.1/STS2-RitsuLib.xml`
- web log viewer assets under `viewer/`

Observed manifest facts:

- id: `STS2-RitsuLib`
- name: `RitsuLib`
- version: `0.4.30`
- `has_dll`: true
- `has_pck`: false
- `affects_gameplay`: false
- `min_game_version`: `0.107.1`

Implications for ModTheSpire2:

- Framework/library mods can be DLL-based while not directly affecting gameplay.
- A mod may use non-trivial internal layout such as versioned DLL folders and sidecar manifests.
- `affects_gameplay: false` is not enough to classify a DLL mod as hot-apply safe.
- RitsuLib should be treated as Restart Required by default because it loads DLL code and provides runtime services.
- RitsuLib should be usable as a dependency root for other mods.
- ModTheSpire2 should not depend on RitsuLib or BaseLib just to operate.

## Important Context From WuWa Ancients

Workshop item `3747583646` is `鸣潮先古`, with mod id `wuwancients`.

Observed structure:

- `wuwancients.json`
- `wuwancients.dll`
- `wuwancients.pck`

Observed manifest facts:

- id: `wuwancients`
- version: `0.61.1`
- `has_dll`: true
- `has_pck`: true
- `affects_gameplay`: true
- dependency: `BaseLib` minimum `v3.2.0`
- minimum game version: `0.107.0`

User-observed behavior:

- The mod can expose a main-menu setting that controls whether new ancient immigrants/events appear.
- That setting can apply without restarting when the game is at the main menu.

Corrected implications for ModTheSpire2:

- Some DLL/PCK/gameplay mods may expose their own runtime-safe settings.
- A mod-owned runtime-safe setting is not the same thing as safely enabling/disabling the entire loaded mod.
- `wuwancients` must not be treated as an entire-mod Hot-Apply candidate. Unchecking the loaded DLL/PCK mod cannot unload or disable its already-loaded effects.
- Use `wuwancients` as an investigation sample for future per-mod setting integration only.
- Whole-mod enable/disable for `wuwancients` and similar DLL/PCK content mods must go through `Close and Open Launcher` / restart flow unless a future implementation applies a specific proven-safe setting through that mod's own API.
- Hot-Apply must classify each change, not only each mod: per-mod setting changes and whole-mod enablement are different change types.

## Preserve Existing Behavior

Do not regress these existing 0.4.0 behaviors:

- Steam launch option still opens the native launcher first.
- Vanilla launch is one-time only and does not erase saved enabled-mod selections.
- Launch Selected saves enabled mods and applies load order.
- Save Order saves both `load-order.txt` and `enabled-mods.txt`.
- Checkboxes follow mods when order changes.
- First-row Move Up and last-row Move Down are harmless.
- Dependent mods cannot be selected unless required dependencies are selected.
- Dependency order remains valid even if unrelated independent mods are between a dependency and a dependent mod.
- In-game Close and Open Launcher does not spawn a second running game instance.
- In-game management overlay keeps `Close and Open Launcher` visible in the top bar without scrolling.
- In-game management overlay keeps a top-right `X` close button visible without scrolling.
- In-game management overlay keeps an inline `Apply` button near Runtime Hot-Apply / Apply at Main Menu candidates.
- Copy Launch Option remains inside the safe confirmation dialog.
- The Workshop upload stays clean and does not include runtime `ModTheSpire2Data`.

## Main Work Areas

### 1. Dependency And Manifest Compatibility

Improve mod scanning and dependency interpretation for real Workshop mods.

Required investigation:

- Inspect more Workshop mods with unusual layouts.
- Identify all known manifest names currently used by STS2 mods, including misspellings or compatibility aliases.
- Confirm how dependencies are represented in:
  - `mod_manifest.json`
  - `<mod>.json`
  - sidecar manifests such as `ritsulib-variants.manifest`
  - any other current ecosystem pattern
- Confirm whether dependencies are always Workshop IDs, mod IDs, or mixed values.
- Decide how to display unresolved dependencies.

Implementation expectations:

- Prefer mod id matching.
- If a dependency only matches a Workshop folder id or title, handle it conservatively and document the heuristic.
- Show missing dependencies clearly in the launcher and in-game overlay.
- Prevent selecting a dependent mod when a required dependency is missing or disabled.
- Keep dependencies before dependents during load-order repair.
- Avoid treating arbitrary JSON files as mods unless they contain a valid mod identity.

### 2. Framework/Library Mod Handling

Treat framework mods as first-class dependency roots.

Initial examples:

- BaseLib
- RitsuLib / `STS2-RitsuLib`
- ModTheSpire2 itself

Expected behavior:

- Framework mods should appear as parent/root entries when other mods depend on them.
- Framework mods with DLLs are Restart Required by default.
- Mods depending on restart-required framework DLLs should be conservatively Restart Required unless proven otherwise.
- The UI should explain the reason in plain English during the optimization phase.

Do not add a hard dependency on BaseLib or RitsuLib.

### 3. Load Order Hardening

The current load-order feature exists. This stage should harden it.

Required improvements:

- Validate saved order profiles before applying them.
- Repair saved profiles when mods are missing, renamed, or newly installed.
- Put newly installed mods after existing ordered mods by default.
- Keep dependency constraints after profile load, numeric order edits, Move Up/Down, and Save Order.
- Show a clear message if the requested order was adjusted to satisfy dependencies.
- Preserve enabled-mod selections together with profiles, or explicitly separate order-only and order-plus-enabled profiles if that is clearer.

Safety:

- Always back up game settings before writing.
- Never write to a hardcoded Steam user directory.
- If multiple STS2 user settings directories exist, choose the newest relevant one or ask the user.

### 4. State-Aware Hot-Apply Hardening

The first conservative Hot-Apply system exists. This stage should tighten and broaden its semantics.

The new model should classify each change, not only each mod. Per-mod settings and whole-mod enablement are separate change types. A change may be:

- Always restart-required.
- Hot-apply safe only at the main menu when no unfinished run exists.
- Hot-apply safe during a run only for clearly local/non-content settings.
- Unsafe to disable during a run, but safe to enable/disable before a new run.

Current conservative rule to preserve as fallback:

- Unknown behavior is Restart Required.
- ModTheSpire2 itself is Restart Required.
- Framework DLLs such as BaseLib and RitsuLib are Restart Required.
- Loaded DLL/PCK content mods are Restart Required for whole-mod enable/disable changes.
- Config-only or clearly runtime-toggleable entries may be Hot-Apply candidates.

New state-aware rule to investigate and implement:

- If there is an unfinished run or active run state, disabling content-providing mods must be blocked or routed to restart-required flow.
- If there is no unfinished run and the game is at a safe menu state, some future-run changes may be Hot-Apply candidates only when ModTheSpire2 can apply a specific proven-safe setting. This does not mean unchecking the loaded mod is safe.
- Mods that add events, characters, ancient choices, relics, cards, encounters, or similar future-run content should remain restart-required for whole-mod enable/disable unless a specific per-mod setting is integrated and proven safe.
- Mods that patch already-loaded UI, framework services, save serializers, core game methods, or active run behavior remain Restart Required.
- Enabling a dependency must be handled before enabling dependents.
- Disabling a dependency must be blocked if enabled dependents still need it.

Required improvements:

- Make the reason for each classification visible enough for debugging.
- Detect whether the game is currently in a run, at the main menu, or has an unfinished saved run that can be resumed.
- Add classification reason strings such as:
  - `main menu only; unfinished run present`
  - `future-run content toggle`
  - `active run may reference this content`
  - `framework DLL; restart required`
  - `unknown DLL/PCK behavior`
  - `DLL/PCK or unknown startup behavior`
- Add controlled test coverage for:
  - config-only hot-apply candidate
  - content-like config/setting change that is safe only before a run starts
  - DLL framework dependency root
  - missing dependency
  - manifest-only Workshop mod
  - versioned-library layout like RitsuLib
  - BaseLib-dependent gameplay content mod such as `wuwancients`, proving it remains restart-required for whole-mod enable/disable
- Keep the 10-second hot-apply timeout.
- If hot apply fails, rolls back, or cannot access settings, downgrade affected mods to Restart Required for the current session.
- Do not attempt true hot-load, hot-unload, or whole-mod hot-disable of DLL/PCK mods as a default user-facing feature.

Stage B true hot-load remains experimental and is not the main deliverable for this stage.

State detection investigation:

- Find how STS2 records a resumable unfinished run.
- Find whether the current scene/screen can reliably distinguish main menu, character select, map, combat, event, reward, merchant, rest site, game-over, and run history states.
- Determine whether changing mod enablement while a run is merely resumable but not loaded is safe.
- If the answer is uncertain, treat resumable unfinished runs as unsafe for disabling content mods.
- Document the exact signals used for "safe menu state" and "unfinished run present".

Hot-Apply UI expectations:

- Show why an item is currently unavailable instead of hiding it.
- Show a different label for menu-only candidates, for example `Apply at Main Menu`.
- If an unfinished run blocks the change, tell the player to finish/abandon the run or restart with the desired mod list.
- Do not let a player disable a mod that may be referenced by the current or resumable run.
- If a setting is provided by the mod itself and the mod reports/apply-proves that it can change future-run generation safely, allow that specific setting at the main menu. Do not represent this as disabling/enabling the whole mod.

### 5. UI Polish

Improve both the native launcher and in-game overlay.

Goals:

- Make the launcher easier to understand when dependency trees are present.
- Improve visual hierarchy for dependency children, missing dependencies, restart-required reasons, and load-order warnings.
- Make order/profile controls feel deliberate rather than prototype-like.
- Use the game's existing style as much as possible in the in-game overlay.
- Keep English UI/log text during this development stage to avoid encoding issues. Add localization after behavior stabilizes.

Possible improvements:

- Better indentation or tree indicators for dependency children.
- Clear labels for Restart Required reasons.
- A compact warning area for repaired load order.
- More polished profile selector and save/load controls.
- Better disabled-state styling for blocked dependent mods.

### 6. Packaging And Release Hygiene

Every usable build must be easy to roll back and upload.

Requirements:

- Keep a clean content folder with only the five uploadable files.
- Keep GitHub source export clean.
- Keep `README.md`, Workshop description, and manual test docs current.
- Do not include runtime data folders, logs, old Lite folders, or uploader binaries in upload content.
- Run the package verifier before considering a build usable.
- If a build is user-testable and improves behavior, save a snapshot.

## File Access And Safety Boundaries

Even if full filesystem access is granted during Goal mode, all download, edit, generate, delete, and build work must stay inside:

```text
C:\Users\HZDH\Desktop\tmp\Forimpro
```

The only exception is the live test mod folder:

```text
E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2
```

Files in that live `ModTheSpire2` folder may be replaced for testing.

Everything else under the game directory, Steam library, workshop cache, or other system locations must be treated as read-only unless the user explicitly gives a new instruction.

Do not edit, delete, move, or overwrite other game files, other mods, Steam files, or workshop cache files.

## Work Log And Rollback Snapshots

Maintain `GOAL_HOT_RELOAD_PROGRESS.md`.

During active Goal-mode work:

- Append a short progress entry at least once every 30 minutes.
- Record what changed, what was learned, test results, and known failures.
- Save a snapshot whenever a usable build adds a real capability or meaningful improvement.

Snapshot folders should live under:

```text
dist/snapshots/hot-reload/YYYYMMDD-HHMM-short-description
```

Each snapshot should include:

- `ModTheSpire2.dll`
- `ModTheSpire2Launcher.exe`
- `ModTheSpire2.json`
- `ModTheSpire2.pck`
- `README.md`
- `SNAPSHOT.md`

Do not overwrite previous snapshots.

The uploaded 0.4.0 build remains the rollback baseline.

## Test Set

Use current-compatible mods only.

Recommended test set:

- ModTheSpire2
- BaseLib
- Quick Restart or another current BaseLib-dependent mod
- RitsuLib / `STS2-RitsuLib`
- Act 4 Heart / `Act4Heart`
- WuWa Ancients / `wuwancients`, as a BaseLib-dependent DLL/PCK content mod with a main-menu runtime setting. Use it to verify that whole-mod enable/disable remains restart-required while documenting a possible future per-mod setting integration.
- A tiny controlled config-only test mod
- A tiny controlled future-run-content test mod if practical
- One additional current Workshop mod only after confirming it starts on the current STS2 build

Do not use old or unstable mods as proof of ModTheSpire2 failure unless they are first confirmed to work without ModTheSpire2 changes.

SpeedX, ModConfig, and DamageMeter remain excluded from core acceptance testing unless their current-version compatibility is re-confirmed.

## Acceptance Criteria

This stage is complete when:

- The scanner handles standard manifests, `mod_manifest.json`, and real framework/library layouts without missing current Workshop mods such as Act4Heart or RitsuLib.
- Dependency roots and children are displayed clearly in the launcher.
- Missing dependencies are visible and block unsafe selection.
- Framework/library DLL mods are classified conservatively as Restart Required.
- Hot-Apply classification is state-aware: runtime/config candidates, proven per-mod setting candidates, blocked candidates, and restart-required mods are separated clearly.
- Content-providing mods cannot be disabled while an active or resumable unfinished run may reference their content.
- A mod like `wuwancients` is used to investigate and document safe main-menu content toggles, while explicitly keeping whole-mod enable/disable for loaded DLL/PCK content mods restart-required.
- `wuwancients` is not shown as an entire-mod Hot-Apply candidate. Its classification for whole-mod enable/disable remains `DLL/PCK or unknown startup behavior` or an equivalent restart-required reason.
- Load-order profiles survive missing mods, new mods, and dependency repairs.
- Saved enabled selections still work and Vanilla still remains one-time only.
- Hot-Apply candidates remain conservative and rollback-safe.
- The in-game overlay and native launcher UI are noticeably more polished than 0.4.0.
- Package verification passes.
- A clean Workshop package and clean GitHub source export are produced.
- `README.md`, Workshop description, and manual test docs are updated.

## Non-Goals

- Do not promise universal hot reload.
- Do not true-load, hot-unload, or whole-mod hot-disable arbitrary DLL/PCK mods after startup as a default feature.
- Do not allow disabling content mods during an active or resumable run unless proven safe.
- Do not infer that a DLL/PCK gameplay mod can be unchecked and disabled without restart just because one of its own settings can change safely at the main menu.
- Do not make BaseLib or RitsuLib mandatory for ModTheSpire2.
- Do not require players to install .NET or external runtimes.
- Do not overwrite user game files outside the allowed live ModTheSpire2 test folder.
- Do not optimize package size ahead of correctness for this stage.

## First Practical Steps

1. Start from snapshot `dist\snapshots\hot-reload\20260621-2017-top-restart-safe-hotapply`.
2. Re-run the current package verifier to establish the starting state.
3. Preserve the current in-game overlay usability fixes: viewport-safe scrolling, top-bar `Close and Open Launcher`, top-right `X`, and inline Hot-Apply `Apply`.
4. Preserve the corrected Hot-Apply semantics: `wuwancients` and similar loaded DLL/PCK content mods are restart-required for whole-mod enable/disable.
5. Continue investigating per-mod runtime-safe settings separately from whole-mod enablement.
6. Inspect a small set of current Workshop mods for additional dependency/manifest patterns.
7. Improve dependency resolution and load-order repair only if a real missing pattern is found.
8. Polish launcher and in-game UI without regressing action visibility.
9. Update manual tests.
10. Build, verify, snapshot, and only then prepare a new uploadable package.

## Current Completed Groundwork

This stage has already made meaningful progress beyond the uploaded 0.4.0 Workshop baseline:

- Standard JSON, `mod_manifest.json`, malformed manifest fallback, payload-backed `settings.json`, typo manifest names, sidecar manifests, and pck-name fallbacks are covered by tests.
- Dependency id aliases, display-name aliases, Workshop folder aliases, optional dependencies, `load_after` / `load_before`, and dependency minimum versions are covered.
- Minimum STS2 game version fields are detected and displayed.
- RitsuLib is detected as a framework/library DLL and remains Restart Required.
- `wuwancients` is detected with its BaseLib dependency and minimum game version, but remains Restart Required for whole-mod enable/disable.
- In-game overlay has a usable top-bar restart path and close path, plus an inline Apply button for safe candidates.
- Package verifier currently passes against the live test folder.
