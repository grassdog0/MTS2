# ModTheSpire2 0.4.0 Manual Test Checklist

Use this checklist after launching Slay the Spire 2 normally from Steam.

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
Initialize ModTheSpire2 0.4.0-hot-order-ui
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
3. Open the companion log and confirm the 0.4.0 marker appears.
4. Confirm no `Initialize failed` or `Management dialog failed` line appears after the 0.4.0 marker.

## Built-In Mod Settings Entry

1. Open Settings / General / Mod Settings.
2. Confirm the `ModTheSpire2 Launcher` button appears.
3. Confirm the button uses the same dark/gold styled treatment as the ModTheSpire2 overlay buttons.
4. Click it.
5. Confirm the in-game overlay appears with these sections:
   - Hot-Apply candidates
   - Restart Required
   - Load Order
6. Confirm the overlay can close through:
   - Close button
   - Clicking the dark backstop outside the panel
   - Esc key
7. Check `companion.log` for:
   - `Management dialog shown`
   - `Load order section shown entries=`
   - `Management dialog closed: button`
   - `Management dialog closed: backstop`
   - `Management dialog closed: escape`
8. While the overlay is already open, click the ModTheSpire2 button again if it is still reachable, or open it twice from another entry.
9. Confirm a second overlay does not stack on top of the first.
10. Check `companion.log` for:
   - `Management dialog already open; focused existing overlay`

## Overlay Style

1. Confirm the overlay uses the styled dark panel, gold section headers, framed rows, and styled buttons.
2. Confirm Hot-Apply candidates are shown as checkbox rows, not loose default text.
3. Confirm Restart Required mods are shown as framed text rows.
4. Confirm the Load Order list and Move/Save/Reset buttons match the same visual style.
5. Resize the game window if possible and confirm the panel remains centered, scrollable, and without text overlap.

## Restart Dialog Style

1. Open the ModTheSpire2 overlay and click `Close and Open Launcher`, but do not confirm yet unless you are ready to close the game.
2. Confirm the restart confirmation uses the styled dark panel instead of the default Godot confirmation dialog.
3. Confirm the Steam launch option appears in a framed text area.
4. Click `Copy Launch Option` and confirm the button changes to `Copied`.
5. Confirm `Cancel`, Esc, and clicking the dark backstop close the dialog without closing the game.
6. Confirm the first click on the overlay's `Close and Open Launcher` button does not immediately close the game; it should only open this confirmation dialog.
7. When ready, click `Close and Open Launcher` inside the confirmation dialog and confirm the game closes and the launcher opens.

## Hot Apply

1. In the overlay, confirm Hot-Apply candidates only include config/utility mods that do not require startup patches.
2. Confirm `ModTheSpire2` appears under Restart Required with a launcher/UI patch restart reason.
3. Confirm DLL/PCK mods such as BaseLib and QuickRestart appear under Restart Required.
4. Confirm the management overlay does not show standalone `Open Launcher` or `Copy Launch Option` buttons.
5. Click `Apply Hot Changes` without changing anything.
6. Confirm the game stays open and the management overlay refreshes.
7. Toggle a hot-apply candidate off or on and click `Apply Hot Changes`.
8. Confirm the overlay reopens with the updated checkbox state.
9. Leave and re-enter the game's Mod Settings page if the native Installing Mods list does not immediately repaint; ModTheSpire2 attempts a native refresh, but the game may only rebuild that list when the page is reopened.
10. Confirm a backup appears under:

```text
ModTheSpire2Data\hot-apply-backups
```

11. If a hot-apply attempt fails or reports that settings are unavailable, confirm the overlay closes and reopens automatically.
12. Confirm the affected hot candidates immediately move to Restart Required for the current session with a `hot apply failed this session` or `hot apply unavailable this session` reason.
13. Restarting the game should clear this session downgrade and classify the mods again from manifests/dependencies.

## Load Order

1. Open the overlay and find the Load Order list.
2. Select an enabled mod that is not blocked by dependency rules.
3. Click Move Up or Move Down.
4. Click Save Order.
5. Confirm this file exists:

```text
ModTheSpire2Data\load-order.txt
```

6. Confirm it contains one mod id per line.
7. Try moving QuickRestart above BaseLib if both are enabled.
8. Confirm the UI refuses or repairs that dependency-breaking move.
9. Click Reset Order.
10. Confirm `load-order.txt` is removed.

## BaseLib ModConfig Entry

Only test this if BaseLib is enabled.

1. Open BaseLib's ModConfiguration page.
2. Confirm there is a ModTheSpire2 entry or page.
3. Confirm the ModTheSpire2 fallback entry and config-page buttons use the same dark/gold button style when the native BaseLib button style is not available.
4. Open it.
5. Confirm `Open Management` opens the same overlay.
6. Confirm `Copy Launch Option` copies the Steam launch option.

## Launcher Regression

1. Close the game.
2. Start through `ModTheSpire2Launcher.exe` or the Steam launch option.
3. Confirm the launcher still detects mods.
4. Confirm BaseLib and QuickRestart are grouped with QuickRestart under BaseLib.
5. Check one independent mod and leave the adjacent independent mod unchecked.
6. Move the checked mod up or down.
7. Confirm the checkbox follows that mod, not the row number.
8. Select the first mod and click `Move Up`.
9. Confirm the launcher stays open and the order does not change.
10. Select the last mod and click `Move Down`.
11. Confirm the launcher stays open and the order does not change.
12. Select an independent mod and click `Move Up`, then `Move Down`.
13. Confirm the mod swaps with the adjacent entry and the launcher stays open.
14. Select a mod, enter a natural number in `Order`, and click `Apply`.
15. Confirm the mod moves to that row number. Values below 1 are treated as 1; values above the mod count are treated as the last row.
16. Click `Save Order`.
17. Confirm both files exist:

```text
ModTheSpire2Data\load-order.txt
ModTheSpire2Data\enabled-mods.txt
```

18. Confirm `enabled-mods.txt` contains only the checked mod ids.
19. Confirm dependency order is still valid. A dependency may load before a dependent with unrelated independent mods between them.
20. Click `Vanilla`.
21. After the game starts, close it and start the launcher again.
22. Confirm the launcher still restores the previously saved checked mods. Vanilla is a one-time launch mode and should not clear `enabled-mods.txt`.
23. Type a profile name in `Profile`, click `Save`, then select it and click `Load`.
24. Confirm the saved named order can be reused.
25. Launch selected mods.
26. Confirm the game starts.

Automated launcher order tests are also part of:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\VerifyModTheSpire2Package.ps1
```

## Report Back

Send back:

- Whether each section appeared.
- Any crash or missing button.
- The last 80 lines of `companion.log`.
- The contents of `ModTheSpire2Data\load-order.txt` if load-order saving was tested.
