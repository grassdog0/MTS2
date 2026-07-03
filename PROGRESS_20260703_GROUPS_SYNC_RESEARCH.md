# Progress 2026-07-03 - Grouping And Multiplayer Sync Research

## Baseline

- Published baseline remains the clean restart-manager workflow.
- GitHub branch: `cross-platform-launcher`
- Published code baseline before this work: `f250fe5`
- Goal docs baseline update pushed as: `2f5ab3c`

## Launcher Group Column Prototype

Implemented a low-risk launcher grouping prototype in `NativeLauncher/ModTheSpire2Launcher.c`.

Behavior:

- Adds a read-only `Group` column to the Windows launcher.
- Does not write Better Mod Menu files.
- Does not change `settings.save`, profiles, enabled state, dependency checks, or launch order.
- Tries to import Better Mod Menu groups from:
  `%APPDATA%\SlayTheSpire2\steam\<steamid>\mod_data\BetterModMenu\mod_profiles.json`
- If Better Mod Menu has no usable `ModGroups`, falls back to ModTheSpire2 simple categories:
  - Dependencies / libraries
  - Gameplay content
  - UI / QoL
  - Cosmetic
  - Utility / tools
  - Unknown

Local evidence:

- Local Better Mod Menu `mod_profiles.json` exists.
- `CustomGroups` contains one test group, but `ModGroups` is `{}`, so no per-mod grouping can currently be imported.
- Diagnostic logs confirm fallback categories are applied:
  - `BaseLib` and `RitsuLib` -> `Dependencies / libraries`
  - `TenshiHinanawi` -> `Cosmetic`
  - gameplay-affecting mods -> `Gameplay content`
  - utility/menu/restart/save/config mods -> `UI / QoL` or `Utility / tools`

## Verification

Commands run:

```powershell
x86_64-w64-mingw32-gcc NativeLauncher\ModTheSpire2Launcher.c -municode -mwindows -O2 -Wall -Wextra -o NativeLauncher\ModTheSpire2Launcher.exe -lcomctl32 -lshell32 -lole32 -luuid -luxtheme
```

```powershell
.\NativeLauncher\ModTheSpire2Launcher.exe --self-test-order -- "E:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe"
```

Result: `order_exit=0`

```powershell
.\NativeLauncher\ModTheSpire2Launcher.exe --self-test-settings -- "E:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe"
```

Result: `settings_exit=0`

Package checks:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

