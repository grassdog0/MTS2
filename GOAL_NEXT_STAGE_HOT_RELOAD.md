# ModTheSpire2 Next Goal: Hot-Reload Mod Management

## Goal Statement For Goal Mode

Continue optimizing ModTheSpire2 from the current confirmed uploadable baseline. The next stage is to investigate and implement a mod-management UI that distinguishes mods that can be enabled or applied without restarting the game from mods that still require a restart.

The hot-start direction is option C: attempt both hot-apply and true hot-load as a long-term direction, but implement and validate hot-apply first.

- Stage A: hot-apply settings or behavior that is already loaded and can safely change during the current game session.
- Stage B: true hot-load of previously unloaded DLL/PCK mods, only if STS2 exposes a safe mechanism for it.
- The first implementation should focus on Stage A. Stage B is experimental until proven safe.

The work must preserve the current confirmed launcher behavior, including:

- Steam launch option flow remains working.
- The launcher can select mods before game startup.
- Dependency-aware grouping remains working.
- The in-game copy-launch-option button remains working.
- Workshop packaging stays small and uploadable.

Do not replace working binaries casually. Treat the current uploaded/confirmed version as the baseline and make changes in small testable steps.

## Desired User Experience

When the player opens ModTheSpire2's in-game management page, mods should be separated into two groups:

- Hot-startable mods: mods that can be enabled during the current game session without restarting.
- Restart-required mods: mods that cannot safely take effect until the game restarts.

For hot-startable mods, provide a button that applies the selected changes immediately without restarting the game.

For restart-required mods, keep the current behavior: show the existing restart/launcher flow and explain that these mods require restarting through ModTheSpire2.

Dependencies must still be respected. A mod cannot be enabled if its required dependency is disabled. If a dependency is restart-required, any dependent mod should be treated conservatively as restart-required unless proven otherwise.

The UI should also support manual mod load-order management. Players should be able to reorder enabled mods, and ModTheSpire2 should save the custom load order so the launcher can apply it on future launches.

## Required Investigation

Before implementing the final UI, inspect the Slay the Spire 2 mod loading and settings code to determine:

- How the game stores enabled/disabled mods.
- How the game stores mod load order.
- Which changes are applied immediately by the game.
- Whether the game exposes an API or method to load/initialize a mod after the main menu is already running.
- Whether DLL mods, PCK mods, config-only mods, and UI mods behave differently.
- Whether dependencies can be loaded safely after startup.
- Whether "hot-startable" can be determined automatically, or whether it needs metadata/manual classification.

The load-order file may be located near the current user's STS2 Steam config directory, for example:

- `%APPDATA%\SlayTheSpire2\steam\<steam_user_id>`

Do not hardcode a Steam user ID such as `76561199218739049`. Dynamically locate the active STS2 config directory, similar to the existing launcher behavior for `settings.save`.

Do not assume all mods can be hot-started. If the game only supports partial hot changes, document the exact safe subset.

Important distinction:

- Hot apply means changing a setting or toggling already-loaded behavior during the current session, similar to built-in options such as vibration or speed settings.
- True hot load means loading a previously unloaded mod DLL/PCK and running its initialization after the game has already started. This is much riskier and must not be assumed safe.

## UI Priority Order

Try these integration targets in order:

1. Best: replace or extend the game's built-in mod settings page so ModTheSpire2 becomes the primary mod management experience.
2. Acceptable: add a separate ModTheSpire2 page under the game's Settings area.
3. Fallback: integrate with the existing BaseLib/ModConfig page and expose the ModTheSpire2 hot-reload UI there.

Use the highest-priority option that can be implemented safely without breaking game startup or existing mod settings.

## Implementation Requirements

- Keep the current launcher executable stable unless the change explicitly requires launcher work.
- Prefer implementing the new in-game UI in `ModTheSpire2.dll`.
- Use the game's own UI controls/styles where possible.
- UI polish is part of the goal. Prefer reusing the game's original visual style, controls, spacing, and interaction patterns instead of adding generic Godot/Windows-looking UI.
- Avoid adding a hard dependency on BaseLib unless it is only used as a fallback integration path.
- Package size is not the primary optimization target for this stage. Prioritize implementing the requested functionality correctly, safely, and testably. Revisit size reduction only after the features are stable.
- Store any new ModTheSpire2 state inside the mod folder, not in an unrelated C drive location.
- Keep Chinese and English text support in mind, but functionality and safety come first.
- During the optimization and proof-of-concept phase, use English UI/log text by default. Add Chinese and other localization only after the feature is complete and stable, to avoid language encoding issues during core implementation.

