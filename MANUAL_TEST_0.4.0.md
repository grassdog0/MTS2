# ModTheSpire2 0.4.0 Manual Test Checklist

Use this checklist for the clean launcher and restart-based mod manager build.

## Files To Test

Live local test folder:

```text
E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2
```

Expected manifest version:

```text
0.4.0
```

Expected companion log marker:

```text
Initialize ModTheSpire2 0.4.0-clean-restart-ui
```

Log file:

```text
E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ModTheSpire2\ModTheSpire2Data\companion.log
```

## Preflight Package Gate

Before manual in-game testing, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1
```

Expected result:

```text
Status  : OK
Version : 0.4.0
```

## Startup

1. Start the game from Steam, not by double-clicking `SlayTheSpire2.exe`.
2. Confirm the game reaches the main menu.
3. Open `companion.log` and confirm the clean-restart marker appears.
4. Confirm no `Initialize failed` or `Management dialog failed` line appears after that marker.

## In-Game Management Overlay

1. Open Settings / General / Mod Settings.
2. Confirm the `ModTheSpire2 Launcher` button appears.
3. Click it.
4. Confirm the overlay title is `ModTheSpire2 Management`.
5. Confirm the overlay explains that whole-mod enablement is managed before startup.
6. Confirm the top bar has:
   - `Close and Open Launcher`
   - top-right `X`
7. Confirm the overlay has these sections:
   - `Enabled For This Launch`
   - `Available But Disabled`
   - `Load Order`
8. Confirm the overlay shows a multiplayer mismatch report status panel.
9. If no multiplayer mismatch has been recorded, confirm the panel says no report exists yet.
10. Confirm the bottom buttons include:
   - `Close and Open Launcher`
   - `Open Missing Mod Links`
   - `Copy Mismatch Report`
   - `Close`
11. Confirm the overlay does not show player-facing Hot-Apply sections or buttons:
   - no `Runtime Hot-Apply`
   - no `Apply at Main Menu`
   - no `Apply Hot Changes`
12. Confirm rows show whether each mod is `Loaded`, `Enabled`, or `Disabled`.
13. Hover rows if possible and confirm tooltips distinguish `Enabled in settings` from `Loaded this session`.
14. Confirm rows/tooltips for mods with a minimum game version include `requires STS2 >= <version>`.
15. Resize the game window or test in a smaller window and confirm:
   - the dialog stays inside the visible game window
   - the mod list scrolls
   - `Close and Open Launcher`, `Close`, and the top-right `X` remain usable
16. Confirm the overlay closes through:
   - top-right `X`
   - `Close`
   - clicking the dark backstop outside the panel
   - Esc key
17. Check `companion.log` for:
   - `Management dialog shown`
   - `Load order section shown entries=`
   - `Management dialog closed:`

## Restart Dialog

1. Open the overlay and click `Close and Open Launcher`, but do not confirm until ready to close the game.
2. Confirm the restart confirmation uses the styled dark panel.
3. Confirm the Steam launch option appears in a framed text area.
4. Click `Copy Launch Option` and confirm the button changes to `Copied`.
5. Confirm `Cancel`, Esc, and clicking the dark backstop close the dialog without closing the game.
6. Confirm the first click on `Close and Open Launcher` only opens the confirmation dialog.
7. When ready, click `Close and Open Launcher` inside the confirmation dialog.
8. Confirm the game closes and the launcher opens after the game process exits.
9. Confirm this flow does not create two running Slay the Spire 2 instances.
10. If the game was started with an extra renderer flag such as `--rendering-driver opengl3`, confirm launching from the reopened launcher preserves that flag.

## Launcher Workflow

1. Start through `ModTheSpire2Launcher.exe` or the Steam launch option.
2. Confirm the launcher detects local and Workshop mods.
3. Confirm standard JSON mods, Workshop `mod_manifest.json` mods, legacy `pck_name` manifests, malformed payload-backed manifests, and explicit-id `.manifest` mods are covered by the verifier.
4. Confirm `Act4Heart`, `BaseLib`, `RitsuLib` / `STS2-RitsuLib`, and `wuwancients` are detected if they are installed locally.
5. Confirm dependent mods are grouped under their required mods when possible.
6. Confirm a dependent mod cannot be selected until its required mod is selected.
7. Confirm missing dependencies show visibly in the `Requirements` and `Status` columns.
8. Confirm too-old dependency versions are blocked.
9. Confirm too-new minimum game version requirements are blocked.
10. Confirm checked mods with satisfied dependencies show `Ready`.
11. Confirm unchecked selectable mods show `Available`.

## Load Order And Profiles

1. Before opening the launcher, change mod enabled state/order once through the game or another mod manager if available.
2. Open the launcher and confirm the initial order and checked mods match the current `settings.save`.
3. Confirm dependent mods can appear visually indented under earlier dependencies without changing the `settings.save` order.
4. Confirm there is no separate `Load Profile` button.
5. Confirm the profile combo shows `Current Game Settings`.
6. Check one independent mod and leave an adjacent independent mod unchecked.
7. Move the checked mod up or down.
8. Confirm the checkbox follows that mod, not the row number.
9. Select the first mod and click `Move Up`.
10. Confirm the launcher stays open and the order does not change.
11. Select the last mod and click `Move Down`.
12. Confirm the launcher stays open and the order does not change.
13. Select a mod, enter a natural number in `Row`, and click `Set`.
14. Confirm the mod moves to that row number.
15. Confirm values below 1 are treated as 1 and values above the mod count are treated as the last row.
16. If a move violates dependency order, confirm the launcher repairs it and reports that order was adjusted.
17. Click `Save Order`.
18. Confirm both files exist:

```text
ModTheSpire2Data\load-order.txt
ModTheSpire2Data\enabled-mods.txt
```

19. Try to save while the combo says `Current Game Settings`; confirm the launcher asks for a profile name instead of creating a default profile.
20. Type a named profile with custom order and selected mods, then click `Save Profile`.
21. Change both order and checked mods.
22. Select the named profile from the profile combo.
23. Confirm both order and checked mods are restored immediately.
24. Select `Current Game Settings` from the combo.
25. Confirm the list reloads from `settings.save`.
26. Confirm the profile sidecar exists:

```text
ModTheSpire2Data\order-profiles\<profile>.enabled.txt
```

## Vanilla Launch

1. Select some mods and save the order/profile.
2. Click `Vanilla`.
3. After the game starts, close it and start the launcher again.
4. Confirm the launcher default reflects the current `settings.save` vanilla state.
5. Confirm saved named profiles still exist and can be selected from the profile combo.

## Launch Selected

1. Select a dependency-valid mod set.
2. Click `Launch Selected`.
3. Confirm the game starts with the selected mods.
4. Close the game.
5. Reopen the launcher and confirm the same selected mods are shown because they are now in `settings.save`.

## Multiplayer Mismatch Helper

This helper is read-only and experimental. It must not bypass multiplayer checks or auto-subscribe Workshop items.

1. Prepare a multiplayer join attempt where the host and local player have different gameplay-relevant mod lists.
2. Join the host and trigger the game's mod mismatch error.
3. Confirm the game's error text includes a `ModTheSpire2 help` section.
4. Confirm the help text explains that the report is saved at:

```text
ModTheSpire2Data\multiplayer-mismatch-last.txt
```

5. Confirm the report file exists and includes:
   - `ModTheSpire2 multiplayer mismatch report`
   - missing local or host mods if the game exposed them
   - Workshop links when ModTheSpire2 could map them
   - `Raw NetErrorInfo`
6. Open ModTheSpire2 Management.
7. Confirm the mismatch status panel shows the latest report time and Workshop link count.
8. Click `Copy Mismatch Report` and confirm the report text is copied to the clipboard.
9. Click `Open Missing Mod Links` and confirm it opens only links from the latest report.
10. Confirm this flow does not subscribe automatically and does not force the join to continue.

## Package Content

The upload folder must contain exactly:

```text
ModTheSpire2.dll
ModTheSpire2.json
ModTheSpire2.pck
ModTheSpire2Launcher.exe
ModTheSpire2Launcher.sh
ModTheSpire2Launcher.command
README.md
```

It must not contain:

```text
ModTheSpire2Data
logs
snapshots
test packages
old Lite folders
temporary build output
```

## Linux / macOS Script Smoke Tests

These first-pass launchers are native Steam script entry points, not full GUI replacements.

1. On Linux or Steam Deck, set the launch option to:

```text
"/path/to/ModTheSpire2Launcher.sh" -- %command%
```

2. On macOS, set the launch option to:

```text
"/path/to/ModTheSpire2Launcher.command" -- %command%
```

3. If needed, make the scripts executable:

```sh
chmod +x "/path/to/ModTheSpire2Launcher.sh"
chmod +x "/path/to/ModTheSpire2Launcher.command"
```

4. Confirm the script reaches a menu or a clear actionable diagnostic error.
5. Confirm the menu offers Vanilla, saved enabled mods, named profiles if present, diagnostics, and quit.
6. Confirm launching Vanilla or saved enabled mods creates a timestamped `settings.save` backup in `ModTheSpire2Data/settings-backups`.
7. Confirm the game starts once through the original Steam command.
8. Confirm extra game launch arguments after `%command%` still reach the game where Steam supports them.

## Report Back

Send back:

- Whether each section appeared.
- Any crash or missing button.
- Whether the launcher opened correctly after `Close and Open Launcher`.
- Whether Linux/macOS scripts reached the menu or the exact diagnostic error shown.
- The last 80 lines of `companion.log`.
- The contents of `ModTheSpire2Data\load-order.txt` if load-order saving was tested.
