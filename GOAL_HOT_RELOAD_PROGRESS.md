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

