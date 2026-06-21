# ModTheSpire2 Hot Reload Goal Progress

## 2026-06-21 Initial Setup

- Read `GOAL_NEXT_STAGE_HOT_RELOAD.md` and confirmed the working rules for this goal.
- Confirmed the current clean upload package exists at `dist/WorkshopUpload/ModTheSpire2Content-Clean`.
- Confirmed the live test folder `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2` contains the current dependency-aware launcher and copy-button DLL.
- Next steps: preserve a rollback snapshot, then inspect STS2 config files and game assemblies to determine mod enablement, load order storage, and whether hot apply / true hot load have safe API support.

## 2026-06-21 01:15:24 Load Order POC

- Inspected STS2 metadata: ModSettings.ModList is an ordered list of SettingsSaveMod entries; SettingsSaveMod contains only Id, Source, and IsEnabled.
- Confirmed ModManager exposes initialization and loaded-mod queries, but no obvious public one-mod true hot-load API. Stage B remains experimental; Stage A/hot-apply should stay conservative.
- Updated NativeLauncher/ModTheSpire2Launcher.c to compile cleanly with MSVC using English-only UI text for this optimization phase.
- Added ModTheSpire2Data/load-order.txt, Move Up / Move Down / Save Order / Reset Order controls, dependency-constrained ordering, and startup rewriting of settings.save mod_list.
- Verified diagnostics with a custom order file: RemoveMultiplayerPlayerLimit can move before other roots, while QuickRestart is automatically kept under/after BaseLib.

## 2026-06-21 01:22:38 Hot Management UI POC

- Built a new in-game DLL from ModTheSpire2Entry.HotManage.cs using Roslyn csc against the game-managed .NET/Godot/STS2 DLL set, avoiding the local net8/net9 project conflict.
- Added a ModTheSpire2 management dialog opened from the in-game mod screen button.
- The dialog now separates detected mods into Hot-Apply candidates and Restart Required with conservative classification: unknown DLL/PCK/gameplay mods remain restart-required.
- Added dialog actions: Apply Hot Changes (informational only until a safe API is verified), Open Launcher, Copy Launch Option, and Close and Open Launcher.
- Copied the new DLL to dist/WorkshopUpload/ModTheSpire2Content-Clean and the live local test folder. Snapshot saved at dist/snapshots/hot-reload/20260621-0122-hot-management-ui-poc.

## 2026-06-21 01:27:16 Hot Apply Settings POC

- Added TestMods/ModTheSpire2HotConfigTest, a tiny config-only test mod kept inside Forimpro for controlled classification tests.
- Confirmed via metadata inspection that SaveManager.Instance.SettingsSave and SaveManager.Instance.SaveSettings() are public, giving Stage A a conservative settings-save path.
- Updated the in-game management dialog: Apply Hot Changes now calls HotApplyService instead of showing only an informational message.
- HotApplyService backs up the newest settings.save, snapshots in-memory mod enabled state, applies only hot-candidate settings via SettingsSave.ModSettings.ModList, enforces the 10 second safety limit, calls SaveSettings(), and rolls back on failure.
- Rebuilt ModTheSpire2.dll, copied it to the clean Workshop content and live local test folder, and saved snapshot dist/snapshots/hot-reload/20260621-0126-hot-apply-settings-poc.

## 2026-06-21 01:31:37 Interactive Hot Apply UI

- Replaced the text-only management dialog with an interactive Godot Window containing scrollable Hot-Apply and Restart Required sections.
- Hot-Apply candidates now render as checkboxes initialized from the current game settings state.
- Restart-required mods remain read-only in a separate section.
- Added dependency hints, dependency selection enforcement for hot candidates, and conservative reclassification when a hot candidate depends on a restart-required mod.
- Updated HotApplyService.Apply to save only the user-selected hot-candidate state, auto-include hot dependencies, preserve rollback behavior, and keep true DLL/PCK hot-load unsupported.
- Rebuilt ModTheSpire2.dll, copied it to the clean Workshop content and live local test folder, and saved snapshot dist/snapshots/hot-reload/20260621-0131-interactive-hot-apply-ui.

## 2026-06-21 01:32:29 Classification Smoke Test

- Added Tools/ClassifyModsForHotApply.ps1 to mirror the conservative classification rules outside the game for quick smoke tests.
- Ran the classifier against the current game mods, workshop cache, and TestMods.
- Result: Hot-Apply candidates are ModTheSpire2 and ModTheSpire2HotConfigTest; all current DLL/PCK mods including BaseLib, QuickRestart, ModConfig, DamageMeter, SpeedX, and others are classified as restart-required.
- Clean Workshop content size is currently about 215 KB.

## 2026-06-21 01:35:55 Hot Apply Missing Entry Fix

- Inspected SettingsSaveMod and confirmed it has a public parameterless constructor plus writable Id, Source, and IsEnabled properties.
- Extended ModSummary with ModSource, so hot apply can create missing SettingsSaveMod entries with ModsDirectory or SteamWorkshop correctly.
- HotApplyService now adds missing settings entries for hot candidates before saving, and rollback removes entries created during a failed attempt.
- Tightened classification: hot candidates with missing dependencies are conservatively reclassified as restart-required.
- Updated Tools/ClassifyModsForHotApply.ps1 to mirror the missing-dependency rule.
- Rebuilt and copied ModTheSpire2.dll to clean Workshop content and the live local test folder; snapshot saved at dist/snapshots/hot-reload/20260621-0135-hot-apply-missing-entry-fix.
- Classification smoke test still reports only ModTheSpire2 and ModTheSpire2HotConfigTest as Hot-Apply candidates. Clean package is about 219 KB.

## 2026-06-21 01:39:42 Dependency Fixed-Point Classification

- Tightened hot-apply dependency classification to iterate to a fixed point, so transitive dependency chains are downgraded correctly.
- Added controlled test manifests under TestMods: ModTheSpire2HotDependsRestart and ModTheSpire2HotDependsChain.
- Updated Tools/ClassifyModsForHotApply.ps1 to mirror the fixed-point dependency downgrade logic.
- Smoke test result: only ModTheSpire2 and ModTheSpire2HotConfigTest remain Hot-Apply candidates; the two controlled dependency-chain tests are correctly Restart Required.
- Rebuilt and copied ModTheSpire2.dll to clean Workshop content and the live local test folder; snapshot saved at dist/snapshots/hot-reload/20260621-0139-dependency-fixedpoint.
- Verified matching SHA256 for clean content, live test DLL, and snapshot DLL: B96E2A73E131AA8C9DCA98EA3BDFFE0614B838B6B2DDF2E96591E267E6C7C829.

## 2026-06-21 01:48:00 Portable Hot Manage Package

- User clarified that package size is not the priority for this phase; functionality and stability come first.
- Reconfirmed clean Workshop content and live local test folder were synchronized before editing.
- Removed the companion DLL's local-machine fallback to `E:\SteamLibrary\steamapps\common\Slay the Spire 2`; mod scanning now discovers `steamapps` from the current mod folder and derives the game/workshop paths from there.
- Rebuilt ModTheSpire2.dll with Roslyn csc against the game-managed assemblies and copied it to both clean Workshop content and the live test mod folder.
- Updated clean package README and manifest version to `0.3.0`, documenting dependency grouping, manual load-order controls, hot-apply limits, and backup behavior.
- Ran launcher diagnostics from `dist/WorkshopUpload/ModTheSpire2Content-Clean`: detected game directory, newest `settings.save`, local mods, Workshop mods, and 9 total mods.
- Diagnostic dependency ordering remains correct: `BaseLib` appears as a root and `QuickRestart` appears indented under it.
- Re-ran `Tools/ClassifyModsForHotApply.ps1`: only `ModTheSpire2` and `ModTheSpire2HotConfigTest` are hot-apply candidates; DLL/PCK and dependency-chain tests remain restart-required.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0148-portable-hot-manage`.

## 2026-06-21 01:58:00 Optional BaseLib Hot Management Entry

- Inspected STS2 and BaseLib metadata for native UI/control options: `NModMenuButton`, `NSettingsButton`, `NConfirmButton`, `NTickbox`, `NVerticalPopup`, `NModalContainer`, `BaseLib.Config.SimpleModConfig`, and `BaseLib.Config.UI.NModListButton`.
- Added optional BaseLib ModConfig integration to the current `ModTheSpire2Entry.HotManage.cs` path instead of switching back to the older `ModTheSpire2Entry.cs` file.
- The integration uses reflection and dynamic type generation, so BaseLib remains optional and ModTheSpire2 does not gain a hard dependency.
- When BaseLib is present, ModTheSpire2 tries to register a `SimpleModConfig` page with `Open Management` and `Copy Launch Option` actions.
- Added a BaseLib ModConfig submenu fallback entry using `NModListButton` when available; it opens the same hot-management dialog.
- Rebuilt the DLL and synchronized it to clean Workshop content, uploader content, and the live local test folder. Initial DLL SHA256: `672D130B826382AC5E34C03F036F8029F74CE7D5256D0B7FB8F9C38EA3D45E23`.
- Re-ran hot-apply classification; results remain conservative and unchanged.
- Re-ran launcher diagnostics; detected 9 mods and preserved `BaseLib -> QuickRestart` dependency grouping.
- Removed generated `ModTheSpire2Data` runtime logs from clean Workshop content after diagnostics.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0158-baselib-hotmanage-entry`.
- Follow-up safety fix: changed dynamically generated BaseLib config button methods to no-argument methods to better match reflection-based button invocation. Rebuilt and synchronized the DLL again. Current DLL SHA256: `9C6E46F67072472626F45904431746DB1D7AD67D0A2CBF2E6EA05B494FDC4C30`.
- Attempted live verification through `steam://rungameid/2868840`, but Steam did not start a SlayTheSpire2 process in this environment.
- Attempted direct exe launch for diagnostics only; it opened a window but did not initialize Steamworks because no appID was provided, so mod initialization did not run. No game files were modified to force this path.

## 2026-06-21 02:06:00 In-Game Overlay Management UI

- Improved the HotManage UI without changing the launcher startup flow.
- Replaced the standalone Godot `Window` management dialog with a full-screen in-game modal overlay using a translucent backstop and centered `PanelContainer`.
- Added an explicit title, concise hot-apply warning, styled section header panels, and a Close button.
- Kept the same functional actions: Apply Hot Changes, Open Launcher, Copy Launch Option, and Close and Open Launcher.
- Rebuilt and synchronized the DLL to clean Workshop content, uploader content, and the live local test folder. Current DLL SHA256: `5EC7CE3DBEC6778227012FB31D7B2301D1D5AE7C3E3ED214F46959E30D39E5E2`.
- Hot-apply classification smoke test remains unchanged and conservative.
- Launcher diagnostics still detect 9 mods and preserve `BaseLib -> QuickRestart` dependency grouping.
- Removed generated diagnostic runtime logs from clean Workshop content after testing.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0206-ingame-overlay-ui`.
- In-game visual verification remains pending for the same environment reason: Steam URI did not start the game, and direct exe launch cannot initialize Steamworks without appID.

## 2026-06-21 02:10:00 In-Game Load Order Controls

- Added a `Load Order` section to the in-game HotManage overlay.
- The list shows enabled mods in current settings order unless `ModTheSpire2Data/load-order.txt` already exists, in which case the saved ModTheSpire2 order is used.
- Added Move Up, Move Down, Save Order, and Reset Order controls.
- Save Order writes one mod id per line to `ModTheSpire2Data/load-order.txt`, matching the native launcher's existing parser.
- Reset Order deletes the custom order file and repopulates the list from current game settings order.
- Move operations enforce dependency constraints, so dependencies cannot be moved below dependents and dependents cannot be moved above dependencies.
- The in-game UI does not directly rewrite `settings.save`; applying the saved order remains the launcher's job, preserving the existing backup/write flow.
- Rebuilt and synchronized the DLL to clean Workshop content, uploader content, and live local test folder. Current DLL SHA256: `4DE69882E5BFE68DFA4004C38384D46A15521D92AD0A65D51EE344BA482A7AD3`.
- Hot-apply classification smoke test remains unchanged and conservative.
- Launcher diagnostics still detect 9 mods and preserve `BaseLib -> QuickRestart` dependency grouping.
- Removed generated diagnostic runtime logs from clean Workshop content after testing.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0210-ingame-load-order-ui`.

## 2026-06-21 02:14:00 Version 0.4.0 Testability And Docs

- Added a companion DLL build marker: `Initialize ModTheSpire2 0.4.0-hot-order-ui`, so manual game testing can confirm the exact DLL loaded.
- Wrapped the Load Order UI section in an isolated error handler; if the load-order controls fail, the rest of the management overlay should still open.
- Updated `ModTheSpire2.json` to version `0.4.0`.
- Updated README to document in-game load-order controls and `ModTheSpire2Data/load-order.txt`.
- Updated Workshop metadata change note to describe 0.4.0.
- Rebuilt and synchronized clean Workshop content, uploader content, and live local test folder. Current DLL SHA256: `656051EF9C62BFF6739C606C100E5073D478B79E11996AE198F6B6485975C49A`.
- Hot-apply classification smoke test remains unchanged and conservative.
- Launcher diagnostics still detect 9 mods and preserve `BaseLib -> QuickRestart` dependency grouping.
- Removed generated diagnostic runtime logs from clean Workshop content after testing.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0214-v040-testability-docs`.

## 2026-06-21 02:17:00 Overlay Close Controls And Manual Test Checklist

- Added close interactions to the in-game management overlay: Close button, dark backstop click, and Esc key.
- Each close path writes a companion log line: `Management dialog closed: button`, `Management dialog closed: backstop`, or `Management dialog closed: escape`.
- The overlay requests focus after opening to improve Esc handling.
- Added `MANUAL_TEST_0.4.0.md` with a structured Steam-launch manual test checklist covering startup marker, mod settings button, overlay sections, close paths, hot apply, load order, BaseLib ModConfig entry, and launcher regression.
- Rebuilt and synchronized clean Workshop content, uploader content, and live local test folder. Current DLL SHA256: `298B0ADB34B783404584D861EF373A5897074D45A9CE68E02305F89DA50EE66E`.
- Hot-apply classification smoke test remains unchanged and conservative.
- Launcher diagnostics still detect 9 mods and preserve `BaseLib -> QuickRestart` dependency grouping.
- Removed generated diagnostic runtime logs from clean Workshop content after testing.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0217-overlay-close-manual-test`.

## 2026-06-21 02:21:00 Responsive Overlay Layout

- Improved the in-game management overlay layout for smaller windows and different viewport sizes.
- The panel now derives its size from the current viewport instead of always using `960x690`.
- The panel stays within the viewport with margin, and the scroll area height is calculated from available panel height.
- Replaced the fixed action button `HBoxContainer` with `FlowContainer` so buttons can wrap when the overlay is narrow.
- Launcher behavior and hot-apply logic were not changed.
- Rebuilt and synchronized clean Workshop content, uploader content, and live local test folder. Current DLL SHA256: `9C0A08B80B354F48895ECC5934223CCC6A5391DC90D0780EBDD17D90D44B55B0`.
- Hot-apply classification smoke test remains unchanged and conservative.
- Launcher diagnostics still detect 9 mods and preserve `BaseLib -> QuickRestart` dependency grouping.
- Removed generated diagnostic runtime logs from clean Workshop content after testing.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0221-responsive-overlay`.

## 2026-06-21 02:24:00 Overlay Singleton And Logs

- Added duplicate-open protection for the in-game management overlay.
- If the overlay is already open, opening it again focuses the existing overlay instead of stacking another full-screen layer.
- Duplicate-open handling logs `Management dialog already open; focused existing overlay`.
- Successful Load Order section creation now logs `Load order section shown entries=<count>`.
- Updated `MANUAL_TEST_0.4.0.md` to ask testers to verify the duplicate-open and Load Order log lines.
- Rebuilt and synchronized clean Workshop content, uploader content, and live local test folder. Current DLL SHA256: `80A8A27CB5544570924658CEB8D9AC5AE8CF6DABD6BC1DE6AAE1E3887E3FFB03`.
- Hot-apply classification smoke test remains unchanged and conservative.
- Launcher diagnostics still detect 9 mods and preserve `BaseLib -> QuickRestart` dependency grouping.
- Removed generated diagnostic runtime logs from clean Workshop content after testing.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0224-overlay-singleton-logs`.