Result: `Status OK`, clean package about `584.46 KB`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1 -SourceExport dist\Release\ModTheSpire2-0.4.0-CrossPlatform-GitHubSource
```

Result: `Status OK`.

## Multiplayer Sync Findings So Far

Current evidence supports a cautious helper, not force bypass.

Observed useful data sources:

- Game logs show Workshop scan and loaded mod details.
- RitsuLib telemetry under player mod data contains a detailed mod inventory with:
  - id
  - name
  - version
  - state
  - source
  - affects_gameplay
  - assembly info
- Better Mod Menu exports can include Workshop links.
- `sts2.xml` documents `ModManager.GetGameplayRelevantModNameList`, described as the list of loaded gameplay-affecting mod names used for multiplayer comparison.
- `sts2.xml` documents `ConnectionFailureReason.ModMismatch`.
- `sts2.xml` documents `ConnectionFailureExtraInfo.missingModsOnLocal` and `missingModsOnHost`.
  - `missingModsOnLocal`: mods the host has and the local player is missing.
  - `missingModsOnHost`: mods the local player has and the host is missing.
- `sts2-heybox-support.dll` contains strings showing it patches:
  - `ModManager.GetGameplayRelevantModNameList`
  - `JoinFlow.Begin`
  - server-side mod detection
  This proves bypass is technically possible, but it also confirms bypass is invasive and should not be the default MTS2 direction.

Not yet proven:

- A public stable way for MTS2 to receive `ConnectionFailureExtraInfo` without patching or hooking multiplayer UI/game flow.
- Whether the missing mod lists contain mod ids, display names, or both.
- Whether the missing mod lists contain Workshop ids or only names.
- A safe way to bypass the game's multiplayer mismatch checks.
- A safe automatic subscribe path through Steamworks from this launcher.

Recommended next step:

1. Build an in-game read-only hook around the disconnect/error UI or `LocalPlayerDisconnected(NetErrorInfo)` path.
2. Capture only `ModMismatch` cases and display `missingModsOnLocal` / `missingModsOnHost`.
3. Map missing names to local/Workshop metadata when possible:
   - Better Mod Menu exports may provide Workshop links for known names/ids.
   - Local manifests provide names, ids, versions, and Workshop folder ids for subscribed mods.
   - If no Workshop id is known, show the name and let the player search manually.
4. If a host list can be read safely, implement a read-only mismatch report first:
   local missing mods, version differences, Workshop links when known, and a restart prompt.
5. Do not implement force-join bypass until exact check points and failure modes are known.

## 2026-07-03 10:25 - Grouping Refinement

Implemented a small read-only grouping refinement in `NativeLauncher/ModTheSpire2Launcher.c`.

Behavior:

- Better Mod Menu JSON `ModGroups` remains the first grouping source.
- If JSON groups are unavailable, the launcher now tries Better Mod Menu CSV exports with columns such as:
  - `Mod Id`
  - `Name`
  - `Group`
  - `Workshop Link`
- CSV matching is read-only and can match by exact mod id, Workshop id extracted from the link, or unique display name.
- Imported CSV groups only fill empty group fields and do not write Better Mod Menu files.
- Grouping still falls back to ModTheSpire2 categories when no BMM group data exists.

Verification added:

- `RunGroupingSelfTest` writes a temporary minimal CSV under `ModTheSpire2Data`, imports one test group, then deletes it.
- The self-test verifies that group import applies to the expected mod.
- The self-test verifies that applying groups does not change the current mod order.

Commands run:

```powershell
x86_64-w64-mingw32-gcc NativeLauncher\ModTheSpire2Launcher.c -municode -mwindows -O2 -Wall -Wextra -o NativeLauncher\ModTheSpire2Launcher.exe -lcomctl32 -lshell32 -lole32 -luuid -luxtheme
```

```powershell
.\NativeLauncher\ModTheSpire2Launcher.exe --self-test-order -- "E:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe"
```

Result: `order_exit=0`, with `Grouping self-test passed` in the launcher log.

```powershell
.\NativeLauncher\ModTheSpire2Launcher.exe --self-test-settings -- "E:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe"
```

Result: `settings_exit=0`.

Current launcher candidate:

- SHA256: `3F9C5809BA2107B8156ADD208EB01F0584E91DA93057DF1951A51507B5C55066`
- Synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`

This is a test candidate, not a manually accepted Workshop release yet.

## 2026-07-03 11:42 - Mismatch Report Status In Management

Improved the ModTheSpire2 management dialog so players can see whether a multiplayer mismatch report is available before pressing action buttons.

Behavior:

- Added `MultiplayerMismatchActions.GetLastReportStatus()`.
- The management dialog now displays a small status panel below the game-state summary.
- If a report exists, it shows:
  - last modified timestamp;
  - number of Workshop links found;
  - a short instruction to use the buttons below.
- If no report exists, it tells the player to trigger a mismatch once and reopen the panel.

Safety:

- Read-only status display.
- Reads only `ModTheSpire2Data\multiplayer-mismatch-last.txt`.
- Does not alter settings, subscriptions, lobby state, or multiplayer checks.

Verification:

```powershell
dotnet C:\Program Files\dotnet\sdk\8.0.402\Roslyn\bincore\csc.dll ... ModTheSpire2Entry.HotManage.cs
```

