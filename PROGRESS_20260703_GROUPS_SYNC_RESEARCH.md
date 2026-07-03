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