## 2026-06-21 02:27:00 Fluid Inner Overlay Controls

- Removed fixed-width controls inside the in-game management overlay that could still overflow after the outer panel became responsive.
- Section headers now expand to parent width instead of using fixed `880/850` widths.
- Load-order fallback text, item list, and status label now use parent width with height-only minimums.
- The Load Order action row now uses `FlowContainer`, so Move/Save/Reset controls can wrap on narrow panels.
- Rebuilt and synchronized clean Workshop content, uploader content, and live local test folder. Current DLL SHA256: `C236E3DC1ABB3D77B8928F6BB8BFD5B3F1E1D82B693CDDCEE720707FF542B6E9`.
- Hot-apply classification smoke test remains unchanged and conservative.
- Launcher diagnostics still detect 9 mods and preserve `BaseLib -> QuickRestart` dependency grouping.
- Removed generated diagnostic runtime logs from clean Workshop content after testing.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0227-fluid-inner-overlay`.

## 2026-06-21 02:31:00 Package Verification Script Snapshot

- Added and verified `Tools/VerifyModTheSpire2Package.ps1` as the package gate for 0.4.0.
- The verifier checks the clean Workshop package, uploader content, and live local test folder for matching expected files and hashes.
- The verifier also runs the hot-apply classifier and launcher diagnostics, then removes generated runtime logs from the clean package.
- Verification result: `Status=OK`, `Version=0.4.0`, clean package size `237.09 KB`, DLL SHA256 `C236E3DC1ABB3D77B8928F6BB8BFD5B3F1E1D82B693CDDCEE720707FF542B6E9`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0231-package-verify-script`.
- Remaining blocker: actual in-game visual/click verification still requires launching through Steam manually.

## 2026-06-21 02:38:00 Load Order Verifier

- Updated `Tools/VerifyModTheSpire2Package.ps1` to verify manual launcher load-order support.
- The verifier now writes a temporary clean-package `ModTheSpire2Data/load-order.txt` containing `ModTheSpire2`, `BaseLib`, and `QuickRestart`.
- Launcher diagnostics must report `Loaded custom order entries=3` and `Diagnostic mod[0]=ModTheSpire2 id=ModTheSpire2`, proving the saved order is read by the launcher.
- The existing dependency grouping check remains in place, so `BaseLib` and `QuickRestart` must still be detected.
- Verification result: `Status=OK`, `Version=0.4.0`, clean package size `237.09 KB`, DLL SHA256 `C236E3DC1ABB3D77B8928F6BB8BFD5B3F1E1D82B693CDDCEE720707FF542B6E9`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0238-load-order-verifier`.
- The package binaries did not change in this step; this is a verification and rollback-safety improvement.

## 2026-06-21 02:43:00 Classification Verifier

- Strengthened `Tools/VerifyModTheSpire2Package.ps1` to check hot-apply classification, not just package hashes.
- The verifier now requires `ModTheSpire2HotConfigTest` to appear as a Hot-Apply candidate.
- The verifier now requires `ModTheSpire2HotDependsRestart` to be downgraded because it depends on restart-required `BaseLib`.
- The verifier now requires `ModTheSpire2HotDependsChain` to be downgraded through the transitive dependency chain.
- Re-ran the full verifier: `Status=OK`, `Version=0.4.0`, clean package size `237.09 KB`, DLL SHA256 `C236E3DC1ABB3D77B8928F6BB8BFD5B3F1E1D82B693CDDCEE720707FF542B6E9`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0243-classification-verifier`.
- Package binaries are still unchanged; this step improves automated regression protection for the requested hot-start and dependency behavior.

## 2026-06-21 02:52:00 Styled Management Overlay

- Rebuilt `ModTheSpire2.dll` with a first UI-polish pass for the in-game management overlay.
- Added shared dark brown/gold style constants for the overlay, panel, section headers, rows, text, lists, and buttons.
- Converted Hot-Apply candidates to framed checkbox rows and Restart Required mods to framed text rows.
- Styled the Load Order list and action buttons to match the same overlay palette.
- Launcher executable and startup flow were not changed.
- Rebuilt with Roslyn csc against the game-managed assemblies and synchronized the DLL to clean Workshop content, uploader content, and the live local test folder.
- Full verifier result: `Status=OK`, `Version=0.4.0`, clean package size `240.09 KB`, DLL SHA256 `40C16837AAFCB56C7D6C557E62CD4649433B61AF2AE6E925958834CF3F813DFA`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0252-styled-management-overlay`.
- Updated `MANUAL_TEST_0.4.0.md` with overlay style checks for manual Steam-launch testing.
- Remaining blocker: actual visual/click verification still requires launching the game through Steam manually.

## 2026-06-21 03:02:00 Modding Screen Entry Style

- Improved the ModTheSpire2 button injected into the game's Mod Settings screen.
- The injected button now uses the same dark/gold styled button treatment as the in-game ModTheSpire2 management overlay.
- Centralized overlay/button/list style helpers into `UiStyle` inside `ModTheSpire2Entry.HotManage.cs`, reducing duplicate styling code.
- Launcher executable and startup flow were not changed.
- Rebuilt with Roslyn csc against the game-managed assemblies and synchronized the DLL to clean Workshop content, uploader content, and the live local test folder.
- Full verifier result: `Status=OK`, `Version=0.4.0`, clean package size `239.59 KB`, DLL SHA256 `0C3F0A51C10E36F23BF1A166210CBE73FCC958A43A9A4B5603841511366FEC81`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0302-modding-screen-entry-style`.
- Updated `MANUAL_TEST_0.4.0.md` to ask testers to verify the styled Mod Settings entry button.
- Remaining blocker: actual visual/click verification still requires launching the game through Steam manually.

## 2026-06-21 03:12:00 Styled Restart Dialog

- Replaced the default Godot restart confirmation dialog with a custom styled in-game overlay.
- The styled restart dialog includes launch-option instructions, a framed launch-option text box, `Copy Launch Option`, `Close and Open Launcher`, and `Cancel` buttons.
- Added duplicate-open protection for the restart dialog; repeated opens focus the existing overlay instead of stacking.
- Added close paths through Cancel, Esc, and dark backstop click.
- Kept native message-box fallback if the styled dialog fails.
- Launcher executable and startup flow were not changed.
- Rebuilt with Roslyn csc against the game-managed assemblies and synchronized the DLL to clean Workshop content, uploader content, and the live local test folder.
- Full verifier result: `Status=OK`, `Version=0.4.0`, clean package size `242.09 KB`, DLL SHA256 `1FC2130C9B4151E9FCEFCEA8006FB3A283EA5E6182D7B1CDF7974EFD2BA0E2EC`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0312-styled-restart-dialog`.
- Updated `MANUAL_TEST_0.4.0.md` with restart-dialog style and button checks.
- Remaining blocker: actual visual/click verification still requires launching the game through Steam manually.

## 2026-06-21 03:22:00 Confirm Flow And Config Style

- Routed the management overlay's `Close and Open Launcher` button through the styled restart confirmation dialog instead of immediately restarting.
- Styled the BaseLib ModConfig fallback button with the shared ModTheSpire2 dark/gold button style.
- Styled the BaseLib ModConfig custom page buttons with the same shared style.
- The BaseLib integration remains optional and reflection-based; BaseLib is still not a hard dependency.
- Launcher executable and startup flow were not changed.
- Rebuilt with Roslyn csc against the game-managed assemblies and synchronized the DLL to clean Workshop content, uploader content, and the live local test folder.
- Full verifier result: `Status=OK`, `Version=0.4.0`, clean package size `242.09 KB`, DLL SHA256 `37B9C78B25C9779B102D2AB52B35A80099C4DFFA12322E413B3638FA6ADF4D29`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0322-confirm-flow-and-config-style`.
- Updated `MANUAL_TEST_0.4.0.md` to verify confirmation-before-restart behavior and BaseLib button styling.
- Remaining blocker: actual visual/click verification still requires launching the game through Steam manually.

## 2026-06-21 03:32:00 Session Hot-Apply Downgrade

- Added an in-memory session safety downgrade for failed or unavailable hot-apply attempts.
- When hot apply fails after rollback, attempted hot candidates and their hot dependencies are marked Restart Required for the current game session.
- When the settings manager is unavailable, attempted hot candidates are also marked Restart Required for the current game session.
- Reopening the management overlay will show these downgraded mods in Restart Required with `hot apply failed this session; restart required` or `hot apply unavailable this session; restart required` as the reason.
- The downgrade is not persisted; restarting the game clears it and classification runs again from manifests/dependencies.
- Updated `HOT_APPLY_FINDINGS.md` to document the session downgrade behavior.
- Launcher executable and startup flow were not changed.
- Rebuilt with Roslyn csc against the game-managed assemblies and synchronized the DLL to clean Workshop content, uploader content, and the live local test folder.
- Full verifier result: `Status=OK`, `Version=0.4.0`, clean package size `243.09 KB`, DLL SHA256 `503BCB4228CFC7ECF3922AB39DA1E035F4AFA1D39AB5DB0B9A3D5DA29D083670`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0332-session-hotapply-downgrade`.
- Updated `MANUAL_TEST_0.4.0.md` with hot-apply failure downgrade checks.
- Remaining blocker: actual visual/click and failure-path verification still requires launching the game through Steam manually.

## 2026-06-21 03:42:00 Hot-Apply Downgrade Auto Refresh

- Changed `HotApplyService.Apply` to return a result enum instead of only showing messages.
- When hot apply fails or is unavailable and triggers a session downgrade, the management overlay now closes and immediately reopens.
- The refreshed overlay shows affected mods under Restart Required right away with the session downgrade reason.
- Successful hot apply behavior is unchanged.
- Launcher executable and startup flow were not changed.
- Rebuilt with Roslyn csc against the game-managed assemblies and synchronized the DLL to clean Workshop content, uploader content, and the live local test folder.
- Full verifier result: `Status=OK`, `Version=0.4.0`, clean package size `243.59 KB`, DLL SHA256 `0C7B517C7E3C715076F2FE7F4EC33AF2749E5BC380C75C2AC4C3CE1DE7086310`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0342-hotapply-downgrade-auto-refresh`.
- Updated `MANUAL_TEST_0.4.0.md` so the failure-path check expects automatic overlay refresh.
- Remaining blocker: actual visual/click and failure-path verification still requires launching the game through Steam manually.

## 2026-06-21 08:48:00 Launcher Move Up Crash Fix

