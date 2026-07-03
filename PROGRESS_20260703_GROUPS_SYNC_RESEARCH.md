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