Result: compiled.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1 -SourceExport dist\Release\ModTheSpire2-0.4.0-CrossPlatform-GitHubSource
```

Result: `Status OK`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

Result: `Status OK`, clean package about `617.18 KB`.

Current DLL candidate:

- SHA256: `76B2B0101518552DDB7950F252B9F7CD38F4C4CB63C4AA389FED49F8932E9522`
- Synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`

This is a test candidate, not a manually accepted Workshop release yet.

## 2026-07-03 11:32 - Actionable Mismatch Help Text

Improved the player-facing multiplayer mismatch help text.

Behavior:

- The ModMismatch appended help now tells players that the full report is saved at:
  `ModTheSpire2Data\multiplayer-mismatch-last.txt`
- It tells players to open ModTheSpire2 Management and use:
  - `Open Missing Mod Links`
  - `Copy Mismatch Report`
- The saved report also includes a short instruction block before the full details.

Safety:

- Text-only change.
- Does not alter network checks, subscriptions, mod state, or launcher flow.

Verification:

```powershell
dotnet C:\Program Files\dotnet\sdk\8.0.402\Roslyn\bincore\csc.dll ... ModTheSpire2Entry.HotManage.cs
```

Result: compiled.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1 -SourceExport dist\Release\ModTheSpire2-0.4.0-CrossPlatform-GitHubSource
```

Result: `Status OK`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

Result: `Status OK`, clean package about `613.68 KB`.

Current DLL candidate:

- SHA256: `77F7F69ACD453CFA18C02FA2687CFA5D91CDEB56D54B11D46473B65422FDD0B6`
- Synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`

This is a test candidate, not a manually accepted Workshop release yet.

## 2026-07-03 11:26 - Mismatch Actions In ModTheSpire2 Management

Made the multiplayer mismatch helper available from ModTheSpire2's own management dialog, not only from BaseLib ModConfig.

Behavior:

- Added `Open Missing Mod Links` to the ModTheSpire2 management dialog button area.
- Added `Copy Mismatch Report` to the ModTheSpire2 management dialog button area.
- These buttons call the same safe actions as the BaseLib config buttons:
  - `MultiplayerMismatchActions.OpenLastWorkshopLinks`
  - `MultiplayerMismatchActions.CopyLastReport`

Safety:

- The buttons only read `ModTheSpire2Data\multiplayer-mismatch-last.txt`.
- They either open Workshop links already recorded in that report or copy the report to clipboard.
- They do not change enabled mods, subscriptions, lobby state, or multiplayer checks.

Verification:

```powershell
dotnet C:\Program Files\dotnet\sdk\8.0.402\Roslyn\bincore\csc.dll ... ModTheSpire2Entry.HotManage.cs
```

Result: compiled.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1 -SourceExport dist\Release\ModTheSpire2-0.4.0-CrossPlatform-GitHubSource
```

Result: `Status OK`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

Result: `Status OK`, clean package about `613.18 KB`.

Current DLL candidate:

- SHA256: `3438512D8BC3C81D6DB6E034682DF66F8646968F10A9909A16B6D537A635D4CC`
- Synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`

This is a test candidate, not a manually accepted Workshop release yet.

## 2026-07-03 11:18 - Copy Mismatch Report Action

Added a feedback-oriented action for multiplayer mismatch testing.

Behavior:

- Added `MultiplayerMismatchActions.CopyLastReport()`.
- It reads `ModTheSpire2Data\multiplayer-mismatch-last.txt`.
- If the report exists and is non-empty, it copies the full report to the clipboard.
- If no report exists yet, it tells the player to trigger a multiplayer mismatch once first.
- Added a `Copy Mismatch Report` button to the BaseLib ModConfig UI.
- Added dynamic config method `CopyMismatchReportFromConfig`.
- Reduced BaseLib config button width slightly so three buttons fit the row more reliably.

Safety:

- This only reads the latest MTS2 report and writes to the clipboard.
- It does not alter game settings, subscriptions, lobby state, or network checks.

Verification:

