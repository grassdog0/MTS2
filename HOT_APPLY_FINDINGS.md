# ModTheSpire2 Hot Apply Findings

## Current Answer

For the current Slay the Spire 2 build, ModTheSpire2 treats "hot-startable" as Stage A hot apply:

- Safe settings/config state can be saved during the current session.
- Already-loaded behavior may react if the game or the mod already supports runtime settings changes.
- Previously unloaded DLL or PCK mods are not true-loaded by ModTheSpire2 during the current session.

Stage B true hot-load remains unsupported because the inspected public game APIs do not expose a clear safe "load this one new mod now" path after the main menu is already running.

## Implemented Classification

Hot-Apply candidates:

- `ModTheSpire2` itself.
- Mods that explicitly declare `hot_apply` or `hot_reload` in their manifest.
- Utility/config manifests that do not include DLL/PCK startup payloads and do not affect gameplay.

Restart Required:

- DLL mods.
- PCK mods.
- Gameplay or unknown-behavior mods.
- Any mod with a missing dependency.
- Any hot-declared mod that depends on a restart-required mod, including transitive dependency chains.

## Rollback Behavior

Before hot apply, ModTheSpire2:

- Backs up the newest `settings.save` into `ModTheSpire2Data/hot-apply-backups`.
- Snapshots the in-memory `SettingsSave.ModSettings.ModList` state.
- Applies only selected hot-candidate state.
- Calls `SaveManager.Instance.SaveSettings()`.

If the operation fails or exceeds the 10-second safety limit, ModTheSpire2 restores the previous in-memory enabled state and saves it back.

Failed or unavailable hot-apply attempts also mark the attempted hot candidates as Restart Required for the current game session. This is an in-memory safety downgrade only; restarting the game clears it and lets ModTheSpire2 classify the mods again from manifests and dependency rules.

## Manual Load Order

The launcher provides:

- Move Up.
- Move Down.
- Save Order.
- Reset Order.

The saved order is stored in:

```text
ModTheSpire2Data/load-order.txt
```

During launcher startup, the saved order is applied before dependency sorting. Dependencies are kept before dependent mods. When launching, the launcher rewrites the game's `settings.save` `mod_list` order after creating a backup.

## Remaining Work

- Manual in-game visual verification of the ModTheSpire2 management window.
- Better UI styling using game-native controls where practical.
- Possible deeper integration into the original mod settings page.
- Localization after the English-only optimization phase stabilizes.
- Stage B true hot-load only if a safe game-supported API is found later.
