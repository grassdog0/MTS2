# Goal Prompt - Stable Launcher, Grouping, And Safe Multiplayer Assistance

## Objective

Continue ModTheSpire2 from the latest published clean launcher release. Keep the product simple and reliable: startup mod selection, saved profiles, dependency-aware order, optional grouping, and quick close/restart. Do not return to broad whole-mod hot-apply work.

The current task is not to invent a new UI direction. Treat the current Workshop build as the rollback point, keep the launcher/profile/settings.save fixes regression-protected, and refine the already-published optional grouping plus read-only multiplayer mismatch helper only from confirmed evidence.

## Current Published Baseline

Use the currently uploaded Workshop build as the stable rollback point.

- GitHub repository: `https://github.com/grassdog0/MTS2`
- GitHub branch: `cross-platform-launcher`
- GitHub push status at this checkpoint: local `cross-platform-launcher` is synced with origin.
- To confirm the exact current GitHub source commit, run `git rev-parse --short HEAD` and `git ls-remote origin refs/heads/cross-platform-launcher` inside the source export.
- Workshop item: `3747911678`
- Local clean package: `C:\Users\HZDH\Desktop\tmp\Forimpro\dist\WorkshopUpload\ModTheSpire2Content-Clean`
- Uploader content: `C:\Users\HZDH\Desktop\tmp\Forimpro\ModUploader-win-x64\ModTheSpire2Workspace\content`
- Timestamped backup package: `C:\Users\HZDH\Desktop\tmp\Forimpro\dist\TestPackages\ModTheSpire2-0.4.0-cross-platform-mismatch-20260703-113950.zip`
- Package SHA256: `94BE1C14179A713D07217149ED8A95390AECA81B6761234E3F10F62CC116D1B2`

Known package hashes for this baseline:

- `ModTheSpire2.dll`: `76B2B0101518552DDB7950F252B9F7CD38F4C4CB63C4AA389FED49F8932E9522`
- `ModTheSpire2Launcher.exe`: `3F9C5809BA2107B8156ADD208EB01F0584E91DA93057DF1951A51507B5C55066`
- `README.md`: `442348626154FF84EA7B502461E9A55B66B6F2DFAEC80ED59E3AE1B759CA6390`

If local source contains unverified experimental work, compare against this baseline before continuing. Do not package or upload hot-apply, draggable UI, force-join multiplayer bypass, auto-subscribe, or invasive UI changes unless they have been explicitly implemented, tested, and accepted.

## Baseline Behavior To Preserve

- Windows launcher remains the primary supported launcher.
- Linux/macOS scripts remain preview-level cross-platform launchers.
- Clean Workshop package remains the 7-file package:
  - `ModTheSpire2.dll`
  - `ModTheSpire2.json`
  - `ModTheSpire2.pck`
  - `ModTheSpire2Launcher.exe`
  - `ModTheSpire2Launcher.sh`
  - `ModTheSpire2Launcher.command`
  - `README.md`
- Launcher live view is `Current settings.save`.
- `Current settings.save` reads the actual current game settings order and enabled state.
- Named profiles save both enabled selections and load order.
- Profile dropdown applies selections immediately.
- The launcher has one clear `Save` button. Select an existing profile or type a new profile name, then `Save` to update/create that profile.
- Vanilla Launch disables mods for that launch only and must not erase or reorder the user's normal saved mod selection.
- Dependency validation at launch checks selected/enabled mods only.
- Dependency indentation/grouping must not change real launch order unless the user explicitly changes order.
- In-game Settings / Mod Settings button remains a simple Close and Open Launcher confirmation flow.
- Close and Open Launcher forwards the current game command line so renderer flags such as `--rendering-driver opengl3` are preserved.
- Do not start a second STS2 instance while the current game is still running.
- The in-game ModTheSpire2 button remains visible and clickable when other mod-setting UI layers, including Better Mod Menu, are present.
- `settings.save` enabled-state parsing uses exact ids and must not enable a mod by fuzzy substring match.

## Published Fixes To Preserve

The following player-reported issues have been addressed in the current published baseline and should be treated as regression-protected behavior, not as new feature work:

- Keep the button visible and clickable when Better Mod Menu is enabled.
- Do not reintroduce the previous draggable entrance control work. That path caused interaction, cleanup, and layering regressions.
- Do not replace the simple Close and Open Launcher flow with the old large management overlay.
- Keep one primary `Save` button.
- `Save` should create a new profile when the typed name does not exist.
- `Save` should update an existing profile when the typed/selected name already exists.
- Profiles must save both enabled mods and load order.
- Selecting an existing profile should apply it immediately in the launcher UI.
- `Current settings.save` is a live view and should not be overwritten as a normal named profile.
- `Default` is a normal saved preset if present; do not let it replace or hide `Current settings.save`.
- Do not delete or silently recreate `Default` without clear intent.
- Vanilla Launch should not mutate the user's normal per-mod enabled list or load order.
- If a temporary vanilla `settings.save` write is needed, preserve enough state to restore the normal modded state on the next launcher view.
- Back up `settings.save` before writing.
- Dependency missing/order checks should only block or warn for enabled/selected mods.
- Disabled mods may still show dependency metadata, but they should not block launch.
- Preserve and expand exact-id parsing from `settings.save`.
- Do not match enabled state by fuzzy substring when exact ids are available.
- Keep focused fixtures/self-tests for overlapping ids/names such as `Tenshi Hinanawi Skin` and `Hina`.

## Current Grouping State

The current published Windows launcher includes a low-risk grouping preview:

- Adds a read-only `Group` column.
- Reads Better Mod Menu `ModGroups` when present.
- Falls back to ModTheSpire2 simple categories when Better Mod Menu has no usable group data.
- Does not write Better Mod Menu files.
- Does not change `settings.save`, enabled state, profiles, dependency checks, or launch order.
- Package verifier now guards this as read-only: real Better Mod Menu files must not be written, deleted, moved, or copied.

Better Mod Menu is subscribed locally at:

```text
E:\SteamLibrary\steamapps\workshop\content\2868840\3748029698
```

It stores readable data under player mod data, for example:

```text
%APPDATA%\SlayTheSpire2\steam\<steamid>\mod_data\BetterModMenu\mod_profiles.json
```

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

This makes Better Mod Menu grouping feasible as an optional read-only import, but the local `ModGroups` object may be empty. Do not make Better Mod Menu a hard dependency.

## Grouping Refinement Direction

Implement grouping refinements only after confirming the published stability fixes above still pass. Grouping is a presentation aid, not a source of truth for launch order.

Preferred order:

1. Preserve the actual `settings.save` load order unless the user explicitly changes order in the launcher.
2. Keep dependency indentation visible where possible.
3. Read Better Mod Menu group data if present and valid.
4. Fall back to ModTheSpire2's own simple groups:
   - Dependencies / libraries
   - Gameplay content
   - UI / QoL
   - Cosmetic
   - Utility / tools
   - Unknown
5. Never write Better Mod Menu files in the first implementation.
6. If imported grouping fails, show a status message and fall back to MTS2 grouping.
7. Any new grouping UI must remain secondary to the core launcher workflow: select mods, preserve order, save profile, launch once.

## Multiplayer Assistance Direction

The original user idea had two possible behaviors:

1. Force bypass the game's mod mismatch block and join anyway.
2. Show the host's mod list, help the player subscribe/install missing Workshop mods, then restart through ModTheSpire2.

Engineering guidance:

- Treat force bypass as dangerous and not the default path.
- The current published feature is a read-only mismatch helper. It appends help text to `ModMismatch` errors when possible, writes `ModTheSpire2Data\multiplayer-mismatch-last.txt`, and exposes `Open Missing Mod Links` / `Copy Mismatch Report`.
- Prefer safe improvements:
  - detect mismatch information if exposed in logs, UI text, lobby metadata, or mod data;
  - display host vs local mod differences;
  - show missing Workshop item links;
  - offer to open missing Workshop pages or Steam URLs;
  - after the user subscribes, offer Close and Open Launcher.
- Do not silently subscribe through Steamworks unless a reliable, low-risk API path is proven.
- Do not alter multiplayer network checks until the exact check location and consequences are understood.

## Multiplayer Research Findings To Preserve

Current evidence supports a cautious helper, not force bypass.

Game XML evidence:

- `ConnectionFailureExtraInfo.missingModsOnLocal`: mods the host has and the local player is missing.
- `ConnectionFailureExtraInfo.missingModsOnHost`: mods the local player has and the host is missing.
- `ConnectionFailureReason.ModMismatch`: host/local mod mismatch.
- `ModManager.GetGameplayRelevantModNameList`: loaded gameplay-affecting mods used for multiplayer comparison.
- `ModManager.GetNonGameplayRelevantModNameList`: non-gameplay loaded mods.
- `JoinFlow.Begin(...)` can throw `ClientConnectionFailedException` on join failure.

