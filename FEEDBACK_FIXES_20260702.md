# ModTheSpire2 Feedback Fixes - 2026-07-02

## Test Package

Clean manual-test package:

`C:\Users\HZDH\Desktop\tmp\Forimpro\dist\TestPackages\ModTheSpire2-20260702-2045-feedback-fixes`

Launcher SHA256:

`505739C8C2F044F04E958896ADB8AA5C07DE7C4BCCEFEF9755879676DAB5DB15`

The same launcher was synchronized to:

- `C:\Users\HZDH\Desktop\tmp\Forimpro\dist\WorkshopUpload\ModTheSpire2Content-Clean`
- `C:\Users\HZDH\Desktop\tmp\Forimpro\ModUploader-win-x64\ModTheSpire2Workspace\content`
- `E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2`

## Fixes In This Build

1. `settings.save` enabled-state reading is now object-scoped and exact-id based.
   - This should fix cases where enabling `TenshiHinanawi` could make a shorter id such as `Hina` appear enabled because the old parser matched substrings.

2. Vanilla launch no longer rewrites `mod_list`.
   - It now changes only the global `mods_enabled` flag.
   - Existing per-mod enabled flags and order are preserved so returning to modded launch keeps the user's previous layout.

3. Launch dependency order validation now checks selected mods only.
   - Disabled mods no longer block launch because of their dependency order.
   - Missing/unchecked dependencies are still enforced for selected mods.

4. Save buttons are clearer.
   - `Save Order` is now `Save Default`.
   - `Reset Order` is now `Reset Default`.
   - Status text explains that this saves the launcher default order and enabled selection; game settings are changed when launching with mods.

5. Profile saving is more predictable.
   - Saving a named profile keeps the current visible order and enabled selections.
   - The saved profile is selected after refresh.
   - Profile names now preserve Chinese/Unicode characters and only remove Windows-invalid filename characters.

## Verification

- Rebuilt `NativeLauncher\ModTheSpire2Launcher.c` with MinGW.
- Ran `ModTheSpire2Launcher.exe --self-test-order`.
- Ran `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`.
- Clean package verifier status: `OK`.

## Manual Test Focus

1. Create a new profile with an English and a Chinese name.
2. Restart launcher and confirm both profiles appear.
3. Select a profile and confirm it applies immediately.
4. Launch Vanilla, close game, reopen launcher, confirm previous mod order and enabled states are not lost.
5. Disable a dependency-related mod and confirm disabled mods do not cause dependency-order warnings.
6. Enable `TenshiHinanawi` while leaving any separate `Hina` mod disabled, then confirm the launcher does not auto-check `Hina`.
