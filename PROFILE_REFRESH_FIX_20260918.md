# Profile selection and refresh correction

- Save targets a named preset only. Existing names update; new names create.
- Refresh reloads the selected preset, not the game settings by default.
- Current settings.save reads the game file; it is not a writable named preset.
- Vanilla recovery is labeled separately as Before Vanilla (recovery).
- Combo selection reads CB_GETLBTEXT instead of potentially stale edit text.
- Loading a preset starts from a fresh inventory, restores exact saved order and checks, and appends newly discovered mods deterministically. Missing enabled sidecars leave all checks off and display a diagnostic.
- Loading does not repair order or prune stored checks. Launch validation still checks requirements.
- Regression test repeats profile/current/profile reloads, verifies saved order and checks, discards unsaved edits, and compares game settings bytes before and after.

Manual acceptance: save two distinct presets A and B, repeatedly select A/B/A and Refresh, and verify identical results. Verify Save and Refresh leave settings.save untouched. Test a Vanilla roundtrip separately. This candidate is not published.