Local mod evidence:

- `sts2-heybox-support.dll` contains strings showing patches for:
  - `ModManager.GetGameplayRelevantModNameList`
  - `JoinFlow.Begin`
  - server-side mod detection
- This proves bypass is technically possible, but also confirms bypass is invasive and should not be the default MTS2 direction.

Unknowns:

- Whether `missingModsOnLocal` / `missingModsOnHost` contain display names, mod ids, or both.
- Whether Workshop ids are available from mismatch info.
- Whether a stable public hook exists for reading mismatch details without invasive multiplayer patches.

## Multiplayer Follow-Up Tasks

1. Test the published mismatch helper with real mismatch reports when possible.
2. Confirm whether `missingModsOnLocal` / `missingModsOnHost` expose stable ids, display names, versions, or Workshop ids.
3. Improve Workshop-link mapping only when the mapping is deterministic or clearly labelled as best effort.
4. Search game/mod logs and user data for lobby mismatch messages and host mod-list serialization.
5. Inspect subscribed multiplayer-related mods already present locally, especially:
   - `RemoveMultiplayerPlayerLimit`
   - `sts2unlimited`
   - `typing`
   - `sts2-heybox-support`
6. Determine whether host mod lists include:
   - mod id
   - version
   - Workshop id / URL
   - enabled state
   - load order
7. Determine whether STS2 writes enough mismatch detail to logs or UI text that MTS2 can consume without patching game internals.
8. Document whether bypassing mismatch is safe, unsafe, or requires per-mod compatibility rules.
9. If more functionality is safe enough, keep it read-only or explicit-user-action first:
   - show local missing mods;
   - show host missing mods;
   - map names to Workshop links where possible;
   - offer Close and Open Launcher after the player changes subscriptions or profiles.

## Implementation Constraints

- All edits/builds stay inside `C:\Users\HZDH\Desktop\tmp\Forimpro`.
- The only approved external writable live test target is:
  `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2`
- Other game, Steam, Workshop, and mod folders are read-only unless explicitly permitted.
- Use English for development-facing text until behavior stabilizes.
- Use `apply_patch` for manual file edits.
- Preserve current Windows launcher behavior unless the change directly fixes a verified bug.
- Keep Linux/macOS changes script-based and preview-level unless real platform testing becomes available.

## Verification

For every usable build:

1. Rebuild the launcher or companion DLL as needed.
2. Run:
   `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive`
3. When the game is closed and the live folder is available, run the verifier without `-SkipLive`.
4. Keep a timestamped test package in `dist\TestPackages`.
5. Record changes in a short markdown note.
6. Manually test:
   - profile creation with a new name;
   - profile update with an existing name;
   - immediate profile switching;
   - `Current settings.save` reload;
   - `Default` preset still present when expected;
   - Vanilla Launch preserves normal enabled selections and order;
   - dependency validation only blocks selected mods;
   - overlapping mod ids/names do not cause false enabled state;
   - Better Mod Menu enabled and disabled;
   - Settings / Mod Settings button remains clickable with Better Mod Menu enabled;
   - Better Mod Menu grouping remains read-only and fallback groups appear when BMM data is unavailable;
   - `companion.log` includes `Startup self-tests: mismatchLinks=True resolver=True`;
   - Close and Open Launcher preserves renderer flags;
   - launcher still starts only one STS2 instance;
   - no mismatch report case is handled cleanly;
   - mismatch report case writes `ModTheSpire2Data\multiplayer-mismatch-last.txt`;
   - `Open Missing Mod Links` opens only links from the latest report;
   - `Copy Mismatch Report` copies the latest report.

## Non-Goals

- Do not reintroduce draggable in-game entrance controls.
- Do not implement broad whole-mod hot-apply.
- Do not replace the stable Settings / Mod Settings button with the old large management overlay unless explicitly requested.
- Do not make Better Mod Menu required.
- Do not force multiplayer mismatch bypass before proving exact safety and failure modes.
- Do not auto-subscribe Workshop items without explicit proof of a safe Steamworks flow and explicit user approval.
- Do not upload Workshop or push a new release candidate until manual testing confirms the package, except for documentation-only GitHub updates that clearly do not change packaged behavior.
