# Test Build - Restore Modsetting Button And Save Profile Flow

## Package

`C:\Users\HZDH\Desktop\tmp\Forimpro\dist\TestPackages\ModTheSpire2-20260702-restore-button-save-profile`

## Hashes

- `ModTheSpire2.dll`: `6774F091566CCE0472B7F10966F78BE4DA8566D8FF37AB4595B53B7009D72683`
- `ModTheSpire2Launcher.exe`: `D1D3E322635F11C9E456550EAE4CEBEA38C7D45C1325EEC8F121AB36CDFCA109`

## Changes

1. Restored the Settings / Mod Settings button to the older stable flow:
   - The button opens the Close and Open Launcher confirmation directly.
   - It no longer opens the larger ModTheSpire2 management overlay from that screen.
   - Button layering was reduced from the experimental high `ZIndex` path to the older stable value.

2. Cleaned the launcher profile save flow:
   - Removed `Save Default`, `Reset Default`, and separate `Save Profile` wording.
   - Added a single `Save` button.
   - Select an existing profile or type a new profile name, then click `Save`.
   - If the profile already exists, it is updated.
   - If it does not exist, it is created.
   - The live settings entry is now labeled `Current settings.save`.

3. Build cleanup:
   - `ModTheSpire2Companion.csproj` now compiles only `ModTheSpire2Entry.HotManage.cs`.
   - Because the game assemblies target .NET 9 while the installed SDK is .NET 8, the companion DLL was built with Roslyn directly against the game's managed DLLs.

## Verification

- Native launcher compiled with MinGW.
- Companion DLL compiled successfully.
- Clean package verifier passed.
- Updated files were synced to:
  - `dist\WorkshopUpload\ModTheSpire2Content-Clean`
  - `ModUploader-win-x64\ModTheSpire2Workspace\content`
  - `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2`

## Manual Test Focus

1. Enable Better Mod Menu and open Settings / Mod Settings.
2. Confirm the ModTheSpire2 button can be clicked.
3. Confirm clicking it opens the Close and Open Launcher confirmation.
4. In the launcher, confirm the profile dropdown first item is `Current settings.save`.
5. Type a new profile name and click `Save`; reopen launcher and confirm it appears.
6. Select an existing profile, change order/enabled mods, click `Save`; reopen and confirm it updated.