## Manual Load Order

Add manual load-order control to the launcher and/or in-game management UI.

Requirements:

- Display enabled mods in their current load order.
- Allow moving a mod up/down, and ideally drag-and-drop if it can be implemented in the game's UI style.
- Save the custom order in a persistent ModTheSpire2 state file inside the ModTheSpire2 mod folder.
- When launching through ModTheSpire2, apply the custom order to the game's settings/load-order data if STS2 supports it.
- Dependencies must constrain ordering: a dependency should load before mods that depend on it.
- If a user tries to move a dependency below a dependent mod, either block the move or automatically repair the order.
- Provide a reset-to-game-default or reset-to-detected-order option.
- Back up any game settings/load-order file before writing.

Investigation tasks:

- Find the exact file and JSON fields used by STS2 for mod order.
- Confirm whether order is stored in `settings.save`, another file in `%APPDATA%\SlayTheSpire2\steam\<steam_user_id>`, or elsewhere.
- Confirm whether changing order requires restart.
- Confirm how missing/uninstalled mods should be handled when loading a saved custom order.

Safety rules:

- Never write to a hardcoded user directory.
- Never write to another Steam user's directory.
- If multiple STS2 Steam user directories exist, choose the newest relevant settings directory or ask the user before writing.
- Always keep a backup before modifying load-order data.

## File Access And Safety Boundaries

Even if full filesystem access is granted during Goal mode, all download, edit, generate, delete, and build work must stay inside:

- `C:\Users\HZDH\Desktop\tmp\Forimpro`

The only exception is the live test mod folder:

- `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2`

Files in that live `ModTheSpire2` folder may be replaced for testing.

Everything else under the game directory, Steam library, workshop cache, or other system locations must be treated as read-only unless the user explicitly gives a new instruction. Do not edit, delete, move, or overwrite other game files, other mods, Steam files, or workshop cache files.

## Work Log And Rollback Snapshots

Maintain a progress log during Goal mode:

- Create or update a Markdown log file inside `Forimpro`, for example `GOAL_HOT_RELOAD_PROGRESS.md`.
- At least once every 30 minutes, append a short entry describing what was done, what was learned, and what changed.
- Include test results and known failures, not only successes.

Whenever a build becomes usable and adds a real capability or meaningful improvement compared with the previous usable build:

- Save a snapshot under a versioned folder inside `Forimpro`, for example `dist/snapshots/hot-reload/YYYYMMDD-HHMM-description`.
- Include the exact files needed to roll back, especially `ModTheSpire2.dll`, `ModTheSpire2Launcher.exe`, `ModTheSpire2.json`, `ModTheSpire2.pck`, and `README.md` when relevant.
- Add a short `SNAPSHOT.md` explaining what works, what changed, and what risks remain.
- Do not overwrite previous snapshots.

The current confirmed uploadable version must remain available as the rollback baseline.

## Classification Rules

If automatic classification is possible, implement it.

If automatic classification is not fully reliable, use a conservative hybrid approach:

- Treat mods with DLLs, PCKs that affect menus, or unknown behavior as restart-required by default.
- Treat config-only or clearly runtime-toggleable mods as hot-startable only after verifying how the game applies them.
- Allow future metadata support such as `hot_reload`: `true` / `false` if the STS2 mod ecosystem supports or can tolerate it.
- Never label a mod as hot-startable if enabling it mid-session can leave the game in a broken or partially loaded state.

Use these initial labels during investigation:

- `hot_apply_candidate`: a mod or setting that appears safe to apply while already loaded.
- `restart_required`: any DLL/PCK mod with unknown load timing requirements.
- `unsupported_or_unstable`: mods known to target older STS2 versions or likely to freeze/crash current builds.
- `experimental_true_hot_load`: isolated tests of true DLL/PCK loading, never default user-facing behavior.

