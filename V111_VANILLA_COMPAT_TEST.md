# v0.111 Vanilla Compatibility Candidate

Date: 2026-08-18

## Problem

Slay the Spire 2 v0.111 no longer exposes the old top-level `mods_enabled` field in the current `settings.save`. The previous Vanilla flow only toggled that field and intentionally preserved every per-mod `is_enabled` value. After a Vanilla session, v0.111 can rewrite all mod entries as enabled, so the next modded launch loses the player's previous selection.

## Fix

- Save the current checked mod ids to `ModTheSpire2Data/enabled-mods.txt` before both Vanilla and modded launches.
- For Vanilla, write every discovered `mod_list` entry with `is_enabled: false` while preserving load order.
- Create `ModTheSpire2Data/vanilla-launch.pending` for the one-time Vanilla recovery state.
- On the next launcher opening, restore checkboxes from `enabled-mods.txt` when that marker exists.
- Save the pre-Vanilla order separately as `ModTheSpire2Data/vanilla-load-order.txt` and restore it with the saved checkboxes.
- Do not overwrite the player's normal `load-order.txt`; the Vanilla snapshot is temporary and is deleted after a modded launch.
- Clear the pending marker when a modded launch is written.

## Verification

- Native launcher compiled with MinGW without warnings.
- `--self-test-settings` passed in the build directory and the live v0.111 game mod directory.
- `--self-test-order` passed.
- Diagnostics completed and discovered 19 current local/Workshop mods.
- Full seven-file package verification passed.

## Manual Test

1. Open the launcher and select a nontrivial subset of installed mods.
2. Click `Vanilla` and confirm the game starts without mods.
3. Close the game and open the launcher again.
4. Confirm the previous subset is restored instead of every mod being checked.
5. Confirm the custom load order is restored too, rather than the game's default order.
6. Click `Launch Selected` and confirm only that subset loads in the restored order.

This candidate has replaced only the approved live ModTheSpire2 test launcher. It has not been uploaded to GitHub or Steam Workshop.
