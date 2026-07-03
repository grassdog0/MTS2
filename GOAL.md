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

The current public Workshop baseline has been uploaded. GitHub source has a local commit ready, but the push may still need to be retried if GitHub network access is unavailable.

- GitHub: `https://github.com/grassdog0/MTS2`
- Branch: `cross-platform-launcher`
- GitHub push status at the time of this goal update: local `cross-platform-launcher` branch is ahead of origin because `github.com:443` timed out / reset during push.
- Before claiming GitHub is updated, run `git status --short --branch` and `git log --oneline origin/cross-platform-launcher..HEAD` inside the source export to inspect the exact pending commits, then retry `git push origin cross-platform-launcher`.
- Workshop item: `3747911678`
- Verified clean package: `dist\WorkshopUpload\ModTheSpire2Content-Clean`
- Timestamped backup package: `dist\TestPackages\ModTheSpire2-0.4.0-cross-platform-mismatch-20260703-113950.zip`
- Package SHA256: `94BE1C14179A713D07217149ED8A95390AECA81B6761234E3F10F62CC116D1B2`

Treat this uploaded build as the stable rollback point. It includes the clean restart-manager workflow, the latest launcher/profile/settings.save fixes, the cross-platform script preview, Better Mod Menu grouping import, and the experimental read-only multiplayer mismatch helper.

If local source contains newer experimental hot-apply, draggable UI, force-join multiplayer bypass, auto-subscribe, or invasive UI changes, compare it against this published baseline before continuing. Do not ship unverified experimental behavior.

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

The next active work should be stabilization and feedback-driven refinement:

- Confirm the uploaded Workshop build behaves like the local verified package.
- Retry the GitHub push for `cross-platform-launcher` when network access to GitHub is available.
- Keep Better Mod Menu grouping optional and read-only.
- Improve multiplayer mismatch assistance only from real reports or confirmed game data.
- Continue to avoid bypassing multiplayer mismatch checks, force joining, and auto-subscribing Workshop items until exact risks and failure modes are understood.

Before implementation, read `GOAL_NEXT_STAGE_GROUPS_AND_MULTIPLAYER_SYNC.md` completely and treat it as the authoritative detailed target.