Do not use known unstable or outdated mods as proof that hot reload fails. If a mod can freeze or crash the current game version by itself, exclude it from acceptance testing.

## Test Mods

Do not rely on SpeedX, ModConfig, or DamageMeter as core test cases for this stage. Their current-version compatibility is not guaranteed, and they may freeze or crash the game independently of ModTheSpire2.

Create a tiny controlled test mod for this stage:

- Minimal metadata JSON.
- Prefer configuration-only behavior first.
- No risky gameplay patches.
- If a DLL is required, keep it tiny and make it log a harmless initialization marker.
- If a visible effect is needed, use a safe effect such as writing to its own state file or exposing a simple setting value.

Suggested test set:

- BaseLib, because it is a current common dependency.
- Quick Restart, because it depends on BaseLib.
- ModTheSpire2 itself.
- The new tiny controlled config-only test mod.
- One additional current Workshop mod only after confirming it is compatible with the current STS2 build.

## Rollback And Safety

Any hot-apply attempt must be reversible.

Before changing enabled-mod state:

- Back up the relevant settings file.
- Record the previous enabled/disabled state for every affected mod.
- Record whether the change was attempted through hot apply or restart-required flow.

If hot apply fails, partially succeeds, times out, or throws:

- Restore the previous settings state.
- Mark the attempted mod as restart-required for the current session.
- Show a clear message explaining that the mod cannot be safely applied without restart.
- Keep the game running if possible.
- Never leave dependencies in a half-enabled state.

## Acceptance Criteria

The stage is complete when:

- There is a documented answer for what "hot-startable" means in STS2.
- Stage A hot-apply behavior is clearly separated from Stage B true hot-load behavior.
- The UI shows two clear groups: hot-startable and restart-required.
- Hot-startable mods can be applied without restarting when the game supports it.
- Restart-required mods still use the existing safe restart/launcher workflow.
- Dependency constraints work in both groups.
- Failed hot-apply attempts roll back safely.
- A tiny controlled test mod exists for validating the hot-apply path.
- Manual mod load order can be edited, saved, restored, and applied during launcher startup.
- Dependency constraints are respected by manual load-order editing.
- The current launch-option workflow still works after the changes.
- The Workshop package remains clean, small, and uploadable.

## Non-Goals

- Do not promise universal hot reload for every mod.
- Do not treat old or unstable mods as reliable acceptance tests.
- Do not break the current launcher to chase hot reload.
- Do not require players to install .NET or external runtimes.
- Do not make BaseLib mandatory unless no other UI integration path is viable.

## First Practical Steps

1. Create a branch or separate working copy for this stage.
2. Preserve the current clean upload package as a rollback baseline.
3. Inspect game assemblies and existing mods to identify how mod enablement is applied.
4. Build a small proof-of-concept UI that only displays classification, without applying changes.
5. Add hot-apply behavior only after the safe mechanism is confirmed.
6. Create and test against a tiny controlled config-only mod.
7. Test dependency behavior with BaseLib and Quick Restart.
8. Exclude outdated or unstable mods from acceptance criteria unless they are first confirmed compatible with the current STS2 build.
9. Keep `GOAL_HOT_RELOAD_PROGRESS.md` updated every 30 minutes during active work.
10. Save a versioned rollback snapshot whenever a usable build makes meaningful progress.
11. Investigate and document the game's load-order storage before implementing manual order writes.
12. Add load-order editing after the storage format is confirmed and backup/rollback behavior is in place.

## Confirmed Operating Defaults

- Hot-apply timeout: 10 seconds. If an apply attempt does not clearly succeed within 10 seconds, treat it as failed and roll back.
- Automated local testing is allowed. The agent may replace files in `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2` and launch the game for testing.
- Snapshot folder names should use `YYYYMMDD-HHMM-short-description`, for example:
  - `20260621-1430-classification-ui`
  - `20260621-1605-hot-apply-poc`
- Each snapshot must include a short Markdown note, preferably `SNAPSHOT.md`, explaining what changed, what works, how to roll back, and any known risks.
