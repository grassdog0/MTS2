# Progress - 2026-07-03 Button/Profile/Exact-Id Fix Test Build

## Scope

Prepared a manual-test build from the current clean restart-manager direction. This build does not reintroduce broad hot-apply, draggable in-game entry controls, or unfinished grouping UI.

## Changes

- Refreshed the in-game Settings / Mod Settings `ModTheSpire2 Launcher` button when it already exists.
- Moved the in-game button parent to the `NModdingScreen` top-level node and raised its `ZIndex` to reduce Better Mod Menu overlay/input conflicts.
- Kept the button as the simple `Close and Open Launcher` entry.
- Updated launcher wording after load-order changes to point players to the single profile `Save` workflow.
- Updated the `Current settings.save` save warning text to match the actual label.
- Added `--self-test-settings` regression coverage for overlapping ids/names:
  - disabled `Hina`
  - enabled `TenshiHinanawi`
  - enabled `HinanawiHinaSkin`
  - partial id `Tenshi` must not match `TenshiHinanawi`

## Verification

- Built `NativeLauncher\ModTheSpire2Launcher.exe` with MinGW.
- Built `ModTheSpire2.dll` with Roslyn `csc` against the STS2 managed assemblies.
- `ModTheSpire2Launcher.exe --self-test-settings`: exit `0`
- `ModTheSpire2Launcher.exe --self-test-order`: exit `0`
- `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: `OK`

## Clean Package Hashes

- `ModTheSpire2.dll`: `BAE013C9C5E8D6BEB6E197CA2EF4B845A7868C4F82405FD2A06672A94388426A`
- `ModTheSpire2Launcher.exe`: `8A068C869A0F30618F2C386FC0B35E5B19EDB62037A1DA56761F475B0D9481B4`

## Manual Test Package

```text
dist\TestPackages\ModTheSpire2-0.4.0-button-profile-idfix-20260703-085306.zip
```

## Needs Manual Testing

- With Better Mod Menu enabled, open Settings / Mod Settings and confirm the `ModTheSpire2 Launcher` button is visible and clickable.
- Confirm clicking it opens the Close and Open Launcher dialog.
- Confirm creating a new profile with a typed name saves both order and enabled selections.
- Confirm Vanilla Launch still preserves normal modded order and enabled selections.
- Confirm Tenshi/Hina-style overlapping mod ids do not cause the wrong mod to be checked.

## 2026-07-03 Profile Save Self-Test Follow-Up

- Added hidden-list self-test coverage for the real `SaveNamedProfile()` path.
- The test now creates a typed profile name, saves order plus enabled selections, saves the same name again with a different enabled selection, reloads the sidecar enabled file, and verifies the update replaced the previous enabled state.
- The test deliberately chooses standalone selectable mods so dependency pruning does not make the assertion noisy.
- `ModTheSpire2Launcher.exe --self-test-order`: exit `0`, with `Named profile self-test passed` in `launcher.log`.
- `ModTheSpire2Launcher.exe --self-test-settings`: exit `0`.
- `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: `OK`.
- Updated manual test package:

```text
dist\TestPackages\ModTheSpire2-0.4.0-profile-save-selftest-20260703-090319.zip
```

- Updated clean package launcher hash:
  `E1D0E81228522DFD883A690257E9C9CB311D6FA553BB91694479362B929E0088`

## 2026-07-03 Delayed Button Refresh Follow-Up

- Added delayed in-game Mod Settings button refresh passes at roughly 0.05s, 0.15s, and 0.35s after insertion.
- Each delayed pass revalidates the screen, finds the existing `ModTheSpire2 Launcher` button, keeps it visible/enabled, moves it to the end of its parent, and keeps `ZIndex` at 5000.
- This is intended to handle Better Mod Menu or other UI mods adding overlay controls shortly after `NModdingScreen._Ready` / `OnSubmenuOpened`.
- The button still opens only the simple `Close and Open Launcher` dialog; no draggable entrance UI or broad hot-apply was added.
- Rebuilt `ModTheSpire2.dll`.
- `ModTheSpire2Launcher.exe --self-test-order`: exit `0`.
- `ModTheSpire2Launcher.exe --self-test-settings`: exit `0`.
- `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: `OK`.
- Updated manual test package:

```text
dist\TestPackages\ModTheSpire2-0.4.0-delayed-button-refresh-20260703-090836.zip
```

- Updated clean package DLL hash:
  `FB0D0DF59DF8450F4A788CD0641A6A411DA0EDB2C7A62C8B1B4F74D1AC56698A`

## 2026-07-03 Vanilla Preserve Self-Test Follow-Up

- Added settings self-test coverage for the Vanilla launch invariant.
- The test writes a fixture `settings.save`, captures the exact `mod_list` byte span, calls the real `WriteSettings(FALSE)` path, then verifies:
  - `mods_enabled` changes from `true` to `false`;
  - the `mod_list` byte span is unchanged exactly;
  - per-mod order and enabled state are therefore preserved for the next modded launch.
- `ModTheSpire2Launcher.exe --self-test-settings`: exit `0`, with `Vanilla launch: preserving mod_list order and per-mod enabled states` in `launcher.log`.
- `ModTheSpire2Launcher.exe --self-test-order`: exit `0`.
- `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: `OK`.
- Updated manual test package:

```text
dist\TestPackages\ModTheSpire2-0.4.0-vanilla-preserve-selftest-20260703-091436.zip
```

- Updated clean package launcher hash:
  `B34F7F63F177A2F7EF115B9C3A309117827BE6E4B4372E8C9733719EF2CA0CAE`

## 2026-07-03 Selected-Only Dependency Validation Self-Test

- Added order self-test coverage for the selected-only dependency validation rule.
- The test creates an intentionally invalid global BaseLib/QuickRestart order while both mods are unchecked, then verifies:
  - full dependency validation detects the bad disabled order;
  - selected-only dependency validation allows launch because the conflicting mods are not selected.
- This protects the launcher from blocking launch because of disabled mods with dependency/order issues.
- `ModTheSpire2Launcher.exe --self-test-order`: exit `0`, with `Selected-only dependency validation self-test passed` in `launcher.log`.
- `ModTheSpire2Launcher.exe --self-test-settings`: exit `0`.
- `Tools\VerifyModTheSpire2Package.ps1 -SkipLive`: `OK`.
- Updated manual test package:

```text
dist\TestPackages\ModTheSpire2-0.4.0-selected-only-deps-selftest-20260703-092049.zip
```

- Updated clean package launcher hash:
  `3B11D11860DC9D01E2E1AB927C2B9E975688891B885AE88D2005CF7C5365A3C2`