- Investigated user report: selecting an independent mod and clicking `Move Up` closed the launcher window.
- Root cause found in `NativeLauncher/ModTheSpire2Launcher.c`: `RebuildListPreservingChecks` allocated `WCHAR checked[MAX_MODS][MAX_TEXT]` on the stack, roughly 1 MB, which can crash the native Win32 launcher during Move Up/Down list rebuilds.
- Moved the temporary checked-id buffer to heap allocation with `HeapAlloc`/`HeapFree`; Move Up/Down logic itself is unchanged.
- Rebuilt `ModTheSpire2Launcher.exe` with MinGW native Win32 compiler.
- Synchronized the rebuilt launcher to clean Workshop content, uploader content, and the live local test folder.
- Full verifier result: `Status=OK`, `Version=0.4.0`, clean package size `439.37 KB`, DLL SHA256 `0C7B517C7E3C715076F2FE7F4EC33AF2749E5BC380C75C2AC4C3CE1DE7086310`, launcher SHA256 `D6E78F9A84B99FAEE153EF1A2436FC9C5FFBF5D462CEE1BFFCAF52B17D93EBDA`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-0848-launcher-move-stack-fix`.
- Test package created at `dist/TestPackages/ModTheSpire2-20260621-0848-launcher-move-stack-fix.zip`.
- Remaining manual test: run the launcher, select an independent mod, click Move Up/Down several times, and confirm the window stays open and order changes normally.

## 2026-06-21 09:08
- Fixed launcher order UI crash risk by keeping the Win32 ListView single-select and clearing old selection before programmatic selection changes.
- Added numeric order application support in shared move logic and kept Save/Start dependency validation.
- Added named order profile save/load support in the launcher data folder.
- Added hidden ListView order self-test for boundary Move Up/Move Down and independent mod movement.
- Added launcher order self-test to Tools\VerifyModTheSpire2Package.ps1.
- Synced the verified launcher to clean package, uploader content, and the live ModTheSpire2 test folder.
- Verified package gate: Status OK, version 0.4.0, clean package 453.8 KB.


## 2026-06-21 09:38
- Fixed launcher checkbox preservation during load-order moves by reading checked IDs from the ListView ID column before rebuilding rows.
- Extended launcher hidden self-test to check that a checked mod remains checked after Move Up reorders it.
- Removed standalone Open Launcher from the management overlay and changed the main-menu launcher entry to use the safe Close-and-Open confirmation flow.
- Removed duplicate Copy Launch Option buttons from the management overlay and BaseLib ModConfig page; copy remains in the close/restart confirmation dialog.
- Hot-apply success now attempts to refresh the native modding screen and reopens the ModTheSpire2 management overlay so its checkbox state is synchronized.
- Rebuilt ModTheSpire2.dll with Roslyn csc single-file HotManage build and rebuilt the native launcher.
- Verified package gate: Status OK, version 0.4.0, clean package 453.8 KB, DLL SHA256 8E4380D9B1BF133265B1BE0C37CFBDC379F476729040AC43D5D3638A2F80168B.


## 2026-06-21 09:57
- Corrected hot-apply classification: ModTheSpire2 is now Restart Required because it ships DLL/Harmony UI patches and cannot be unloaded in-session.
- Updated classifier and verifier expectations so only the controlled config test remains a hot-apply candidate.
- Added launcher enabled-mod persistence at ModTheSpire2Data/enabled-mods.txt.
- Save Order now saves both load-order.txt and enabled-mods.txt; Launch Selected also saves enabled selections before starting.
- Vanilla launch no longer saves enabled-mods.txt, so it is a one-time vanilla start and does not clear the next launcher default selections.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe and synchronized clean/uploader content. Live DLL is currently locked by running SlayTheSpire2.exe and remains pending live sync.
- Verified clean/uploader package with -SkipLive: Status OK, version 0.4.0, clean package 455.93 KB, DLL SHA256 F4D4B153AADFE7234210EDAF06EBA103841C01860546961FBE91FF37F8D4A600.


## 2026-06-21 10:30
- Investigated missing Workshop mod Act 4 Heart / Act4Heart.
- Root cause: the mod only ships mod_manifest.json, while the launcher and companion scanner excluded mod_manifest.json files.
- Changed NativeLauncher, in-game ModScanner, and Tools/ClassifyModsForHotApply.ps1 to accept any JSON manifest with a valid id, including mod_manifest.json and mod_mainfest.json.
- Verified launcher diagnostics now detects Act 4 Heart [Act4Heart] from Workshop item 3747537811.
- Verified classifier marks Act4Heart as Restart Required because it has DLL/PCK startup behavior.
- Added verifier coverage so Act4Heart detection from mod_manifest.json is checked.
- Rebuilt DLL/launcher, synchronized clean/uploader/live content, and full package verification passed.
- Rebuilt Workshop and GitHub release zips with the compatibility fix.

## 2026-06-21 10:55
- Updated README and Workshop metadata links to the public repository: https://github.com/grassdog0/MTS2.
- Rebuilt clean release copies:
  - dist/Release/ModTheSpire2-0.4.0-WorkshopContent
  - dist/Release/ModTheSpire2-0.4.0-GitHubSource
- Rebuilt release zips:
  - dist/Release/ModTheSpire2-0.4.0-WorkshopContent.zip
  - dist/Release/ModTheSpire2-0.4.0-GitHubSource.zip
- Full verifier result: Status OK, version 0.4.0, clean package 457.7 KB, DLL SHA256 0DB05DBA7C1790D75F2D0E52138DA6A95F95BA7217947D9D9C27D6E448E2C81D.
- Created local GitHub repository commit in the clean source export: 8ed617d Release ModTheSpire2 0.4.0.
- GitHub push is blocked by local network connectivity: github.com resolves and pings, but TCP 443 times out for git and curl. Retry from a network that can reach GitHub HTTPS.
- Later retry reached GitHub. Remote contained only the initial README commit, so the clean source export merged it with `--allow-unrelated-histories` while keeping the full ModTheSpire2 README.
- Pushed successfully to https://github.com/grassdog0/MTS2 at merge commit f76964d.
- Uploaded the same 0.4.0 content to Steam Workshop item 3747911678 with ModUploader. Upload completed successfully as Steam user grass_dog, with 468570 bytes processed and no dependency changes.

## 2026-06-21 12:13

- Re-read `GOAL_NEXT_STAGE_HOT_RELOAD.md`, current HotManage source, classifier, verifier, and baseline notes before changing code.
- Established the current 0.4.0 package gate before edits: verifier passed with clean package `457.7 KB` and DLL SHA256 `0DB05DBA7C1790D75F2D0E52138DA6A95F95BA7217947D9D9C27D6E448E2C81D`.
- Confirmed STS2 public state signals through metadata inspection: `NGame.Instance.MainMenu`, `NGame.Instance.CurrentRunNode`, `RunManager.Instance.IsInProgress`, `SaveManager.Instance.HasRunSave`, and `HasMultiplayerRunSave`.
- Added a first state-aware Hot-Apply classification pass in `ModTheSpire2Entry.HotManage.cs`.
- Added an in-game state label to the ModTheSpire2 overlay, reporting main menu, active run, unfinished run, and whether signals were reliable.
- Added `HotApplyScope` to the in-game classifier while preserving the existing `IsHotCandidate` surface used by the UI and hot-apply service.
- RitsuLib / `STS2-RitsuLib`, BaseLib, and RitsuLib alias are now treated as framework DLL roots and remain Restart Required by default.
- `wuwancients` is recognized as a main-menu/future-run content example, but only becomes Hot-Apply when state signals show a safe main menu with no unfinished run and the mod is already enabled/loaded.
- Unloaded DLL/PCK content mods still require launcher restart; this avoids pretending ModTheSpire2 can true-load a newly enabled DLL/PCK.
- Main-menu content candidates may use an already enabled framework dependency such as BaseLib or RitsuLib, but arbitrary restart-required dependencies still downgrade the candidate.
- Updated `Tools/ClassifyModsForHotApply.ps1` with `-AssumeSafeMainMenuNoRun` to simulate the safe-menu state for offline verification.
- Updated `Tools/VerifyModTheSpire2Package.ps1` to require RitsuLib restart classification and wuwancients state-aware classification.
- Updated `HOT_APPLY_FINDINGS.md` to remove the stale claim that ModTheSpire2 itself is hot-applicable and to document the state-aware signals.
- Rebuilt the companion DLL to `dist/build-temp/ModTheSpire2.dll` and synchronized it to clean Workshop content and uploader content.
- Adjusted the in-game scan path so the overlay state label and Hot-Apply classification use the same single `GameSessionState` snapshot.
- Live test sync is pending because `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2\ModTheSpire2.dll` is locked by a running game process; no forced close was attempted.
- Verification with `-SkipLive` passed: Status OK, version `0.4.0`, clean package `466.7 KB`, DLL SHA256 `21ECDD1152DDF1B6344E1A3615F1DB4F91F560DF4D3966A45EEC862D10EFB72E`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-1218-state-aware-hotapply-v1`.

## 2026-06-21 12:22

- Confirmed the game process was still running, so the live test DLL remained locked and was not forcibly replaced.
- Tightened `Tools/ClassifyModsForHotApply.ps1` so `-AssumeSafeMainMenuNoRun` alone does not promote unloaded DLL/PCK content mods.
- Added `-AssumeLoadedIds` to the offline classifier. It accepts both comma-separated and array-style input.
- Updated the package verifier to check both cases:
  - Safe main menu but unloaded `wuwancients` remains Restart Required with `main menu only; mod is not loaded, start through launcher to enable`.
  - Safe main menu plus simulated loaded `wuwancients,BaseLib` promotes `wuwancients` with `future-run content toggle; main menu safe`.
- Re-ran `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: Status OK, version `0.4.0`, clean package `466.7 KB`, DLL SHA256 `21ECDD1152DDF1B6344E1A3615F1DB4F91F560DF4D3966A45EEC862D10EFB72E`.

## 2026-06-21 12:26

- Game process was still running, so live DLL sync remained pending; no forced close was attempted.
- Improved the in-game management overlay classification layout.
- Main-menu-only content candidates that are currently blocked by state are now shown in a separate `Main Menu Only / Currently Blocked` section instead of being mixed into permanent `Restart Required`.
- The Hot-Apply service input remains unchanged: only actual `IsHotCandidate` rows can be applied.
- Rebuilt the companion DLL and synchronized clean Workshop content plus uploader content.
- Re-ran `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: Status OK, version `0.4.0`, clean package `467.2 KB`, DLL SHA256 `EF267D6EDB9CE1C16E6F63E92E835A103EA56A99689C0A0E0B362B0CD619A3C4`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-1226-main-menu-blocked-section`.

## 2026-06-21 12:29

- Game process was still running, so live DLL sync remained pending.
- Added controlled test manifest `TestMods/ModTheSpire2FutureRunContentTest/ModTheSpire2FutureRunContentTest.json`.
- The controlled manifest uses `hot_apply_scope: main_menu_no_run` to verify the metadata path for future-run content without relying only on hardcoded `wuwancients`.
- Added a small README for the controlled test mod explaining expected classifier behavior.
- Updated `Tools\VerifyModTheSpire2Package.ps1` to require:
  - default state blocks the controlled future-run content test;
  - safe menu but unloaded state still blocks it;
  - safe menu plus simulated loaded state promotes it to Hot-Apply.
- Re-ran `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: Status OK, version `0.4.0`, clean package `467.2 KB`, DLL SHA256 `EF267D6EDB9CE1C16E6F63E92E835A103EA56A99689C0A0E0B362B0CD619A3C4`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-1229-future-run-content-test`.

## 2026-06-21 12:33

- Game process was still running, so live DLL sync remained pending.
- Added a Hot-Apply dependency safety guard before writing settings.
- If a selected hot-apply operation would disable a currently enabled hot candidate that another enabled mod still depends on, the operation is blocked before `settings.save` is modified.
- The guard uses the same discovered mod graph from the currently open overlay, avoiding a second state scan during Apply.
- Updated `HOT_APPLY_FINDINGS.md` and `MANUAL_TEST_0.4.0.md` with the dependency-safety behavior.
- Rebuilt the companion DLL and synchronized clean Workshop content plus uploader content.
- Re-ran `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: Status OK, version `0.4.0`, clean package `469.2 KB`, DLL SHA256 `2DE835A4959A0AFB797E90536349D19E1A06AFB100989524AFE519CA955C7257`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-1233-hotapply-dependent-block`.

## 2026-06-21 12:42

- Game process was still running, so live DLL sync remained pending.
- Inspected current Workshop/local/TestMods manifests for dependency-field patterns. Installed real Workshop samples currently mostly use `dependencies`, including object entries with `id`.
- Added controlled dependency alias manifests:
  - `TestMods/ModTheSpire2RequiresAliasTest/ModTheSpire2RequiresAliasTest.json`
  - `TestMods/ModTheSpire2RequiredModsAliasTest/ModTheSpire2RequiredModsAliasTest.json`
- Extended in-game scanner dependency parsing to read `dependencies`, `requires`, `required_mods`, and `requiredMods`.
- Extended dependency object parsing to read `id`, `mod_id`, and `modId`.
- Mirrored the same alias parsing in `Tools\ClassifyModsForHotApply.ps1`.
- Mirrored the same alias parsing in `NativeLauncher\ModTheSpire2Launcher.c`, then rebuilt the native launcher.
- Updated the verifier to require alias manifests to be downgraded through their BaseLib dependency.
- Updated `MANUAL_TEST_0.4.0.md` with dependency alias verifier coverage.
- Rebuilt companion DLL and native launcher, then synchronized clean Workshop content plus uploader content.
- Re-ran `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: Status OK, version `0.4.0`, clean package `470.2 KB`, DLL SHA256 `4903B1D7EA915DA8E1FAA2566827AC85B37EEE3C8B73BF84A9C8405DC259EF10`, launcher SHA256 `E784CB7F38BB7AE3668B71E2DEE9B9927DF7339750F8D54F2DC5F37A9E33FAC0`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-1242-dependency-alias-compat`.

## 2026-06-21 12:47

- Strengthened `Tools\VerifyModTheSpire2Package.ps1` with a temporary fake game directory inside the clean package runtime data area.
- The fake launcher diagnostics fixture verifies native launcher parsing for:
  - `requires: ["BaseLib"]`
  - `requiredMods: [{ "modId": "BaseLib" }]`
- Fixed verifier cleanup so the temporary alias diagnostics fixture and `ModTheSpire2Data/launcher.log` are removed after the check.
- Re-ran `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: Status OK, version `0.4.0`, clean package `470.2 KB`, DLL SHA256 `4903B1D7EA915DA8E1FAA2566827AC85B37EEE3C8B73BF84A9C8405DC259EF10`.
- Confirmed clean Workshop content returned to exactly five uploadable files and no `ModTheSpire2Data` directory remained.

## 2026-06-21 12:48

- Confirmed the game process had exited.
- Synchronized the verified clean Workshop content to the live test folder:
  `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2`.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`.
- Full verifier result: Status OK, version `0.4.0`, clean package `470.2 KB`, DLL SHA256 `4903B1D7EA915DA8E1FAA2566827AC85B37EEE3C8B73BF84A9C8405DC259EF10`.

## 2026-06-21 12:52

- Hardened native launcher order-profile regression coverage.
- Added a profile repair self-test inside `NativeLauncher\ModTheSpire2Launcher.c`.
- The self-test writes a temporary profile with:
  - `QuickRestart` before `BaseLib`;
  - a missing mod id;
  - then `BaseLib`.
- Loading that profile must ignore the missing id, preserve installed/new mods, repair dependencies, and leave `BaseLib` before `QuickRestart`.
- Rebuilt the native launcher and synchronized clean Workshop content, uploader content, and live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `471.7 KB`, DLL SHA256 `4903B1D7EA915DA8E1FAA2566827AC85B37EEE3C8B73BF84A9C8405DC259EF10`, launcher SHA256 `4722AC115D70D3277204E7BE80B5111D220A26165DC31B68C4F038F36CDD6EFC`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-1252-profile-repair-selftest`.

## 2026-06-21 12:59