```powershell
dotnet C:\Program Files\dotnet\sdk\8.0.402\Roslyn\bincore\csc.dll ... ModTheSpire2Entry.HotManage.cs
```

Result: compiled.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1 -SourceExport dist\Release\ModTheSpire2-0.4.0-CrossPlatform-GitHubSource
```

Result: `Status OK`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

Result: `Status OK`, clean package about `612.68 KB`.

Current DLL candidate:

- SHA256: `FE5187A41BA92C77E0ECA901FF53538A187C65CFA37B6586735B1A7015D10E68`
- Synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`

This is a test candidate, not a manually accepted Workshop release yet.

## 2026-07-03 11:10 - Open Missing Workshop Links Action

Added a safe player action for the multiplayer mismatch helper.

Behavior:

- Added `MultiplayerMismatchActions` in `ModTheSpire2Companion/ModTheSpire2Entry.HotManage.cs`.
- It reads the latest `ModTheSpire2Data\multiplayer-mismatch-last.txt` report.
- It extracts unique Steam Workshop links from that report.
- It opens at most 12 links to avoid flooding Steam/browser windows.
- If no report or links exist, it shows an explanatory message telling the player to trigger a mismatch once first.
- Added an `Open Missing Mod Links` button to the BaseLib ModConfig UI.
- Added a dynamic config button method `OpenMismatchWorkshopLinksFromConfig`.

Safety:

- This does not auto-subscribe.
- This does not bypass multiplayer mismatch checks.
- This does not alter lobby/network/game state.
- It only opens links that ModTheSpire2 already recorded in the last mismatch report.

Verification:

```powershell
dotnet C:\Program Files\dotnet\sdk\8.0.402\Roslyn\bincore\csc.dll ... ModTheSpire2Entry.HotManage.cs
```

Result: compiled.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1 -SourceExport dist\Release\ModTheSpire2-0.4.0-CrossPlatform-GitHubSource
```

Result: `Status OK`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

Result: `Status OK`, clean package about `611.68 KB`.

Current DLL candidate:

- SHA256: `E5296A0DD7FF771F00C28FFE6039C2E89A0CAF1813DACCF83A38E3D443D87ABE`
- Synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`

This is a test candidate, not a manually accepted Workshop release yet.

## 2026-07-03 11:00 - Mismatch Resolver Regression Guard

Added regression coverage for the Workshop-link resolver path.

Changes:

- Added `MismatchModResolver.SelfTest()` in `ModTheSpire2Companion/ModTheSpire2Entry.HotManage.cs`.
- The self-test constructs a tiny in-memory mod index and verifies matching by:
  - mod id;
  - display name;
  - Workshop URL / Workshop id;
  - unknown Workshop URL fallback.
- Added static verification gates to `Tools\VerifyModTheSpire2Package.ps1` so the mismatch helper, report file, resolver, Workshop URL mapping, and self-test method cannot be removed silently.

Note:

- I tried a standalone PowerShell reflection runner for `SelfTest()`, but it is not portable in this workspace because the compiled companion targets the game's .NET 9 runtime and the host PowerShell cannot load that `System.Private.CoreLib` graph directly.
- The retained verification path is source gate plus normal companion DLL compilation and package verification.

Verification:

```powershell
dotnet C:\Program Files\dotnet\sdk\8.0.402\Roslyn\bincore\csc.dll ... ModTheSpire2Entry.HotManage.cs
```

Result: compiled.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1 -SourceExport dist\Release\ModTheSpire2-0.4.0-CrossPlatform-GitHubSource
```

Result: `Status OK`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

Result: `Status OK`, clean package about `607.68 KB`.

Current DLL candidate:

- SHA256: `3B4FF50CB6DA548E831887F80A15A29303F263735E1AFFB43B15F5304C9EB65B`
- Synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`

This is a test candidate, not a manually accepted Workshop release yet.

## 2026-07-03 10:50 - Multiplayer Mismatch Workshop Link Mapping

