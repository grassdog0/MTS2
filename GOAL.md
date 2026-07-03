# ModTheSpire2 Goal Mode Entry

Use this file as the entry prompt for the next Goal-mode run.

## Goal Prompt

Continue ModTheSpire2 development by strictly following:

```text
C:\Users\HZDH\Desktop\tmp\Forimpro\GOAL_NEXT_STAGE_GROUPS_AND_MULTIPLAYER_SYNC.md
```

Also maintain the progress log:

```text
C:\Users\HZDH\Desktop\tmp\Forimpro\PROGRESS_20260703_GROUPS_SYNC_RESEARCH.md
```

## Current Published Baseline

The current public baseline has been uploaded to both GitHub and Workshop.

- GitHub: `https://github.com/grassdog0/MTS2`
- Branch: `cross-platform-launcher`
- Code/package commit: `fe0e104 Document multiplayer mismatch findings`
- Workshop item: `3747911678`
- Verified clean package: `dist\WorkshopUpload\ModTheSpire2Content-Clean`
- Timestamped backup package: `dist\TestPackages\ModTheSpire2-0.4.0-current-upload-20260703-100953.zip`
- Package SHA256: `1F79C9E677E0CE9E3931527D4355299A86381B768CD74C685FF0FBCEAD4E7CCC`

Treat this uploaded build as the stable rollback point. It includes the clean restart-manager workflow, the latest launcher/profile/settings.save fixes, the cross-platform script preview, and the launcher grouping preview.

If local source contains newer experimental multiplayer, hot-apply, draggable UI, or invasive UI changes, compare it against this published baseline before continuing. Do not ship unverified experimental behavior.

## Next Direction

The next stage should preserve the clean restart-manager direction:

- Whole-mod enablement is changed before startup through the launcher.
- The in-game Settings / Mod Settings entry remains a simple `Close and Open Launcher` helper.
- Do not bring broad whole-mod Hot-Apply back as the default workflow.
- Do not reintroduce draggable in-game entrance controls unless the user explicitly reopens that feature.

Regression-protect the current published behavior before larger new features:

- The Settings / Mod Settings button remains visible and clickable when other mod-setting UI layers, including Better Mod Menu, are present.
- Launcher startup state is tied to the actual current `settings.save`: enabled state and load order should reflect the game, RitsuLib, Better Mod Menu, or vanilla mod manager output.
- User-defined profiles preserve both enabled selections and load order.
- The launcher has one clear `Save` action: if the typed/selected profile name exists, update it; otherwise create it.
- Vanilla Launch is a temporary launch mode and must not erase or reorder the normal saved mod selection.
- Dependency validation runs only on selected/enabled mods.
- `settings.save` enabled-state parsing uses exact ids, especially for overlapping names/ids such as `Hina` and `TenshiHinanawi`.
- `Default` is a normal saved preset if present; it should not replace the live `Current settings.save` view, and it should not disappear unless the user explicitly deletes or overwrites it.

The next active work should be low-risk grouping refinement and multiplayer synchronization research:

- Better Mod Menu grouping should remain optional and read-only at first.
- If Better Mod Menu group data is unavailable, fall back to ModTheSpire2's own simple categories.
- Multiplayer mod sync should first focus on showing host/local differences and opening Workshop pages. Do not bypass multiplayer mismatch checks until exact risks and failure modes are understood.

Before implementation, read `GOAL_NEXT_STAGE_GROUPS_AND_MULTIPLAYER_SYNC.md` completely and treat it as the authoritative detailed target.