- Upgraded named launcher profiles from order-only to order-plus-enabled selections.
- Existing order-only profile files remain compatible: `<profile>.txt` still stores order.
- New sidecar files `<profile>.enabled.txt` store enabled mod selections for named profiles.
- Loading a named profile restores both order and enabled selections when the sidecar exists; old profiles without a sidecar still load order only.
- The profile list filters out `.enabled.txt` sidecars so they do not appear as separate profiles.
- Extended the hidden order self-test to save, clear, reload, and verify a named profile enabled-selection sidecar.
- Updated `MANUAL_TEST_0.4.0.md` with named profile enabled-selection checks.
- Rebuilt the native launcher and synchronized clean Workshop content, uploader content, and live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `474.4 KB`, DLL SHA256 `4903B1D7EA915DA8E1FAA2566827AC85B37EEE3C8B73BF84A9C8405DC259EF10`, launcher SHA256 `EF213C2798173BA0942E3771188E1C8B8AA63FA1564114E86F33335ED4C95B9A`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-1259-profile-enabled-selections`.

## 2026-06-21 13:04

- Improved launcher feedback when dependency repair adjusts load order.
- `RepairDependencyOrderInPlace` now reports whether it changed the order and logs `Load order adjusted to satisfy dependencies`.
- `Save Order`, named profile save, and profile load now show a status message when the requested order was adjusted to satisfy dependencies.
- Profile repair self-test now requires the intentionally invalid profile to report a dependency adjustment.
- Updated `MANUAL_TEST_0.4.0.md` with the dependency-adjustment status check.
- Rebuilt the native launcher and synchronized clean Workshop content, uploader content, and live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `474.9 KB`, DLL SHA256 `4903B1D7EA915DA8E1FAA2566827AC85B37EEE3C8B73BF84A9C8405DC259EF10`, launcher SHA256 `77B950D9C2B3F72B40F7ECA15E4C1F9E378958FA5AC977366A90D52F15FAB423`.
- Snapshot saved at `dist/snapshots/hot-reload/20260621-1304-dependency-adjustment-feedback`.

## 2026-06-21 13:11

- Continued from `GOAL_NEXT_STAGE_HOT_RELOAD.md` and confirmed the current baseline from the clean package, progress log, and key source files.
- Added a broader manifest vocabulary for state-aware future-run content classification in both the in-game scanner and offline classifier.
- New recognized fields include `hot_apply_content`, `hot_reload_content`, `content_type`, `content_types`, `content_tags`, `mod_type`, `mod_types`, `tags`, and `categories`.
- New recognized English tokens include `future_run_content`, `run_generation`, `events`, `characters`, `relics`, `cards`, `encounters`, and `ancients`.
- Added controlled test fixture `TestMods/ModTheSpire2TaggedFutureContentTest` to prove future-run content can be discovered through `content_type`/`content_tags` rather than only `hot_apply_scope` or hardcoded `wuwancients`.
- Verified classifier behavior:
  - default state keeps the tagged future-content test blocked with `main menu only; safe menu state not confirmed`;
  - simulated safe main menu but unloaded state keeps it blocked with `main menu only; mod is not loaded, start through launcher to enable`;
  - simulated safe main menu plus loaded state promotes it with `future-run content toggle; main menu safe`.
- Rebuilt `ModTheSpire2.dll`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `476.4 KB`, DLL SHA256 `FF9D1CCFE52BEE26F299139DBA6513D4E4BB9D6EBC266C175062A7F73F86D5C3`.
- Updated `HOT_APPLY_FINDINGS.md` and `MANUAL_TEST_0.4.0.md` with the new manifest-tag classification behavior.

## 2026-06-21 13:20

- Hardened native launcher handling for mods whose dependency is not installed.
- The launcher now builds the visible `Depends on` column through a shared helper that marks missing dependencies as `missing: <dependency id>`.
- Checking a mod with a missing dependency immediately unchecks it and shows a missing-dependency message.
- `Launch Selected` also blocks selected mods with missing dependencies before writing settings or starting the game.
- Launcher diagnostics now use the same dependency display helper, so automated tests cover the text shown by the launcher UI.
- Extended the package verifier's temporary fake game fixture with `LauncherMissingDependency`, which depends on missing `MissingFramework`.
- The verifier now requires diagnostics to contain `deps=missing: MissingFramework`.
- Rebuilt `ModTheSpire2Launcher.exe`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `477.54 KB`, DLL SHA256 `FF9D1CCFE52BEE26F299139DBA6513D4E4BB9D6EBC266C175062A7F73F86D5C3`, launcher SHA256 `3512A4CEF350F874AC14BE4D31243FF7CEDAD6C997168F738A13DDF1CFA79604`.
- Updated `MANUAL_TEST_0.4.0.md` with missing-dependency launcher checks.

## 2026-06-21 13:27

- Improved the in-game ModTheSpire2 management overlay's state-aware Hot-Apply messaging.
- Added a summary line directly under `Game state:` explaining whether Hot-Apply is currently available, blocked by unfinished/active run state, blocked because candidates are not loaded, or absent.
- Disabled the `Apply Hot Changes` button when there are no currently safe Hot-Apply candidates, with a tooltip directing players to the launcher flow for restart-required changes.
- Kept HotApplyService behavior unchanged; this is a UI clarity/safety improvement rather than a settings-write change.
- Rebuilt `ModTheSpire2.dll`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `479.54 KB`, DLL SHA256 `A8A6B2747ACD61D4ED8E4D36D635C50DE7CA5F1DF4A674521C67EE2E94396012`, launcher SHA256 `3512A4CEF350F874AC14BE4D31243FF7CEDAD6C997168F738A13DDF1CFA79604`.
- Updated `MANUAL_TEST_0.4.0.md` with summary-line and disabled-button checks.

## 2026-06-21 13:35

- Made missing-dependency reasons visible for every discovered mod in the in-game overlay/classifier, not only mods that were initially Hot-Apply candidates.
- Restart-required DLL/PCK mods with missing dependencies now show `missing dependency: <dependency id>` instead of a generic `DLL/PCK or unknown startup behavior` reason.
- Mirrored the rule in `Tools/ClassifyModsForHotApply.ps1`.
- Added controlled fixture `TestMods/ModTheSpire2MissingDependencyTest`, a DLL/PCK gameplay manifest depending on absent `ModTheSpire2NotInstalledDependency`.
- Extended the verifier to require `ModTheSpire2MissingDependencyTest` to report the missing dependency reason.
- Rebuilt `ModTheSpire2.dll`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `479.54 KB`, DLL SHA256 `530FBEB60BFBB211FF125B3668DB6F34BE0B2ECAB7F74021395F9D1FB3520404`, launcher SHA256 `3512A4CEF350F874AC14BE4D31243FF7CEDAD6C997168F738A13DDF1CFA79604`.
- Updated `MANUAL_TEST_0.4.0.md` with the overlay missing-dependency reason check.

## 2026-06-21 13:45

- Investigated whether STS2 exposes a stronger loaded-mod signal than `settings.save`.
- Confirmed from `sts2.xml` that the game distinguishes `ModLoadState.Loaded`, `Failed`, `Disabled`, `DisabledDuplicate`, and `AddedAtRuntime`; this supports the goal requirement to treat enabled and loaded as separate concepts.
- Direct PowerShell reflection against `sts2.dll` caused a StackOverflowException, so no executable reflection dependency was added.
- Updated in-game `ModSummary` to carry both `IsEnabled` and `IsLoaded`.
- `IsEnabled` still reflects the current settings entry and drives checkbox default state.
- `IsLoaded` is now derived from current AppDomain assembly names plus a seeded `ModTheSpire2` self hint, and is used for main-menu content Hot-Apply promotion.
- Main-menu content candidates now require `IsLoaded` rather than only `IsEnabled`; this avoids treating a mod enabled in settings but not loaded into the current process as Hot-Apply safe.
- Framework dependencies for main-menu candidates now require the dependency to be loaded this session before they can be accepted as already available.
- Overlay row tooltips now show both `Enabled in settings` and `Loaded this session` for debugging.
- Rebuilt `ModTheSpire2.dll`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `480.54 KB`, DLL SHA256 `5CF37642D08BE59F0BEF3D4672874BB3A629873A75FA9CF584656A1FC36AB99D`, launcher SHA256 `3512A4CEF350F874AC14BE4D31243FF7CEDAD6C997168F738A13DDF1CFA79604`.
- Updated `HOT_APPLY_FINDINGS.md` and `MANUAL_TEST_0.4.0.md` with the enabled-vs-loaded distinction.

## 2026-06-21 13:53

- Strengthened automated coverage for the enabled-vs-loaded Hot-Apply distinction.
- Added `-AssumeEnabledIds` and `-ShowState` to `Tools/ClassifyModsForHotApply.ps1`.
- The offline classifier can now explicitly show simulated `enabled=True loaded=False` state without promoting a main-menu content mod.
- Extended `Tools/VerifyModTheSpire2Package.ps1` to require:
  - safe main menu plus settings-enabled but unloaded `ModTheSpire2FutureRunContentTest` remains blocked;
  - the output explicitly reports `enabled=True loaded=False`.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `480.54 KB`, DLL SHA256 `5CF37642D08BE59F0BEF3D4672874BB3A629873A75FA9CF584656A1FC36AB99D`.
- No new binary snapshot was created because this change only strengthens test tooling and documentation; the current usable binary rollback remains `dist/snapshots/hot-reload/20260621-1345-enabled-vs-loaded-hotapply`.
- Updated `MANUAL_TEST_0.4.0.md` with the verifier coverage note.

## 2026-06-21 14:02

- Improved the in-game overlay visual hierarchy for state-aware Hot-Apply candidates.
- Split previously combined Hot-Apply candidates into:
  - `Runtime Hot-Apply`
  - `Apply at Main Menu`
- `Runtime Hot-Apply` contains runtime/config candidates.
- `Apply at Main Menu` contains loaded future-run content candidates that are only safe at a safe main menu with no unfinished run.
- `Main Menu Only / Currently Blocked` remains for future-run content candidates blocked by current state or loaded-session checks.
- The summary line now reports separate runtime-safe and main-menu-only candidate counts.
- HotApplyService behavior remains unchanged; this is a UI clarity and state-visibility improvement.
- Rebuilt `ModTheSpire2.dll`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `481.54 KB`, DLL SHA256 `770289CB8BBC022493ABABA832C067756FD7C2A93ADB4FF6560B723EFA854330`, launcher SHA256 `3512A4CEF350F874AC14BE4D31243FF7CEDAD6C997168F738A13DDF1CFA79604`.
- Updated `MANUAL_TEST_0.4.0.md` with the new section names and summary-count checks.

## 2026-06-21 14:12

- Hardened launcher saved/profile enabled-selection restoration when a selected mod has a missing dependency.
- Added `PruneInvalidChecks()` to clear checked mods whose dependencies are either missing or known but unchecked.
- `RefreshList()` now prunes invalid saved selections after restoring `enabled-mods.txt` or `settings.save` state.
- `LoadEnabledFromPathToList()` now prunes invalid profile selections after loading a named profile sidecar.
- Automatic pruning writes launcher log entries and updates the status text instead of opening a message box during startup/profile restore.
- Manual checkbox clicks still show the existing missing-dependency message.
- Extended the hidden launcher order self-test to simulate a checked mod with a missing dependency and require it to be pruned.
- Extended the verifier's fake game fixture so `--self-test-order` must log `Order self-test pruned missing dependency selection`.
- Rebuilt `ModTheSpire2Launcher.exe`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `482.59 KB`, DLL SHA256 `770289CB8BBC022493ABABA832C067756FD7C2A93ADB4FF6560B723EFA854330`, launcher SHA256 `DD1C5CEB3DDE30D0D21C1AF5F7D932D5568AAFF15B61B3255FC257149744AAC1`.
- Updated `MANUAL_TEST_0.4.0.md` with saved/profile invalid-selection pruning checks.

## 2026-06-21 14:21

- Generalized restart-required classification beyond hardcoded `BaseLib`, `RitsuLib`, and `STS2-RitsuLib` ids.
- The in-game scanner and offline classifier now recognize conservative manifest tags such as `framework`, `library`, `shared_library`, `dependency_root`, `api`, `core_patch`, `ui_patch`, `startup_patch`, `harmony_patch`, `save_serializer`, and `runtime_service`.
- These tags keep a mod Restart Required even if it also declares `hot_apply`.
- Added controlled fixtures:
  - `TestMods/ModTheSpire2TaggedFrameworkTest`
  - `TestMods/ModTheSpire2TaggedUiPatchTest`
- Extended the verifier to require both fixtures to remain Restart Required.
- Rebuilt `ModTheSpire2.dll`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `483.09 KB`, DLL SHA256 `F0E08389FE9297200E01406968548C70DB31853C949B33E9D3D976B2D06079BB`, launcher SHA256 `DD1C5CEB3DDE30D0D21C1AF5F7D932D5568AAFF15B61B3255FC257149744AAC1`.
- Updated `HOT_APPLY_FINDINGS.md` and `MANUAL_TEST_0.4.0.md` with restart-required tag behavior.

## 2026-06-21 14:02

- Split framework-root tags from generic restart-required patch tags.
- `framework`, `library`, `shared_library`, `dependency_root`, `api`, and `runtime_service` now mark a mod as both Restart Required and a reusable loaded dependency root.
- `core_patch`, `ui_patch`, `startup_patch`, `harmony_patch`, and `save_serializer` remain Restart Required but are not treated as dependency roots for Hot-Apply.
- Updated the in-game scanner's `ModSummary` with `IsFrameworkRoot` and changed loaded dependency handling to use that flag instead of hardcoded `BaseLib`/`RitsuLib` ids.
- Mirrored the rule in `Tools/ClassifyModsForHotApply.ps1`.
- Added controlled fixture `TestMods/ModTheSpire2TaggedFrameworkDependentContentTest`.
- Extended `Tools/VerifyModTheSpire2Package.ps1` to verify:
  - the tagged framework root stays Restart Required;
  - a loaded future-run content mod is blocked when its tagged framework root is not loaded;
  - the same content mod is promoted at a safe main menu when both it and the tagged framework root are loaded;
  - UI/core patch tags still override `hot_apply` without becoming framework roots.
- Rebuilt `ModTheSpire2.dll`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `484.09 KB`, DLL SHA256 `D9DEB6EEB6984A4DE043889A62B3FBDFC4DD28B91ACF01C2CED8BFBFF3ED0C8D`, launcher SHA256 `DD1C5CEB3DDE30D0D21C1AF5F7D932D5568AAFF15B61B3255FC257149744AAC1`.
- Updated `HOT_APPLY_FINDINGS.md` and `MANUAL_TEST_0.4.0.md` with tagged framework-root dependency behavior.

## 2026-06-21 14:09

- Investigated stronger game-state signals for resumable unfinished runs using `sts2.xml` and `Tools/MetadataInspector` without executing game assembly reflection directly.
- Confirmed public API surface:
  - `SaveManager.HasRunSave`
  - `SaveManager.HasMultiplayerRunSave`
  - `SaveManager.CurrentRunSaveTask`
  - `NMainMenu.ContinueRunInfo`
  - `NContinueRunInfo.HasResult`
- Strengthened `GameSessionState.Detect()` so unfinished-run state is true if any of these are present:
  - single-player run save;
  - multiplayer run save;
  - main-menu continue-run panel has a result;
  - a current run save task is still in progress.
- `Game state:` and companion log now include source details: `run save`, `multiplayer save`, `continue panel`, and `saving`.
- Extended `Tools/ClassifyModsForHotApply.ps1` with simulated state switches:
  - `-AssumeMainMenuWithUnfinishedRun`
  - `-AssumeActiveRun`
  - `-AssumePartialSignals`
- Extended `Tools/VerifyModTheSpire2Package.ps1` to prove loaded future-run content stays blocked when an unfinished run or active run is simulated.
- Rebuilt `ModTheSpire2.dll`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `484.59 KB`, DLL SHA256 `83E35BCE2ACFCC0E391ED0058668F5E583209D8A95D76430EE884F7DE8AFB3BB`, launcher SHA256 `DD1C5CEB3DDE30D0D21C1AF5F7D932D5568AAFF15B61B3255FC257149744AAC1`.
- Updated `HOT_APPLY_FINDINGS.md` and `MANUAL_TEST_0.4.0.md` with the stronger unfinished-run detection and verifier coverage.

## 2026-06-21 14:14

- Polished the in-game ModTheSpire2 management overlay without changing launcher or Hot-Apply behavior.
- Replaced the loose `Game state:` and summary labels with a framed state summary panel.
- The state panel now shows a headline such as safe main menu, active run, unfinished run available, or unconfirmed menu state.
- The state panel also shows compact source signals for debugging: main menu, active run, run save, multiplayer save, continue panel, saving, and reliability.
- Blocked and Restart Required mod rows now use a two-line information layout:
  - first line: mod name and id;
  - second line: reason, enabled state, loaded state, and dependencies.
- Hot-Apply checkbox rows now keep the visible text shorter and move detailed reason/state data into the tooltip.
- Rebuilt `ModTheSpire2.dll`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `487.59 KB`, DLL SHA256 `3E9EAE8C74D245E2586E45C62C2B854DDA65BC62FE6BAC4F38ABC085B683A98E`, launcher SHA256 `DD1C5CEB3DDE30D0D21C1AF5F7D932D5568AAFF15B61B3255FC257149744AAC1`.
- Updated `MANUAL_TEST_0.4.0.md` with the new visual checks for the state panel and two-line mod rows.

## 2026-06-21 14:21

- Performed a read-only scan of current Workshop and local mod manifests.
- Confirmed current subscribed Workshop mods include standard `<mod>.json` and `mod_manifest.json` layouts, plus RitsuLib's sidecar `ritsulib-variants.manifest`.
- Confirmed several local legacy mods ship an id-less `mod_manifest.json` or typo `mod_mainfest.json` containing `pck_name`, while also often shipping a full `<mod>.json` or `settings.json` with an `id`.
- Added conservative `pck_name` fallback scanning to:
  - in-game scanner;
  - offline classifier;
  - native launcher.
- The fallback only creates a mod id from `pck_name` when the same directory has no other JSON file with a real `id`, avoiding duplicate entries for sidecar manifests.
- Added controlled fixtures:
  - `TestMods/ModTheSpire2PckNameFallbackTest`;
  - `TestMods/ModTheSpire2PckNameDuplicateGuardTest`.
- Extended verifier coverage so both the classifier and launcher diagnostics must discover pck-name-only manifests and ignore pck-name sidecars beside real id manifests.
- Rebuilt `ModTheSpire2.dll` and `ModTheSpire2Launcher.exe`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `488.59 KB`, DLL SHA256 `D227DADEDDE821D75DD494643A5AA2D99D6551DFC721F1E188201D4AD0675A16`, launcher SHA256 `D75CF082583F8C35EEAEDBF386464E7C013BDB8156590F0974F328FFFE59F478`.
- Updated `HOT_APPLY_FINDINGS.md` and `MANUAL_TEST_0.4.0.md` with the legacy `pck_name` fallback behavior.

## 2026-06-21 14:28

- Added conservative dependency display-name alias resolution.
- Exact mod-id dependency matches still win.
- If a dependency string does not match any id but uniquely matches one discovered mod's display `name`, the dependency is canonicalized to that mod id.
- If the display-name match is ambiguous, the dependency remains unresolved and is shown as missing.
- Mirrored the rule in:
  - in-game scanner;
  - offline classifier;
  - native launcher.
- Added controlled fixtures:
  - `TestMods/ModTheSpire2NameAliasBase`;
  - `TestMods/ModTheSpire2NameAliasDependent`;
  - `TestMods/ModTheSpire2AmbiguousNameAliasOne`;
  - `TestMods/ModTheSpire2AmbiguousNameAliasTwo`;
  - `TestMods/ModTheSpire2AmbiguousNameAliasDependent`.
- Extended verifier coverage so both the classifier and launcher diagnostics prove unique display-name aliases resolve and ambiguous display-name aliases remain missing.
- Rebuilt `ModTheSpire2.dll` and `ModTheSpire2Launcher.exe`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `490.59 KB`, DLL SHA256 `3F21541A820C17160508C6E62FE48840D4E84664A5619F327BCD38D0BCC3E5A0`, launcher SHA256 `7CDED240A4216ECD7DAE05DB43E908E7196F519475FA191D6A5F02554482372B`.
- Updated `HOT_APPLY_FINDINGS.md` and `MANUAL_TEST_0.4.0.md` with dependency display-name alias behavior.

## 2026-06-21 14:33

- Added conservative Workshop numeric dependency alias resolution.
- Exact mod-id matches still win, followed by unique display-name alias matching.
- If a dependency string uniquely matches a discovered mod's local Workshop folder id, it is canonicalized to that mod id.
- The rule only uses already discovered local Workshop folders and does not query Steam or guess missing subscriptions.
- Mirrored the rule in:
  - in-game scanner;
  - offline classifier;
  - native launcher.
- Extended verifier coverage with a temporary classifier fixture under the clean package runtime data and a launcher fake-game fixture containing a synthetic Workshop content folder.
- Rebuilt `ModTheSpire2.dll` and `ModTheSpire2Launcher.exe`, synchronized clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `492.09 KB`, DLL SHA256 `ECBBA9AFE3B687C855072534C2438AE1E3E0D7FA092D9D4044259F173077044F`, launcher SHA256 `03517F93AC8542D4D4C355CD310C9B497A415DEF8621813F621BD5FB78A8FF3F`.
- Updated `HOT_APPLY_FINDINGS.md` and `MANUAL_TEST_0.4.0.md` with Workshop numeric dependency alias behavior.

## 2026-06-21 14:36

- Updated release-facing documentation after the accumulated post-0.4.0 development changes.
- Expanded clean package `README.md` to describe:
  - state-aware Hot-Apply sections;
  - active/unfinished run safety checks;
  - loaded-session checks;
  - framework/root and restart-required tag handling;
  - dependency field/key compatibility;
  - dependency resolution by id, unique display name, and local Workshop numeric folder id;
  - legacy `pck_name` manifest fallback;
  - missing dependency behavior.
- Updated `ModUploader-win-x64/template/workshop.json` description and change note with the same feature set and GitHub link.
- Synchronized README to clean Workshop content, uploader content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `494.42 KB`, DLL SHA256 `ECBBA9AFE3B687C855072534C2438AE1E3E0D7FA092D9D4044259F173077044F`, launcher SHA256 `03517F93AC8542D4D4C355CD310C9B497A415DEF8621813F621BD5FB78A8FF3F`.
- No new rollback snapshot was created because this was documentation/upload-metadata only; the latest binary rollback remains `dist/snapshots/hot-reload/20260621-1433-workshop-id-dependency-alias`.

## 2026-06-21 14:43

- Finished the native launcher dependency-readiness `Status` column.
- Added visible row states for missing dependencies, unchecked available mods, and checked ready mods.
- Refreshed the status column after list rebuilds, saved/profile pruning, and dependency checkbox enforcement.
- Added the Status column to the hidden order self-test ListView and the real launcher ListView.
- Extended launcher diagnostics and the package verifier to assert `status=Missing dependency: MissingFramework` for a fake missing-dependency mod.
- Rebuilt `ModTheSpire2Launcher.exe`, synchronized clean Workshop content, uploader content, release content, and the live test folder.
- Re-ran full `Tools\VerifyModTheSpire2Package.ps1` without `-SkipLive`: Status OK, version `0.4.0`, clean package `496.04 KB`, DLL SHA256 `ECBBA9AFE3B687C855072534C2438AE1E3E0D7FA092D9D4044259F173077044F`, launcher SHA256 `17597BCAD66EF31F1C295B9102412894D406F33E4189635BA5AD106E7731BB96`.
- Updated `MANUAL_TEST_0.4.0.md` with Status column checks.
- Saved rollback snapshot: `C:\Users\HZDH\Desktop\tmp\Forimpro\dist\snapshots\hot-reload\20260621-1443-launcher-status-column`.

## 2026-06-21 14:48

- Added explicit save-serializer Hot-Apply safety classification.
- Manifest token save_serializer now produces the specific reason save serializer; restart required instead of the generic startup/core/UI patch reason.
- Added controlled fixture TestMods/ModTheSpire2TaggedSaveSerializerTest, which declares hot_apply: true but is still forced Restart Required for save safety.
- Mirrored the rule in Tools/ClassifyModsForHotApply.ps1.
- Extended Tools/VerifyModTheSpire2Package.ps1 to assert the save serializer fixture remains Restart Required.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 498.04 KB, DLL SHA256 95D7CF86BD5DA2420B25A359040961871662DA47AEB4B2D922C983D9D337A8BA, launcher SHA256 17597BCAD66EF31F1C295B9102412894D406F33E4189635BA5AD106E7731BB96.
- Updated HOT_APPLY_FINDINGS.md and MANUAL_TEST_0.4.0.md with save serializer Restart Required behavior.
- Saved rollback snapshot: C:\Users\HZDH\Desktop\tmp\Forimpro\dist\snapshots\hot-reload\20260621-1448-save-serializer-hotapply-safety.

## 2026-06-21 14:57

- Added explicit runtime/config scope classification.
- Manifest scope or tags such as runtime, runtime_safe, settings, config, configuration, and utility can now classify config-only, non-gameplay, no-DLL/PCK mods as Runtime Hot-Apply.
- DLL/PCK or gameplay payloads remain Restart Required even if they claim runtime/config scope.
- Added controlled fixtures TestMods/ModTheSpire2RuntimeScopeConfigTest and TestMods/ModTheSpire2RuntimeScopeDllGuardTest.
- Mirrored the rule in Tools/ClassifyModsForHotApply.ps1.
- Extended Tools/VerifyModTheSpire2Package.ps1 to assert both the accepted config-only path and the DLL/PCK guard path.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 498.85 KB, DLL SHA256 1E8C37091E37BAC799793A1DBD231713E9AB615967DEAE9908E0117F11B0AC0D, launcher SHA256 17597BCAD66EF31F1C295B9102412894D406F33E4189635BA5AD106E7731BB96.
- Updated HOT_APPLY_FINDINGS.md, MANUAL_TEST_0.4.0.md, and the clean package README with runtime/config scope behavior.
- Saved rollback snapshot: C:\Users\HZDH\Desktop\tmp\Forimpro\dist\snapshots\hot-reload\20260621-1457-runtime-scope-guard.

## 2026-06-21 15:06

- Performed a read-only scan of current local and Workshop manifests and wrote analysis to dist/analysis-manifest-scan.tsv.
- Found that recursive JSON scanning could enter ModTheSpire2Data runtime folders containing settings backups or generated JSON.
- Added ModTheSpire2Data exclusion to the in-game scanner, offline classifier, and native launcher recursive discovery.
- Extended Tools/VerifyModTheSpire2Package.ps1 with ghost runtime JSON fixtures proving classifier and launcher diagnostics do not discover runtime data as mods.
- Moved classifier Workshop-alias verifier fixtures out of ModTheSpire2Data into dist/verify-fixtures so the new exclusion does not invalidate the fixture itself.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 499.51 KB, DLL SHA256 1B1107B33C9A5B075039044D74F295ECD1A4FD52CDC50147E7D4CE32CE784A55, launcher SHA256 D63835F5F19FF502194410E47E920D505AF4C4CBFCCF4997367D9BC3A26F7A44.
- Updated HOT_APPLY_FINDINGS.md, MANUAL_TEST_0.4.0.md, and the clean package README with runtime-data scan exclusion behavior.
- Saved rollback snapshot: C:\Users\HZDH\Desktop\tmp\Forimpro\dist\snapshots\hot-reload\20260621-1506-skip-runtime-data-scan.

## 2026-06-21 15:10

- Added rollback backups for ModTheSpire2-owned launcher data files before overwrite.
- SaveOrderToPath and SaveCurrentEnabledToPath now back up existing target files into ModTheSpire2Data/file-backups before replacing them.
- This covers load-order.txt, enabled-mods.txt, named order profiles, and named profile enabled-selection files.
- Added launcher self-test coverage to verify data-file backup creation.
- Extended Tools/VerifyModTheSpire2Package.ps1 to assert the launcher order self-test reports Data-file backup self-test passed.
- Rebuilt ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 501.30 KB, DLL SHA256 1B1107B33C9A5B075039044D74F295ECD1A4FD52CDC50147E7D4CE32CE784A55, launcher SHA256 21248F218E170601359CDF5A3BDEE1AA26DE46F6FC755AF919831C106CD928F0.
- Updated HOT_APPLY_FINDINGS.md, MANUAL_TEST_0.4.0.md, and the clean package README with data-file backup behavior.
- Saved rollback snapshot: C:\Users\HZDH\Desktop\tmp\Forimpro\dist\snapshots\hot-reload\20260621-1510-launcher-data-file-backups.

## 2026-06-21 15:46

- Polished the native launcher bottom control layout without changing command IDs or launch/save behavior.
- Added Load order and Profiles grouping labels.
- Renamed the order-number Apply button to Set and the profile buttons to Save Profile / Load Profile.
- Moved Refresh, Vanilla, Launch Selected, and status text to a separate lower row and increased the window height to avoid overlap.
- Rebuilt ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 501.30 KB, DLL SHA256 1B1107B33C9A5B075039044D74F295ECD1A4FD52CDC50147E7D4CE32CE784A55, launcher SHA256 F2167790F5E8FD2A386842B795457B09ACA87DA759F5247242D2193DC6F58C70.
- Updated MANUAL_TEST_0.4.0.md with launcher bottom-control visual checks.
- Saved rollback snapshot: C:\Users\HZDH\Desktop\tmp\Forimpro\dist\snapshots\hot-reload\20260621-1546-launcher-bottom-ui-polish.

## 2026-06-21 15:52

- Added dependency object parsing for local Workshop numeric id fields: workshop_id, workshopId, steam_id, steamId, published_file_id, and publishedFileId.
- Mirrored the rule in the in-game scanner, offline classifier, and native launcher.
- The values still resolve conservatively through already discovered local Workshop folders only; there is no Steam query or guessing.
- Extended Tools/VerifyModTheSpire2Package.ps1 with classifier and launcher fixtures proving Workshop object aliases resolve to canonical mod ids.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 501.87 KB, DLL SHA256 CFB184CDFEA53EC503439FBBE53AA489BA29888D51BDF31A6EF0B2151ACE030E, launcher SHA256 578E560F02C6C93B8B9409DFB3FF00AE49A5F8159562F3185639BD2EDD90DF6E.
- Updated HOT_APPLY_FINDINGS.md, MANUAL_TEST_0.4.0.md, and the clean package README with the new dependency object fields.
- Saved rollback snapshot: C:\Users\HZDH\Desktop\tmp\Forimpro\dist\snapshots\hot-reload\20260621-1552-workshop-object-dependency-alias.

## 2026-06-21 16:00

- Confirmed the active goal file and current 0.4.0 workspace baseline before making changes.
- Reviewed launcher settings discovery and found it already searched under the roaming SlayTheSpire2 settings tree instead of using a hardcoded Steam id.
- Extracted the launcher settings search into FindNewestSettingsFileUnder so the newest settings.save selection rule can be tested directly.
- Added ModTheSpire2Launcher.exe --self-test-settings, which builds a fake multi-user settings tree under ModTheSpire2Data and verifies that the newest settings.save is selected.
- Extended Tools\VerifyModTheSpire2Package.ps1 to run the new settings self-test.
- Rebuilt ModTheSpire2Launcher.exe and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated MANUAL_TEST_0.4.0.md and the clean package README with the multi-user settings.save behavior.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 504.56 KB, DLL SHA256 CFB184CDFEA53EC503439FBBE53AA489BA29888D51BDF31A6EF0B2151ACE030E.

## 2026-06-21 16:07

- Reviewed the current state-aware Hot-Apply classifier, in-game scanner, and verifier coverage.
- Found a safety gap: config/runtime-only Hot-Apply was allowed during an active run without a separate proof that the setting is safe while a run is in progress.
- Added explicit run-safe runtime tokens: run_safe, during_run, active_run_safe, and combat_safe.
- Updated the in-game C# scanner so Runtime Hot-Apply candidates are blocked during active runs unless one of those run-safe tokens is present.
- Mirrored the rule in Tools\ClassifyModsForHotApply.ps1.
- Added controlled fixture TestMods\ModTheSpire2RunSafeRuntimeConfigTest.
- Extended Tools\VerifyModTheSpire2Package.ps1 to prove ordinary runtime/config mods are blocked during active runs and explicit run-safe runtime/config mods remain Hot-Apply candidates.
- Rebuilt ModTheSpire2.dll and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated MANUAL_TEST_0.4.0.md and README with active-run runtime/config Hot-Apply behavior.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 505.82 KB, DLL SHA256 201EEE63F1204A05EAF074303E0DCD5047BBCD6618207ED9F0CBFAEE0DC3A9CD, launcher SHA256 07416D52D2485CF10C3F255F33B273B0123A2978EFD3EFD044F5B0377E7BB4F1.

## 2026-06-21 16:11

- Improved the in-game Hot-Apply overlay grouping after the active-run runtime/config guard.
- Added a separate Runtime Currently Blocked section for runtime/config candidates that are temporarily blocked by active-run safety checks.
- Kept true Restart Required for DLL/PCK/gameplay/framework/UI patch/save serializer/unknown behavior instead of mixing temporary active-run blocks into that section.
- Updated the state summary to explain that runtime/config candidates without run-safe declarations are blocked while a run is active.
- Rebuilt ModTheSpire2.dll and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated MANUAL_TEST_0.4.0.md, the clean package README, and the GitHub source README with the new Runtime Currently Blocked behavior.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 506.99 KB, DLL SHA256 B37E4A45E4B8C94D68C31685039455ADBD7D0FE76380301B4AF03DD286670EED, launcher SHA256 07416D52D2485CF10C3F255F33B273B0123A2978EFD3EFD044F5B0377E7BB4F1.

## 2026-06-21 16:15

- Tightened in-game safe-main-menu detection for main-menu-only Hot-Apply candidates.
- Safe main menu now requires the main-menu node to exist, be inside the scene tree, be visible in tree, have no active run node, and have no unfinished run signals.
- Added scene/root diagnostic signals to GameSessionState: current scene name and root child node names.
- Updated the in-game state signal line and companion log to show main-menu inside-tree/visible state, scene name, and root child names.
- Rebuilt ModTheSpire2.dll and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated MANUAL_TEST_0.4.0.md, the clean package README, and the GitHub source README with the stricter safe-main-menu signals.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 509.67 KB, DLL SHA256 1BD80D2C412235ADD404CC6D992D49CEA26B6E968D9591722F926D09A85838C1, launcher SHA256 07416D52D2485CF10C3F255F33B273B0123A2978EFD3EFD044F5B0377E7BB4F1.

## 2026-06-21 16:18

- Added verifier coverage for partial/unreliable state signals.
- Tools\VerifyModTheSpire2Package.ps1 now runs ClassifyModsForHotApply.ps1 with -AssumeSafeMainMenuNoRun, -AssumePartialSignals, and loaded future-run content ids.
- The new gate proves loaded main-menu-only content such as wuwancients and controlled future-run fixtures remain blocked with safe menu state not confirmed when signals are partial.
- Updated MANUAL_TEST_0.4.0.md and synchronized verifier/manual docs into the GitHub source export.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 509.67 KB, DLL SHA256 1BD80D2C412235ADD404CC6D992D49CEA26B6E968D9591722F926D09A85838C1, launcher SHA256 07416D52D2485CF10C3F255F33B273B0123A2978EFD3EFD044F5B0377E7BB4F1.

## 2026-06-21 16:23

- Tightened Hot-Apply dependency safety for loaded framework roots.
- Main-menu future-run content can now use a restart-required framework dependency as an already-loaded root only when that framework is both loaded this session and enabled in current settings.
- This prevents Hot-Apply from saving a dependent content mod as enabled while its required framework is disabled in settings.
- Mirrored the rule in Tools\ClassifyModsForHotApply.ps1.
- Extended Tools\VerifyModTheSpire2Package.ps1 with both cases: loaded-but-disabled framework root blocks its dependent, while loaded-and-enabled framework root allows the dependent future-run content candidate.
- Rebuilt ModTheSpire2.dll and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated README and MANUAL_TEST_0.4.0.md with the loaded+enabled framework dependency rule.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 509.73 KB, DLL SHA256 D35D1595A2980D07D704F6148FF3955EB0268241E365C0094F7D74A71951871D, launcher SHA256 07416D52D2485CF10C3F255F33B273B0123A2978EFD3EFD044F5B0377E7BB4F1.

## 2026-06-21 16:29

- Scanned local game/workshop/test manifests for dependency field shapes and wrote dist\analysis-dependency-field-shapes.tsv.
- Current local samples use dependency arrays, but compatibility was expanded for less standard manifests.
- Updated the in-game C# scanner to parse dependency fields that are arrays, a single string, or a single dependency object.
- Updated the native launcher to parse single-string and single-object dependency fields in addition to arrays.
- Added controlled fixture TestMods\ModTheSpire2SingleStringDependencyTest for the classifier path.
- Extended Tools\VerifyModTheSpire2Package.ps1 with classifier coverage for single-string dependencies and launcher diagnostics coverage for single-object dependencies.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated README and MANUAL_TEST_0.4.0.md with the dependency field shape compatibility.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 510.31 KB, DLL SHA256 189C59157F4C612EF06463E781039959DEBD0AD01B002C47E0D8F7E5C3E4E290, launcher SHA256 6C9EF14512CF2621BF8EB433BFEC89F994CD4CC447236E48E1F77E26D68F0202.

## 2026-06-21 16:32

- Improved native launcher numeric load-order feedback.
- The Row/Set control now immediately repairs dependency order after moving a mod to a numeric row.
- If dependency repair changes the requested numeric order, the launcher status now says Order was adjusted to satisfy dependencies instead of leaving the user with a misleading success message.
- Rebuilt ModTheSpire2Launcher.exe and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated MANUAL_TEST_0.4.0.md with the Row/Set dependency repair check.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 510.37 KB, DLL SHA256 189C59157F4C612EF06463E781039959DEBD0AD01B002C47E0D8F7E5C3E4E290, launcher SHA256 162C6BC348C0952FA24D1CE2546F14BD348128E8F30618C1EBA7AFEB83F2A5BE.

## 2026-06-21 16:36

- Added direct launcher self-test coverage for numeric Row/Set dependency repair.
- Refactored numeric order application into ApplyNumericOrderForIndex so the UI path and self-test share the same immediate dependency-repair logic.
- Order self-test now intentionally tries to move BaseLib below QuickRestart and verifies the numeric order path repairs the dependency order immediately.
- Extended Tools\VerifyModTheSpire2Package.ps1 to require the Order self-test numeric dependency repair passed log line.
- Rebuilt ModTheSpire2Launcher.exe and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 511.38 KB, DLL SHA256 189C59157F4C612EF06463E781039959DEBD0AD01B002C47E0D8F7E5C3E4E290, launcher SHA256 4E3287C05DB2E9CB2275A6448DCC97BFCD6A1CFBCF1113EF2B7D1EAA0C20F4ED.

## 2026-06-21 16:43

- Added conservative `.manifest` sidecar compatibility across the in-game C# scanner, PowerShell Hot-Apply classifier, and native launcher scanner.
- `.manifest` files now count as mod manifests only when they declare an explicit `id`; id-less variant sidecars such as RitsuLib's version-selection manifest are ignored instead of becoming fake mods.
- Kept legacy `pck_name` fallback limited to JSON manifests so arbitrary `.manifest` sidecars cannot create loose fallback ids.
- Added controlled fixtures for an explicit-id `.manifest` mod and an id-less variants `.manifest` file.
- Extended Tools\VerifyModTheSpire2Package.ps1 to prove classifier and launcher diagnostics discover explicit-id `.manifest` files and ignore id-less variant sidecars.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated README and MANUAL_TEST_0.4.0.md with the conservative sidecar manifest rule.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 513.72 KB, DLL SHA256 5221E2191309A13A3BB81EB8D78CED6572BABF2726196753810828C8AF869672, launcher SHA256 not shown by the verifier output but synchronized from the verified clean package.

## 2026-06-21 16:53

- Added an Apply-time state safety gate to HotApplyService.
- Hot-Apply now re-detects GameSessionState immediately before writing settings.save.
- If a main-menu-only content candidate would change state and the latest state is no longer a confirmed safe main menu with no unfinished run, the apply is blocked and settings are not written.
- The block message distinguishes disabling content during active run, disabling content while an unfinished run may reference it, and changing content from an unconfirmed screen.
- Added a PowerShell classifier self-test switch, -SelfTestApplyStateGate, to simulate the apply-time guard without launching the game.
- Extended Tools\VerifyModTheSpire2Package.ps1 to prove disabling an enabled loaded future-run content fixture is blocked when an unfinished-run state appears before Apply, while unchanged content does not block.
- Rebuilt ModTheSpire2.dll and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated README and MANUAL_TEST_0.4.0.md with the apply-time state recheck behavior.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 515.22 KB, DLL SHA256 EDEC098291FEBBEF4AE1CE8632E9D6071B149F5D5790C79D864C65A3BF50B289.

## 2026-06-21 17:05

- Performed a read-only manifest field-shape scan across local mods, current Workshop content, and TestMods; wrote dist\analysis-manifest-field-shapes-current.tsv.
- Current real samples still use dependency arrays, plus the already-covered requires/requiredMods aliases in controlled fixtures.
- Identified a conservative Hot-Apply gap for versioned-library layouts: if a manifest omits has_dll/has_pck but stores a DLL/PCK under a subfolder such as lib/<game-version>/, top-level-only payload detection could incorrectly allow Runtime Hot-Apply.
- Updated the in-game C# scanner to detect DLL/PCK payloads recursively within a mod folder while still ignoring ModTheSpire2Data runtime folders.
- Mirrored recursive payload detection in Tools\ClassifyModsForHotApply.ps1.
- Added controlled fixture TestMods\ModTheSpire2NestedPayloadGuardTest with a nested placeholder DLL under lib\0.107.1.
- Extended Tools\VerifyModTheSpire2Package.ps1 to prove a config/hot manifest with a nested payload remains Restart Required.
- Rebuilt ModTheSpire2.dll and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated README and MANUAL_TEST_0.4.0.md with nested payload detection behavior.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 515.98 KB, DLL SHA256 DC86B3102B21E11988455FF9741DBDAEE6A1211A1B9DD9D75904BD0AA8356F64.

## 2026-06-21 17:16

- Hardened dependency parsing for optional dependency objects.
- Added explicit optional dependency recognition in the in-game C# scanner, PowerShell classifier, and native launcher.
- Dependency objects marked optional: true, is_optional: true, isOptional: true, or required: false are no longer treated as hard dependencies.
- This prevents optional integrations from appearing as missing dependencies, blocking launcher selection, or downgrading Hot-Apply candidates.
- Added controlled fixture TestMods\ModTheSpire2OptionalDependencyTest.
- Extended Tools\VerifyModTheSpire2Package.ps1 with classifier coverage for optional dependency objects and native launcher diagnostic coverage for optional array and single-object dependency forms.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated README and MANUAL_TEST_0.4.0.md with optional dependency behavior.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 516.33 KB, DLL SHA256 AD52B6214026E84EC62EF36C8E9357D4DBC0C93BD79BE7D539FFDEF3DB308E18.

## 2026-06-21 17:28

- Added order-only load-after handling for launcher and in-game load-order repair.
- Native launcher now parses load_after and loadAfter into a separate orderAfter list instead of hard dependencies.
- load_after/loadAfter references are canonicalized by id, unique display name, or local Workshop folder id just like hard dependencies.
- Sort/repair/validation now keeps installed load-after targets before the declaring mod, but selection blocking, missing dependency status, profile pruning, and Hot-Apply dependency safety still only use hard dependencies.
- The launcher dependency column displays load-after relations as order: <mod id>.
- The in-game ModTheSpire2 overlay now reads load_after/loadAfter into ModSummary.LoadAfter, shows them as Loads after, and uses them for load-order repair only.
- Extended Tools\VerifyModTheSpire2Package.ps1 with launcher diagnostic coverage proving load_after appears as order: BaseLib and is not treated as a missing hard dependency.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated README and MANUAL_TEST_0.4.0.md with order-only load-after behavior.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 520.36 KB, DLL SHA256 DDA342F6A97A91BFE2EA9EB804130CD6FCBC40D02685C2D0F9BB93B2A390D683.

## 2026-06-21 17:38

- Added order-only load-before handling to mirror load-after support.
- Native launcher now parses load_before and loadBefore into a separate orderBefore list.
- In-game ModTheSpire2 overlay now reads load_before/loadBefore into ModSummary.LoadBefore, displays them as Loads before, and uses them for load-order repair only.
- Sort/repair/validation now honors installed load-before targets without treating them as hard dependencies.
- Selection blocking, missing dependency status, profile pruning, and Hot-Apply dependency safety still only use hard dependencies.
- Extended Tools\VerifyModTheSpire2Package.ps1 with launcher diagnostic coverage proving load_before appears as order before: <mod id> and is not treated as a missing hard dependency.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated README and MANUAL_TEST_0.4.0.md with order-only load-before behavior.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 523.72 KB, DLL SHA256 29D8AF3937D7657D50D6338E31ED3F839162CE85EFA8AC25BB5BB3867E12F4EF.

## 2026-06-21 17:22

- Added direct native launcher self-test proof for order-only repair.
- The order self-test now intentionally perturbs a fake `load_after` fixture by placing BaseLib after LauncherLoadAfterOnly, then verifies repair restores BaseLib before the declaring mod.
- The order self-test also intentionally perturbs a fake `load_before` fixture by placing LauncherLoadBeforeOnly after LauncherLoadAfterOnly, then verifies repair restores the before relationship.
- Extended Tools\VerifyModTheSpire2Package.ps1 to require both `Order self-test load_after repair passed` and `Order self-test load_before repair passed` in the fake-fixture self-test log.
- Rebuilt ModTheSpire2Launcher.exe and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated MANUAL_TEST_0.4.0.md to document that the verifier covers direct order-only repair, not only diagnostic display.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 525.79 KB, DLL SHA256 29D8AF3937D7657D50D6338E31ED3F839162CE85EFA8AC25BB5BB3867E12F4EF.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1722-order-only-repair-selftest.

## 2026-06-21 17:33

- Investigated the current Hot-Apply state detection path in ModTheSpire2Entry.HotManage.cs.
- Confirmed the in-game detector uses NGame.MainMenu visibility/tree state, NGame.CurrentRunNode, RunManager.IsInProgress/IsGameOver/IsAbandoned, SaveManager.HasRunSave, SaveManager.HasMultiplayerRunSave, MainMenu.ContinueRunInfo, and SaveManager.CurrentRunSaveTask.
- Added Tools\CheckSts2StateSignals.ps1. It verifies the current STS2 API documentation and DLL metadata still expose the state signals ModTheSpire2 relies on for safe-main-menu and unfinished-run detection.
- Integrated the state signal checker into Tools\VerifyModTheSpire2Package.ps1 so a future game update that renames/removes these signals fails packaging verification.
- Updated README and MANUAL_TEST_0.4.0.md to document the verified state signals.
- Synchronized README, verifier, state-signal checker, manual test doc, and progress log into the GitHub source export and release/uploader/live content where applicable.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 525.98 KB, DLL SHA256 29D8AF3937D7657D50D6338E31ED3F839162CE85EFA8AC25BB5BB3867E12F4EF.

## 2026-06-21 17:33

- Replaced the scattered `wuwancients` Hot-Apply special case with a documented `KnownMainMenuContentMods` allow-list in the C# companion scanner.
- Mirrored the same known main-menu content allow-list in Tools\ClassifyModsForHotApply.ps1.
- Kept the rule intentionally narrow: WuWa Ancients is an investigated future-run content example, not proof that arbitrary DLL/PCK gameplay mods are hot-apply safe.
- Extended Tools\VerifyModTheSpire2Package.ps1 to verify the allow-list remains mirrored in both implementations.
- Rebuilt ModTheSpire2.dll and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 525.98 KB, DLL SHA256 7EDB06C9DC86B462960B1FF896B3CE6674D551711F7BA7D029837953251B19C5.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1733-known-main-menu-content-rules.

## 2026-06-21 17:36

- Added a small in-game overlay UI polish pass for blocked and restart-required rows.
- Rows now show the mod title and a compact right-side status badge (`Blocked` or `Restart`) above the detailed reason line, improving scanability without changing Hot-Apply logic.
- Added UiStyle.CreateCompactStyle for badge-like controls while preserving the existing dark/gold game-adjacent style.
- Rebuilt ModTheSpire2.dll and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Updated MANUAL_TEST_0.4.0.md to ask testers to check badge wrapping and overlap with long mod names.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 526.48 KB, DLL SHA256 267CABF9FDFED41511125401A206A1853F0EFD792114CF4C2F1764581279076F.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1736-overlay-status-badges.

## 2026-06-21 17:39

- Tightened the overlay badge implementation after review.
- Changed status badges from a Label-level stylebox override to a PanelContainer-backed badge with an inner centered Label, which should render the badge background/border more reliably in Godot.
- Rebuilt ModTheSpire2.dll and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 526.48 KB, DLL SHA256 40CFA09DBA750F544D4A5F13802CF8A706B0B77C9CCF5C1A0B71B01781515FCA.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1739-reliable-overlay-badges.

## 2026-06-21 17:41

- Improved launcher wording for mixed dependency/order information.
- Renamed the launcher list column from `Depends on` to `Requirements`, since it now contains both hard dependency status and order-only hints.
- Changed order-only display/diagnostic text from `order:` / `order before:` to `loads after:` / `loads before:` for player-facing clarity.
- Updated verifier expectations and MANUAL_TEST_0.4.0.md accordingly.
- Rebuilt ModTheSpire2Launcher.exe and synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 526.48 KB, DLL SHA256 40CFA09DBA750F544D4A5F13802CF8A706B0B77C9CCF5C1A0B71B01781515FCA, launcher SHA256 FD537B41418FE9EC05D1DFBE0DD6EDB15A7FB736BA66F7B525674187B98923E5.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1741-launcher-requirements-wording.

## 2026-06-21 17:52

- Added conservative malformed-manifest fallback coverage after investigating real Workshop compatibility with broken JSON manifests such as Watcher.
- Tools\ClassifyModsForHotApply.ps1 now recovers invalid .json / .manifest candidates only when the filename can supply a mod id and the same mod directory contains a DLL or PCK payload.
- Invalid manifest fallback entries are marked Restart Required with reason invalid manifest; restart required; they are never Hot-Apply candidates.
- Added TestMods\ModTheSpire2InvalidManifestFallbackTest with intentionally malformed JSON and a placeholder DLL payload.
- Extended Tools\VerifyModTheSpire2Package.ps1 so the classifier must discover the invalid-manifest fixture, and launcher diagnostics must discover a malformed JSON fixture with valid early id/name fields.
- Confirmed the classifier now also sees the real Workshop Watcher mod as invalid manifest; restart required instead of missing it.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 527.48 KB, DLL SHA256 3E06EDC3CA91266164CF5F0CBE167E1FED96985E7FA20B5079B9144A41C0BA9E.

## 2026-06-21 17:55

- Reworked Tools\AnalyzeModManifests.ps1 into a richer ecosystem evidence table.
- The analysis table now records candidate kind, parse status, identity source, DLL/PCK payload presence, dependency fields/values, tag fields, and source file path.
- Confirmed the tool marks the real Workshop Watcher manifest and the controlled invalid-manifest fixture as invalid_json_payload_fallback.
- The same scan also documented current ecosystem oddities such as mod_mainfest.json and mod-like settings.json in sts2-heybox-support; these are evidence for later scanner hardening rather than release behavior changes.
- Tightened Tools\VerifyModTheSpire2Package.ps1 with source guards so both the in-game companion and classifier retain conservative invalid-manifest fallback logic.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 527.48 KB, DLL SHA256 3E06EDC3CA91266164CF5F0CBE167E1FED96985E7FA20B5079B9144A41C0BA9E.

## 2026-06-21 18:29

- Added a manifest false-positive guard across the in-game companion scanner, PowerShell classifier, and native launcher.
- Nonstandard JSON manifests are still accepted when they look like STS2 mod manifests: standard/typo manifest names, sidecar manifests, filename or parent-folder manifest shape, explicit has_dll/has_pck, sidecar companion files, or actual DLL/PCK payloads.
- Added controlled fixtures for payload-backed settings.json compatibility and plain config.json false-positive rejection.
- Preserved existing compatibility for pck_name sidecars, real id manifests beside sidecars, Workshop id dependency aliases, malformed manifest fallback, and runtime-data exclusion.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 530.09 KB, DLL SHA256 2D4521167B6CB134C7C33B634206A8938246D60430A93B49806684789CBF3CE5.

## 2026-06-21 18:33

- Improved the Workshop manifest analysis tool to preserve dependency version constraints such as min_version, minVersion, minimum_version, ersion, and max-version aliases.
- The analysis TSV now includes a DependencyConstraints column while keeping the existing dependency id/value columns unchanged.
- Verified current Workshop samples now record BaseLib minimum-version requirements: QuickRestart -> 3.3.0, wuwancients -> v3.2.0, and ActsFromThePast -> v3.2.1.
- Added a package verifier gate that runs Tools\AnalyzeModManifests.ps1 and fails if those real Workshop dependency constraints are no longer captured.
- This is evidence/tooling groundwork only; launcher and in-game behavior still do not block on dependency version mismatches yet.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 530.09 KB, DLL SHA256 2D4521167B6CB134C7C33B634206A8938246D60430A93B49806684789CBF3CE5.

## 2026-06-21 18:38

- Added dependency minimum-version handling to Tools\ClassifyModsForHotApply.ps1.
- The classifier now reads dependency object version fields such as min_version, minVersion, minimum_version, version_min, and required_version, canonicalizes dependency aliases, and compares numeric version segments conservatively.
- Missing dependencies still take precedence over version mismatches.
- Unknown or unparseable installed/required versions do not block by themselves; only a proven lower installed version downgrades the mod.
- Added verifier coverage for ModTheSpire2VersionedFrameworkOldTest v1.2.0 and ModTheSpire2VersionedDependencyTooLowTest requiring v1.3.0.
- Confirmed the classifier reports `dependency version too low: ModTheSpire2VersionedFrameworkOldTest requires v1.3.0, found v1.2.0`.
- Updated MANUAL_TEST_0.4.0.md with dependency version mismatch coverage.
- Synchronized the updated tool scripts, manual test doc, goal docs, and versioned dependency fixtures into the GitHub source export.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 530.09 KB, DLL SHA256 2D4521167B6CB134C7C33B634206A8938246D60430A93B49806684789CBF3CE5.
- No binary rebuild or rollback snapshot was created because this pass changed tooling/classifier groundwork only, not the runnable Workshop content.

## 2026-06-21 18:47

- Promoted dependency minimum-version handling from classifier groundwork into the runnable ModTheSpire2 companion and native launcher.
- The in-game companion scanner now stores mod versions and dependency minimum-version requirements, canonicalizes versioned dependency aliases, and marks a mod Restart Required with `dependency version too low: <id> requires <min>, found <actual>` when the installed dependency is provably too old.
- The native launcher now reads mod versions and dependency `min_version`-style fields, shows requirements such as `LauncherVersionedFrameworkOld >= v1.3.0`, reports `Dependency too old` status, blocks direct checkbox selection, prunes invalid saved/profile selections, and blocks Launch Selected for too-old dependencies.
- Kept the comparison conservative: missing dependencies still take precedence, and unknown/unparseable versions do not block by themselves.
- Extended Tools\VerifyModTheSpire2Package.ps1 with fake launcher fixtures for `LauncherVersionedFrameworkOld` v1.2.0 and `LauncherVersionedDependencyTooLow` requiring v1.3.0.
- Updated MANUAL_TEST_0.4.0.md so dependency version mismatch coverage now includes launcher diagnostics/status, not only classifier output.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 541.77 KB, DLL SHA256 12FC13786C12976CE1E7F6CBF1699F4F731928D82FAC31F5CBB3ECBC6BC93767, launcher SHA256 DC8B39733219D5FDCD4BF92A2B43F6E716536F201E1D9D31E440520A60FF6A33.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1847-dependency-version-guard.

## 2026-06-21 18:51

- Audited the current Hot-Apply Apply path and confirmed it already re-checks latest game state before saving settings.
- Verified the existing safety gate blocks changing main-menu/future-run content candidates if an unfinished run or active run appears after the overlay was opened.
- Improved candidate-row UI clarity: Hot-Apply rows now append the exact action that Apply Hot Changes will perform, such as `currently enabled; uncheck to disable` or `currently disabled; check to enable`.
- Main-menu content candidates now use future-run wording, for example `currently enabled; uncheck to disable before future runs`.
- Candidate tooltips now include `Action when applied:` so the player can inspect the intended change before applying it.
- Added a verifier source guard to keep the action text from being accidentally removed.
- Updated MANUAL_TEST_0.4.0.md with overlay checks for action text and future-run wording.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 542.27 KB, DLL SHA256 E43D97D6CE9A549C84B49B646BAB31A13BA4DF52743B33EAD891DDDA8CD600CB, launcher SHA256 DC8B39733219D5FDCD4BF92A2B43F6E716536F201E1D9D31E440520A60FF6A33.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1851-hot-apply-action-text.

## 2026-06-21 18:56

- Tightened Hot-Apply settings backup behavior in the in-game companion.
- Replaced the inline AppData/SlayTheSpire2/steam scan with FindLatestSettingsFile and EnumerateSettingsSearchRoots helpers.
- The backup now consistently selects the newest discovered `settings.save` and logs both the backup destination and the source settings file path.
- Added a package verifier source guard so the companion keeps the newest-settings backup selection behavior.
- Updated MANUAL_TEST_0.4.0.md to ask testers to confirm `companion.log` records the Hot-Apply backup source path.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 543.77 KB, DLL SHA256 E14381DB988F91D0B5EA7F507D34E1CB809D5ECDC97BD80906F3905924D9DA19, launcher SHA256 DC8B39733219D5FDCD4BF92A2B43F6E716536F201E1D9D31E440520A60FF6A33.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1856-hot-apply-settings-backup.

## 2026-06-21 19:01

- Tightened the Apply-time state safety gate for runtime/config Hot-Apply candidates.
- The in-game companion now re-checks the latest state at Apply time and blocks changing non-run-safe runtime/config candidates if an active run appears after the overlay was opened.
- Run-safe runtime/config candidates remain allowed during an active run when the manifest explicitly declares a run-safe token.
- Mirrored the same rule in Tools\ClassifyModsForHotApply.ps1 SelfTestApplyStateGate.
- Extended Tools\VerifyModTheSpire2Package.ps1 so the state-gate self-test must prove active-run runtime/config changes are blocked.
- Updated MANUAL_TEST_0.4.0.md with Apply-time runtime/config state-gate checks.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 544.27 KB, DLL SHA256 AE34A241CDF67EDA907C6FFA7B5137879A11D6CE5D5F6A0AB04E70C521072526, launcher SHA256 DC8B39733219D5FDCD4BF92A2B43F6E716536F201E1D9D31E440520A60FF6A33.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1901-runtime-apply-state-gate.

## 2026-06-21 19:06

- Extended conservative Restart Required manifest tokens for active-run/save-affecting behavior.
- Added restart-required aliases for active-run-patch, run-patch, combat-patch, save-patch, save-affecting, save-data, and serializer.
- Mirrored the token expansion in the in-game companion scanner and Tools\ClassifyModsForHotApply.ps1.
- Added controlled fixtures ModTheSpire2TaggedActiveRunPatchTest and ModTheSpire2TaggedSaveAffectingPatchTest; both declare hot_apply but must remain Restart Required.
- Extended Tools\VerifyModTheSpire2Package.ps1 to fail if active-run or save-affecting patch tags are allowed as Hot-Apply candidates.
- Updated MANUAL_TEST_0.4.0.md with verifier coverage for active-run and save-affecting patch manifest tags.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 544.77 KB, DLL SHA256 4BFA5E03669D090E4A3388C4EBA27A1E171380492C7BDDAE01196FAFDEE041BE, launcher SHA256 DC8B39733219D5FDCD4BF92A2B43F6E716536F201E1D9D31E440520A60FF6A33.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1906-run-save-patch-tags.

## 2026-06-21 19:11

- Improved restart-required reason precision for manifest patch tags.
- Active-run/run/combat patch tags now report `active-run patch; restart required` instead of the generic startup/core/UI patch reason.
- Save-affecting/save-data/serializer patch tags now report `save-affecting patch; restart required`, while `save-serializer` keeps `save serializer; restart required`.
- Mirrored the reason split in the in-game companion scanner and Tools\ClassifyModsForHotApply.ps1.
- Updated Tools\VerifyModTheSpire2Package.ps1 expectations for the active-run and save-affecting fixtures.
- Updated MANUAL_TEST_0.4.0.md to tell testers to confirm the specific reason strings.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 545.27 KB, DLL SHA256 8882F4241CD88A0B80EE917DA8819017E30659726B140BC7093371D542D33716, launcher SHA256 DC8B39733219D5FDCD4BF92A2B43F6E716536F201E1D9D31E440520A60FF6A33.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1911-specific-patch-reasons.


## 2026-06-21 19:30

- Added minimum game version compatibility handling across manifest analysis, the Hot-Apply classifier, the in-game companion scanner, and the native launcher.
- The tools now read STS2 `release_info.json` and manifest fields such as `min_game_version`, `minGameVersion`, `minimum_game_version`, `game_version_min`, and `required_game_version`.
- Added controlled fixture `TestMods\ModTheSpire2FutureGameVersionTest`, which requires `v999.0.0` and must be blocked with `game version too low`.
- Verified real Workshop samples now expose minimum game versions in analysis: RitsuLib / `STS2-RitsuLib` records `0.107.1`, and `wuwancients` records `0.107.0`.
- The launcher now stores each mod minimum game version, shows it in diagnostics as `minGame=...`, reports `Game too old: needs <required>, found <actual>`, blocks checkbox selection, prunes invalid saved/profile selections, and blocks Launch Selected for proven game-version mismatches.
- The compatibility check is conservative: missing or unparseable current game versions do not block by themselves.
- Updated MANUAL_TEST_0.4.0.md with minimum game version checks.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 548.42 KB, DLL SHA256 77FDD8AF89CA04436933639F32F11732EF571AEF7C4A104731FE39102D228995, launcher SHA256 BFE177D11B83F3B8A91CC59CC79B4628D8210FB3C6DE99C0BA3851898E127E99.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1930-game-version-guard.

## 2026-06-21 19:48

- Improved minimum game version visibility without changing classification semantics.
- In-game overlay row details and tooltips now include `requires STS2 >= <version>` when a manifest declares a minimum game version.
- Native launcher details/dependency text now includes `requires STS2 >= <version>` and diagnostics expose the same text beside the existing `minGame=...` field.
- Extended Tools\VerifyModTheSpire2Package.ps1 so companion source must keep the minimum game version row details and launcher diagnostics must show `requires STS2 >= v999.0.0` for the controlled fake fixture.
- Updated MANUAL_TEST_0.4.0.md with visible minimum game version checks.
- Rebuilt ModTheSpire2.dll and ModTheSpire2Launcher.exe, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 548.42 KB, DLL SHA256 1B2CDEFF240313368BBDF51A55A58C0C41FC8569BA10B3B47ECDA37F52866220, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1948-game-version-visible-details.

## 2026-06-21 19:44

- Fixed a critical in-game dialog layout bug where the management/restart dialogs could extend below the game window and leave buttons unreachable.
- The restart confirmation dialog now constrains itself to the viewport, places long explanatory text and launch option content inside a ScrollContainer, and keeps Copy Launch Option, Close and Open Launcher, and Cancel visible.
- The management dialog now uses smaller viewport-safe minimum dimensions and updates the scroll area/button area/title/intro widths on resize so bottom actions remain visible.
- Escape and backstop-close behavior remain wired for both dialogs.
- Added verifier source guards to prevent removing the viewport-safe scroll layout.
- Updated MANUAL_TEST_0.4.0.md and created dist\TestPackages\ModTheSpire2-20260621-1944-dialog-scroll-fix for focused manual testing.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 550.42 KB, DLL SHA256 7EDD78ECEDCD1CFE69177D4F45CAEF090A996C4C57D5F457AE4F030687881DA6, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-1944-dialog-scroll-fix.

## 2026-06-21 20:04

- Added a fixed top-right `X` close button to the in-game ModTheSpire2 management dialog so players can immediately close the overlay and return to the game without scrolling.
- Added an inline `Apply` button directly below the Runtime Hot-Apply / Apply at Main Menu candidate lists.
- The inline `Apply` button reuses the same ApplyHotChangesAndRefresh path as the bottom `Apply Hot Changes` button, so there is only one Hot-Apply behavior to maintain.
- The inline `Apply` button is disabled when no safe candidates exist and enabled when safe candidates are present.
- Added verifier source guards for the top-right close control and inline Hot-Apply control.
- Updated MANUAL_TEST_0.4.0.md and created dist\TestPackages\ModTheSpire2-20260621-2004-top-close-inline-apply for focused manual testing.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 551.92 KB, DLL SHA256 A6069E79C2DB5F346F10D2B2B36B1C84B41BF07EF8AEBAB18139BDBEF2DFC9F6, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-2004-top-close-inline-apply.

## 2026-06-21 20:17

- Fixed the management overlay action visibility issue by adding a top-bar `Close and Open Launcher` button that is visible without scrolling.
- Kept the top-right `X` close button from the previous iteration.
- Corrected Hot-Apply semantics for WuWa Ancients / `wuwancients`: it is no longer treated as an entire-mod Hot-Apply candidate.
- Documented the distinction that WuWa Ancients may expose its own runtime-safe main-menu setting, but unchecking the already-loaded DLL/PCK mod itself cannot unload or disable its effects without restart.
- Updated classifier and verifier expectations so `wuwancients` remains `DLL/PCK or unknown startup behavior` in normal, safe-menu, active-run, unfinished-run, and partial-signal simulations.
- Updated MANUAL_TEST_0.4.0.md and created dist\TestPackages\ModTheSpire2-20260621-2017-top-restart-safe-hotapply for focused manual testing.
- Rebuilt ModTheSpire2.dll, synchronized clean Workshop content, uploader content, release content, GitHub source export, and the live test folder.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1 without -SkipLive: Status OK, version 0.4.0, clean package 551.92 KB, DLL SHA256 2EDB315C92E13317AAD27737A6F88A29BBC67C74767BB070DC7FCF313D67DA0E, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.
- Saved rollback snapshot dist\snapshots\hot-reload\20260621-2017-top-restart-safe-hotapply.

## 2026-06-21 20:25

- Updated Goal-mode prompt files after user confirmed the `20260621-2017-top-restart-safe-hotapply` build is usable.
- `GOAL.md` now names the latest usable snapshot/manual test package, hashes, top-bar restart button, top-right close button, inline Apply button, and corrected `wuwancients` semantics.
- `GOAL_NEXT_STAGE_HOT_RELOAD.md` now treats `wuwancients` as a per-mod-setting investigation sample, not as proof that whole loaded DLL/PCK mods can be unchecked and disabled without restart.
- The detailed goal now explicitly preserves top-bar `Close and Open Launcher`, top-right `X`, inline Apply, viewport-safe overlay layout, dependency/game-version checks, and the corrected whole-mod Hot-Apply safety rule.
- Synchronized updated goal docs into the GitHub source export.

## 2026-06-21 21:50 Clean Restart UI

- Read GOAL_CLEAN_RESTART_MANAGER.md and aligned the active work with the simplified launcher/restart direction.
- Changed the in-game management overlay in ModTheSpire2Entry.HotManage.cs so the default player-facing UI no longer shows Runtime Hot-Apply, Apply at Main Menu, Main Menu Only blocked lists, or Apply Hot Changes controls.
- The overlay now focuses on current mod state: Enabled For This Launch, Available But Disabled, Load Order, top-bar Close and Open Launcher, top-right X, and a restart-based workflow explanation.
- Kept the native launcher source and startup flow unchanged.
- Rebuilt ModTheSpire2.dll with the existing Roslyn single-file build path and synchronized clean Workshop content, uploader content, GitHub source export, and the live local test folder.
- Updated README.md, MANUAL_TEST_0.4.0.md, and package verifier source guards for the clean restart-manager workflow.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1: Status OK, version 0.4.0, clean package 538.77 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.
- Saved rollback snapshot dist\snapshots\simple-manager\20260621-2150-clean-restart-ui and manual test package dist\TestPackages\ModTheSpire2-20260621-2150-clean-restart-ui.

## 2026-06-21 21:55 Clean Restart Documentation Alignment

- Updated player-facing documentation to match the clean launcher/restart direction instead of advertising Hot-Apply as the current workflow.
- Updated GITHUB_README.md to describe the in-game companion as a restart helper and point to GOAL_CLEAN_RESTART_MANAGER.md as the current design notes.
- Updated Workshop README.md wording so optional dependencies and order-only fields no longer mention Hot-Apply downgrades in player-facing text.
- Updated ModUploader-win-x64\template\workshop.json and ModUploader-win-x64\ModTheSpire2Workspace\workshop.json with clean restart-manager descriptions and change notes.
- Synchronized README/workshop metadata into uploader content, live test folder, and GitHub source export.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.

## 2026-06-21 21:58 Clean Restart Test Package Refresh

- Confirmed clean Workshop content, uploader content, and live test folder contain the same five files with matching hashes.
- Created a fresh manual test package after documentation synchronization: dist\TestPackages\ModTheSpire2-20260621-2157-clean-restart-docsync.
- Created rollback snapshot: dist\snapshots\simple-manager\20260621-2157-clean-restart-docsync.
- Synchronized GITHUB_README.md, root source-export README.md, Workshop README, uploader workshop metadata, manual test notes, current goal prompt, companion source, and verifier into the GitHub source export.
- Verified source-export player-facing docs no longer advertise Hot-Apply as the current workflow.
- Re-ran full Tools\VerifyModTheSpire2Package.ps1: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606, README SHA256 2CB7647CE6D8EA2C363049B8DDE2F3830548DFCA863307964A3C1F908FED96B5.

## 2026-06-21 22:03 Clean GitHub Source Export

- Audited the previous GitHub source export and found generated/runtime entries such as .git, in, obj, ModTheSpire2Data, launcher logs, analysis TSV files, and old launcher backup sources.
- Created a clean source export at dist\Release\ModTheSpire2-0.4.0-CleanRestart-GitHubSource instead of mutating the older export in place.
- The clean export includes current companion source, current native launcher source, helper/verifier scripts, controlled test manifests, manual test notes, current goal docs, uploader metadata, and the five-file Workshop upload content.
- Added Tools\VerifyGitHubSourceExport.ps1 to prevent .git, build outputs, runtime data, logs, snapshots, test packages, and analysis files from re-entering the clean source export.
- Re-ran source export verification: Status OK, file count 76, size 0.99 MB.
- Re-ran full package verification: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606, README SHA256 2CB7647CE6D8EA2C363049B8DDE2F3830548DFCA863307964A3C1F908FED96B5.

## 2026-06-21 22:05 Clean Restart Release Status

- Added RELEASE_STATUS_CLEAN_RESTART.md as the current handoff/status document for the clean launcher and restart-based manager goal.
- The status document separates automation-proven requirements from manual in-game checks that still need user verification.
- Updated Tools\VerifyGitHubSourceExport.ps1 so the clean GitHub/source export must include the release status document.
- Synchronized the status document and updated source-export verifier into dist\Release\ModTheSpire2-0.4.0-CleanRestart-GitHubSource.
- Re-ran source export verification: Status OK, file count 77, size 1 MB.
- Re-ran full package verification: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.
- Remaining completion evidence needed: manual game UI verification of the ModTheSpire2 overlay, top-right X, scroll behavior, restart confirmation, and Close and Open Launcher process flow.

## 2026-06-21 22:09 Manual Evidence Checker

- Added Tools\CheckCleanRestartManualEvidence.ps1 to inspect live companion.log and launcher.log for clean restart-manager manual test evidence.
- Current live logs show previous UI/restart evidence from  .4.0-hot-order-ui, including overlay open, top-right close, restart dialog confirm, launcher start with wait-for-pid, launcher scanning, and selected launch.
- Current live logs do not yet show Initialize ModTheSpire2 0.4.0-clean-restart-ui, so the new clean DLL still needs one real in-game test pass.
- Updated RELEASE_STATUS_CLEAN_RESTART.md with the current manual-evidence result and the exact follow-up command.
- Synchronized the new evidence checker and release status into the clean GitHub/source export.
- Re-ran source export verification: Status OK, file count 78, size 1 MB.
- Re-ran full package verification: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.

## 2026-06-21 22:12 Manual Evidence Diagnostic Improved

- Checked live files and logs: live ModTheSpire2.dll hash matches the clean build ($dllHash), but companion.log newest marker is still Initialize ModTheSpire2 0.4.0-hot-order-ui.
- Enhanced Tools\CheckCleanRestartManualEvidence.ps1 to report NextAction, live DLL hash, expected DLL hash, and newest initialize marker.
- Current manual evidence result remains INCOMPLETE, missing only CleanBuildLoaded; the script now tells the tester to fully exit Slay the Spire 2, start it again, open ModTheSpire2 management, and rerun the script.
- Updated RELEASE_STATUS_CLEAN_RESTART.md with this clearer diagnosis.
- Synchronized the updated evidence checker and release status into the clean GitHub/source export.
- Re-ran source export verification: Status OK, file count 78, size 1 MB.
- Re-ran full package verification: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.

## 2026-06-21 22:15 Manual Test Preparation Script

- Rechecked live manual evidence: still incomplete, missing only CleanBuildLoaded; live DLL hash already matches the clean build, but newest companion marker remains  .4.0-hot-order-ui.
- Added Tools\PrepareCleanRestartManualTest.ps1 to make the next manual pass cleaner: it requires the game/launcher to be closed, verifies live files match clean package hashes, backs up old companion/launcher logs, and writes manual-test-ready.txt with next steps.
- Updated RELEASE_STATUS_CLEAN_RESTART.md with the optional clean-log preparation command.
- Updated Tools\VerifyGitHubSourceExport.ps1 so the clean source export must include the preparation script.
- Synchronized the new script and docs into dist\Release\ModTheSpire2-0.4.0-CleanRestart-GitHubSource.
- Re-ran source export verification: Status OK, file count 79, size 1.01 MB.
- Re-ran full package verification: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.

## 2026-06-21 22:20 Clean Restart Goal Continuation

- Re-read GOAL_CLEAN_RESTART_MANAGER.md, RELEASE_STATUS_CLEAN_RESTART.md, and MANUAL_TEST_0.4.0.md to verify the active objective is the clean launcher/restart-helper direction, not broad whole-mod Hot-Apply.
- Re-ran Tools\VerifyModTheSpire2Package.ps1: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D, launcher SHA256 7A614860B423DA78FA19A2B889C9395C0F093112D0C98C6CEF8C883237CC9606.
- Re-ran Tools\VerifyGitHubSourceExport.ps1: Status OK, file count 79, size 1.01 MB.
- Re-ran Tools\CheckCleanRestartManualEvidence.ps1: still INCOMPLETE, missing CleanBuildLoaded. The live DLL hash matches the clean build, but the latest companion marker still shows Initialize ModTheSpire2 0.4.0-hot-order-ui.
- Confirmed no Slay the Spire 2 or ModTheSpire2Launcher process was running, then ran Tools\PrepareCleanRestartManualTest.ps1 successfully. It backed up the old live companion/launcher logs and wrote manual-test-ready.txt.
- Current next step remains a real in-game pass: start Slay the Spire 2, open the ModTheSpire2 management overlay once, then rerun Tools\CheckCleanRestartManualEvidence.ps1.

## 2026-06-21 22:22 Clean Restart Status Sync

- Synchronized updated release/progress docs into dist\Release\ModTheSpire2-0.4.0-CleanRestart-GitHubSource.
- Re-ran Tools\VerifyModTheSpire2Package.ps1: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D.
- Re-ran Tools\VerifyGitHubSourceExport.ps1: Status OK, file count 80, size 1.13 MB.
- Re-ran Tools\CheckCleanRestartManualEvidence.ps1 after clean-log preparation. It is still INCOMPLETE, now also missing companion.log and launcher.log because old logs were intentionally backed up. This is the expected clean pre-test state.

## 2026-06-21 22:26 Clean Restart Evidence Recheck

- Re-ran Tools\CheckCleanRestartManualEvidence.ps1: still INCOMPLETE because companion.log and launcher.log have not been recreated after the clean-log preparation.
- Re-ran Tools\VerifyModTheSpire2Package.ps1: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D.
- Re-ran Tools\VerifyGitHubSourceExport.ps1: Status OK, file count 80, size 1.13 MB.
- Inspected the live ModTheSpire2Data folder: manual-test-ready.txt exists from 2026-06-21 22:19 and old logs are backed up under manual-test-log-backups.
- Checked running processes: Steam is running, but Slay the Spire 2 and ModTheSpire2Launcher are not currently running, so no new clean-build game evidence can exist yet.
- Completion remains unproven until a real game launch opens the ModTheSpire2 management overlay and reruns the evidence checker.

## 2026-06-21 22:31 Clean Restart Automatic Verification Recheck

- Re-ran Tools\CheckCleanRestartManualEvidence.ps1: still INCOMPLETE. The live clean DLL is present, but no new companion.log or launcher.log has been generated since manual-test preparation.
- Rechecked running processes: Steam is running, but Slay the Spire 2 and ModTheSpire2Launcher are not running.
- Reviewed launcher/verifier coverage: Tools\VerifyModTheSpire2Package.ps1 exercises launcher diagnose mode, order self-test, settings self-test, dependency pruning, boundary Move Up/Move Down behavior, enabled selection persistence, profile enabled-selection persistence, dependency order repair, and non-standard manifest compatibility.
- Re-ran Tools\VerifyModTheSpire2Package.ps1: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D.
- Re-ran Tools\VerifyGitHubSourceExport.ps1: Status OK, file count 80, size 1.14 MB.
- No code or package files were changed in this pass; the remaining evidence gap is strictly the real in-game UI/restart flow on the clean build.

## 2026-06-21 22:55 Clean Restart Beta Release

- User reported the current clean restart version succeeded in manual testing and requested a clean release package plus GitHub/Workshop upload, with GitHub using a beta branch because this direction is not the Hot-Apply track.
- Re-ran Tools\CheckCleanRestartManualEvidence.ps1 after testing: INCOMPLETE only for TopRightCloseUsed. Logs prove clean build loaded, the ModTheSpire2 management overlay was shown, the restart dialog was shown and confirmed, the companion started the launcher, the launcher used wait-for-pid, detected game/settings, scanned mods, and launched selected mods.
- Re-ran Tools\VerifyModTheSpire2Package.ps1: Status OK, version 0.4.0, clean package 538.69 KB, DLL SHA256 A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D.
- Synchronized dist\WorkshopUpload\ModTheSpire2Content-Clean into ModUploader-win-x64\ModTheSpire2Workspace\content and confirmed it contains exactly five files.
- Initialized the clean GitHub source export as a Git repository, created branch beta, committed the clean restart manager source/export, and pushed to https://github.com/grassdog0/MTS2.git branch beta. Latest pushed commit is 1a128f5.
- Updated Tools\VerifyGitHubSourceExport.ps1 so a source export that is itself a Git repository may contain its root .git directory while still rejecting nested .git folders, bin/obj, logs, runtime data, snapshots, and test packages.
- Uploaded Steam Workshop item 3747911678 through ModUploader.exe successfully. Upload processed 551230 bytes and committed changes.
- Re-ran Tools\VerifyGitHubSourceExport.ps1 after the verifier adjustment: Status OK, file count 80, size 1.14 MB.
