# Goal Prompt - Better Grouping And Multiplayer Mod Sync

## Objective

Continue ModTheSpire2 from the latest stable clean launcher direction. Keep the mod manager simple: startup selection, saved profiles, dependency-aware order, and quick close/restart. Do not return to broad hot-apply work.

This stage has two research-backed feature tracks:

1. Improve launcher grouping by optionally reading Better Mod Menu data.
2. Investigate and prototype safe multiplayer mod-list synchronization support.

## Current Baseline

Start from the build documented in:

`C:\Users\HZDH\Desktop\tmp\Forimpro\TEST_RESTORE_BUTTON_SAVE_PROFILE_20260702.md`

Important baseline behavior to preserve:

- Launcher live view is labeled `Current settings.save` and reads current `settings.save` order and enabled state.
- Vanilla launch disables the global game mod switch for that launch but preserves per-mod order and enabled states for later modded launches.
- Named profiles save both order and enabled selections.
- Profile dropdown applies selections immediately.
- Launcher has one `Save` button. Select an existing profile or type a new profile name, then `Save` to update/create that profile.
- Dependency order validation at launch checks selected mods only.
- In-game Settings / Mod Settings button uses the simple stable Close and Open Launcher confirmation flow. Do not reintroduce draggable controls or the large management overlay as the primary entry from this page.
- Better Mod Menu compatibility is important. The ModTheSpire2 button must remain clickable when Better Mod Menu is enabled.
- Cross-platform scripts remain present but are still preview-level unless separately tested.

## Better Mod Menu Findings

Better Mod Menu is subscribed locally at:

`E:\SteamLibrary\steamapps\workshop\content\2868840\3748029698`

It stores readable data under player mod data, for example:

`%APPDATA%\SlayTheSpire2\steam\<steamid>\mod_data\BetterModMenu\mod_profiles.json`

Observed fields:

- `Profiles`
- `DisabledMods`
- `CurrentProfileIndex`
- `CustomGroups`
- `ModGroups`
- `CollapsedGroups`
- `ModNameStyles`

It can also export CSV with columns:

- `Mod Id`
- `Name`
- `Version`
- `Enabled`
- `Group`
- `Workshop Link`

This means Better Mod Menu integration is feasible as a read-only optional import, but it needs real grouped data samples because the current local `ModGroups` object is empty.

## Grouping Feature Direction

Implement grouping in the launcher without making Better Mod Menu a hard dependency.

Preferred order:

1. Read Better Mod Menu group data if present and valid.
2. Fall back to ModTheSpire2's own simple groups:
   - Dependencies / libraries
   - Gameplay content
   - UI / QoL
   - Cosmetic
   - Utility / tools
   - Unknown
3. Keep dependency indentation visible inside groups where possible.
4. Never write Better Mod Menu's files in the first implementation. Read-only import only.
5. If imported grouping fails, show a status message and fall back to MTS2 grouping.

## Multiplayer Sync Feature Direction

The user idea has two possible behaviors:

1. Force bypass the game's mod mismatch block and join anyway.
2. Show the host's mod list, help the player subscribe/install missing Workshop mods, then restart through ModTheSpire2.

Engineering guidance:

- Treat force bypass as dangerous and not the default path.
- First determine where the game stores/transmits lobby host mod lists and mismatch checks.
- Prefer a safe first feature:
  - detect mismatch information if exposed in logs, UI text, lobby metadata, or mod data;
  - display host vs local mod differences;
  - show missing Workshop item links;
  - offer to open missing Workshop pages or Steam URLs;
  - after the user subscribes, offer close and restart through launcher.
- Do not silently subscribe through Steamworks unless a reliable, low-risk API path is proven.
- Do not alter multiplayer network checks until the exact check location and consequences are understood.

## Multiplayer Research Tasks

1. Search game/mod logs and user data for lobby mismatch messages and host mod-list serialization.
2. Inspect subscribed multiplayer-related mods already present locally, especially:
   - `RemoveMultiplayerPlayerLimit`
   - `sts2unlimited`
   - `typing`
   - `sts2-heybox-support`
3. Determine whether host mod lists include:
   - mod id
   - version
   - Workshop id / URL
   - enabled state
   - load order
4. Determine whether STS2 writes enough mismatch detail to logs or UI text that MTS2 can consume without patching game internals.
5. Document whether bypassing mismatch is safe, unsafe, or requires per-mod compatibility rules.

## Implementation Constraints

- All edits/builds stay inside `C:\Users\HZDH\Desktop\tmp\Forimpro`.
- The only approved external writable live test target is:
  `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2`
- Other game, Steam, Workshop, and mod folders are read-only unless explicitly permitted.
- Use English for development-facing text until behavior stabilizes.
- Preserve the clean 7-file Workshop package layout:
  - `ModTheSpire2.dll`
  - `ModTheSpire2.json`
  - `ModTheSpire2.pck`
  - `ModTheSpire2Launcher.exe`
  - `ModTheSpire2Launcher.sh`
  - `ModTheSpire2Launcher.command`
  - `README.md`

## Verification

For every usable build:

1. Rebuild the launcher or companion DLL as needed.
2. Run existing package verifier.
3. Keep a timestamped test package in `dist\TestPackages`.
4. Record changes in a short markdown note.
5. Manually test:
   - profile creation and switching;
   - vanilla launch preserving order;
   - selected-only dependency validation;
   - Better Mod Menu enabled and disabled;
   - Settings / Mod Settings button remains clickable with Better Mod Menu enabled;
   - launcher still starts only one STS2 instance.

## Non-Goals

- Do not reintroduce draggable in-game entrance controls.
- Do not implement broad hot-apply.
- Do not replace the stable Settings / Mod Settings button with the old large management overlay unless explicitly requested.
- Do not make Better Mod Menu required.
- Do not force multiplayer mismatch bypass before proving exact safety and failure modes.
