# ModTheSpire2 Clean Restart Manager Release Status

Last updated: 2026-06-21 22:20.

This document records the current state of the `GOAL_CLEAN_RESTART_MANAGER.md` work.

## Current Build

Version:

```text
0.4.0
```

Workshop clean content:

```text
dist\WorkshopUpload\ModTheSpire2Content-Clean
```

Manual test package:

```text
dist\TestPackages\ModTheSpire2-20260621-2157-clean-restart-docsync
```

Clean GitHub/source export:

```text
dist\Release\ModTheSpire2-0.4.0-CleanRestart-GitHubSource
```

Rollback snapshots:

```text
dist\snapshots\simple-manager\20260621-2150-clean-restart-ui
dist\snapshots\simple-manager\20260621-2157-clean-restart-docsync
```

## Verified By Automation

The following command passes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1
```

Latest verified result:

```text
Status         : OK
Version        : 0.4.0
CleanPackageKB : 538.69
DllSha256      : A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D
LauncherSha256 : 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606
```

The package verifier currently proves:

- Workshop upload content has exactly five files.
- Clean package, uploader content, and live test folder have matching hashes.
- Manifest id and version are correct.
- The in-game companion source contains the clean restart-management controls.
- The in-game companion source no longer exposes `Apply Hot Changes` in the clean restart UI.
- Management/restart dialogs keep scroll-safe viewport constraints.
- Mod rows retain minimum game version detail text.
- Launcher diagnostics handle dependency aliases, missing dependencies, dependency version requirements, minimum game version requirements, non-standard manifests, malformed payload-backed manifests, and runtime data exclusion.
- Launcher self-tests cover missing-dependency pruning and `load_after` / `load_before` order repair.
- Known Workshop compatibility cases such as Act4Heart, BaseLib, RitsuLib, wuwancients, QuickRestart, and ActsFromThePast remain covered by analysis/classifier checks.

The following command also passes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1
```

Latest source export result:

```text
Status    : OK
FileCount : 80
SizeMB    : 1.13
```

The source export verifier proves:

- The clean GitHub/source export exists.
- It contains required source, docs, verifier scripts, and clean Workshop upload content.
- It excludes `.git`, `bin`, `obj`, `ModTheSpire2Data`, `build-temp`, snapshots, test packages, logs, `.bak`, `.tmp`, `.obj`, and analysis files.
- Its embedded Workshop upload content remains exactly five files.
- Its README describes the clean restart workflow and does not advertise Hot-Apply as the current player-facing workflow.

The following command checks live manual-test log evidence:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CheckCleanRestartManualEvidence.ps1
```

Current result:

```text
Status       : INCOMPLETE
Missing      : CompanionLogExists, LauncherLogExists, CleanBuildLoaded, ModSettingsButtonSeen, ManagementDialogShown, TopRightCloseUsed, RestartDialogShown, RestartConfirmed, LauncherStartedByCompanion, LauncherWaitForPidUsed, LauncherDetectedGame, LauncherScannedMods, LauncherLaunchedSelected
NextAction   : Live DLL hash matches the clean build, but companion.log has not loaded it yet. Fully exit Slay the Spire 2, start it again, open ModTheSpire2 management, then rerun this script.
LiveDllSha256: A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D
NewestMarker : 
```

Interpretation:

```text
The live DLL hash matches the clean build. Old logs were backed up by Tools\PrepareCleanRestartManualTest.ps1, so the current missing companion/launcher logs are expected until the next manual game run.

The current 0.4.0-clean-restart-ui DLL still needs one real in-game test pass: launch the game, open the ModTheSpire2 management overlay, test close/restart flow, then rerun the evidence checker.
```

## Implemented Product Direction

The current build follows the clean restart-manager direction:

- Launcher remains the main mod management surface.
- Whole-mod enablement is managed before startup.
- Vanilla launch is treated as a one-time launch action.
- Enabled selections and load order are saved for normal modded launch.
- Named profiles remain part of the launcher workflow.
- In-game management focuses on mod status and `Close and Open Launcher`.
- Loaded content/DLL/PCK/UI/unknown whole-mod changes are not presented as safely unloadable in-process.
- BaseLib-style runtime configuration remains future reference only, not a hard dependency.

## Remaining Manual Verification

The following items still need in-game testing before the goal can be marked complete:

1. Start Slay the Spire 2 from Steam and confirm the companion log marker:

```text
Initialize ModTheSpire2 0.4.0-clean-restart-ui
```

2. Open Settings / General / Mod Settings and confirm `ModTheSpire2 Launcher` appears.
3. Open the overlay and confirm it shows:

```text
Enabled For This Launch
Available But Disabled
Load Order
```

4. Confirm the overlay does not show:

```text
Runtime Hot-Apply
Apply at Main Menu
Apply Hot Changes
```

5. Confirm the top-bar `Close and Open Launcher` button is visible without scrolling.
6. Confirm the top-right `X` closes the overlay.
7. Confirm the overlay remains inside the game window and scrolls at small window sizes.
8. Confirm `Close and Open Launcher` opens a confirmation dialog first, instead of immediately closing the game.
9. Confirm the confirmation dialog has `Copy Launch Option`, `Cancel`, and `Close and Open Launcher`.
10. Confirm choosing `Close and Open Launcher` closes the current game and opens the launcher only after the game process exits.
11. Confirm this flow does not leave two Slay the Spire 2 instances running.
12. Confirm the launcher still starts Vanilla and selected-mod launches correctly from the current package.
13. Confirm launcher saved selections, load order, and named profiles still behave correctly in the real UI.

After testing, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CheckCleanRestartManualEvidence.ps1
```

The goal can only be considered complete when this script reports `Status : OK` and the visual checks above also pass.

For a clean log before manual testing, first run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\PrepareCleanRestartManualTest.ps1
```

This command checks that the game/launcher are not running, confirms the live mod folder matches the clean package, backs up old `companion.log` and `launcher.log` under `ModTheSpire2Data\manual-test-log-backups`, and writes `manual-test-ready.txt` with the next steps.

This preparation was last run successfully at about 2026-06-21 22:19. It backed up the previous live logs and wrote:

```text
E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2\ModTheSpire2Data\manual-test-ready.txt
```

Use:

```text
MANUAL_TEST_0.4.0.md
```

as the detailed checklist.

## Release Readiness

Ready for user manual testing:

```text
yes
```

Ready for final Workshop/GitHub release without manual game verification:

```text
not yet
```

Reason:

```text
Automation proves package/source hygiene and launcher diagnostics, but the in-game overlay and close/reopen flow still require real game UI verification.
```
