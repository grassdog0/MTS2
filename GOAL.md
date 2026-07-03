# ModTheSpire2 Goal Mode Entry

Use this file as the entry prompt for the next Goal-mode run.

## Goal Prompt

Continue ModTheSpire2 development by strictly following:

```text
C:\Users\HZDH\Desktop\tmp\Forimpro\GOAL_NEXT_STAGE_GROUPS_AND_MULTIPLAYER_SYNC.md
```

Also maintain the progress log:

```text
C:\Users\HZDH\Desktop\tmp\Forimpro\GOAL_HOT_RELOAD_PROGRESS.md
```

## Current Published Baseline

The current public baseline has already been uploaded to both GitHub and Workshop.

- GitHub: `https://github.com/grassdog0/MTS2`
- Branch: `cross-platform-launcher`
- Commit: `f250fe5 Fix launcher profile and mod settings compatibility`
- Workshop item: `3747911678`
- Verified clean package: `dist\WorkshopUpload\ModTheSpire2Content-Clean`
- Timestamped backup package: `dist\TestPackages\ModTheSpire2-0.4.0-current-upload-20260703-093016.zip`

Treat this uploaded build as the stable rollback point. If local source contains experimental grouping, multiplayer, hot-apply, or draggable UI work, compare it against the published baseline before continuing and do not ship unverified experimental behavior.

## Next Direction

The next stage should preserve the clean restart-manager direction:

- Whole-mod enablement is changed before startup through the launcher.
- The in-game Settings / Mod Settings entry remains a simple `Close and Open Launcher` helper.
- Do not bring broad whole-mod Hot-Apply back as the default workflow.
- Do not reintroduce draggable in-game entrance controls unless the user explicitly reopens that feature.

Prioritize the currently reported issues before larger new features:

- Preserve the refreshed ModTheSpire2 button behavior on Settings / Mod Settings. It should remain visible and clickable when other mod-setting UI layers, including Better Mod Menu, are present.
- Keep launcher startup state tied to the actual current `settings.save`: enabled state and load order should reflect the game, RitsuLib, Better Mod Menu, or vanilla mod manager output.
- Preserve user-defined profiles. The launcher has one clear `Save` action: if the typed/selected profile name exists, update it; otherwise create it.
- Preserve both enabled selections and load order in named profiles.
- Keep Vanilla Launch as a temporary launch mode that does not erase the normal saved mod selection or rearrange the stored mod order.
- Run dependency validation only on selected/enabled mods.
- Preserve exact-id parsing for `settings.save`, especially overlapping names/ids such as `Hina` and `TenshiHinanawi`.
- Clarify and preserve the role of `Default`: it should not replace the live `Current settings.save` view, and it should not disappear unless the user explicitly deletes or overwrites it.

The next active work should be grouping and multiplayer synchronization as researched, low-risk features:

- Better Mod Menu grouping should be optional and read-only at first.
- If Better Mod Menu group data is unavailable, fall back to ModTheSpire2's own simple categories.
- Multiplayer mod sync should first focus on showing host/local differences and opening Workshop pages. Do not bypass multiplayer mismatch checks until the exact safety risks are understood.

Before implementation, read `GOAL_NEXT_STAGE_GROUPS_AND_MULTIPLAYER_SYNC.md` completely and treat it as the authoritative detailed target.