Extended the read-only multiplayer mismatch helper in `ModTheSpire2Companion/ModTheSpire2Entry.HotManage.cs`.

Behavior:

- The mismatch helper now builds a local/subscribed mod index with `ModScanner.Discover()`.
- Missing mod tokens from `missingModsOnLocal` / `missingModsOnHost` are resolved by:
  - exact mod id;
  - exact mod name;
  - Workshop id extracted from a token or Workshop URL;
  - normalized id/name fallback.
- Resolved entries are displayed as:
  `Name [Id] | version X | https://steamcommunity.com/sharedfiles/filedetails/?id=...`
- The saved `multiplayer-mismatch-last.txt` report now includes a local/subscribed mod index for debugging and player feedback.
- This remains informational only. It does not subscribe to Workshop items, force join, or alter network checks.

Verification:

```powershell
dotnet C:\Program Files\dotnet\sdk\8.0.402\Roslyn\bincore\csc.dll ... ModTheSpire2Entry.HotManage.cs
```

Result: compiled.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1 -SourceExport dist\Release\ModTheSpire2-0.4.0-CrossPlatform-GitHubSource
```

Result: `Status OK`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

Result: `Status OK`, clean package about `606.68 KB`.

Current DLL candidate:

- SHA256: `72040AF9A76EA594D1ADDFDD662078DBBEEA7D5339B035B098666DD9D3F8CCD2`
- Synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`

This is a test candidate, not a manually accepted Workshop release yet.

## 2026-07-03 10:38 - Read-Only Multiplayer Mismatch Helper

Investigated multiplayer mismatch surfaces.

Evidence:

- `sts2.xml` documents `ConnectionFailureExtraInfo.missingModsOnLocal` and `missingModsOnHost`.
- `sts2.xml` documents `NetError.ModMismatch` and `NetErrorInfo.GetErrorString`.
- `sts2.xml` documents `JoinFlow.Begin(...)` throwing `ClientConnectionFailedException` on join failure.
- Local logs did not contain a captured real mismatch case yet.
- Direct PowerShell reflection against the game runtime is noisy because of runtime/type-load differences, so implementation must stay defensive.

Implemented a low-risk read-only helper in `ModTheSpire2Companion/ModTheSpire2Entry.HotManage.cs`.

Behavior:

- Adds a Harmony postfix target for `MegaCrit.Sts2.Core.Entities.Multiplayer.NetErrorInfo.GetErrorString`.
- If the error appears to be `ModMismatch`, appends a `ModTheSpire2 help` section to the existing game error text.
- Does not bypass multiplayer checks.
- Does not alter `JoinFlow`, lobby state, network messages, or mod lists.
- Attempts to reflectively find `missingModsOnLocal` and `missingModsOnHost` anywhere inside the error-info object graph.
- If exact missing mod names are unavailable, it still explains that the host/local gameplay mod lists differ.
- Writes the latest report to:
  `ModTheSpire2Data\multiplayer-mismatch-last.txt`

Limitations:

- Needs a real multiplayer mismatch manual test to confirm whether STS2 exposes missing mod names through `NetErrorInfo` at this hook point.
- The helper is intentionally informational only. It does not auto-subscribe Workshop items and does not force join.

Verification:

```powershell
dotnet C:\Program Files\dotnet\sdk\8.0.402\Roslyn\bincore\csc.dll ... ModTheSpire2Entry.HotManage.cs
```

Result: compiled.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1 -SkipLive
```

Result: `Status OK`, clean package about `602.18 KB`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyGitHubSourceExport.ps1 -SourceExport dist\Release\ModTheSpire2-0.4.0-CrossPlatform-GitHubSource
```

Result: `Status OK`.

Current DLL candidate:

- SHA256: `E8B06CEC359AFEBC7740B8DEE0866089CA12C50EC6CA6146280B6CD197AFD104`
- Synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`

This is a test candidate, not a manually accepted Workshop release yet.
