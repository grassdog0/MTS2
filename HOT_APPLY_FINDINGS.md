# ModTheSpire2 Hot Apply Findings

## Current Answer

For the current Slay the Spire 2 build, ModTheSpire2 treats "hot-startable" as Stage A hot apply:

- Safe settings/config state can be saved during the current session.
- Already-loaded behavior may react if the game or the mod already supports runtime settings changes.
- Previously unloaded DLL or PCK mods are not true-loaded by ModTheSpire2 during the current session.

Stage B true hot-load remains unsupported because the inspected public game APIs do not expose a clear safe "load this one new mod now" path after the main menu is already running.

## Implemented Classification

Hot-Apply candidates:

- Mods that explicitly declare runtime-safe Hot-Apply in their manifest, unless dependencies or session state make them unsafe.
- Utility/config manifests that do not include DLL/PCK startup payloads and do not affect gameplay.
- State-aware main-menu content candidates only when the game is at the main menu and no unfinished run may reference their content.

`ModTheSpire2` itself is Restart Required. It ships a DLL and patches game UI, so disabling it cannot unload the already-loaded code during the current session.

Restart Required:

- DLL mods.
- PCK mods.
- Gameplay or unknown-behavior mods.
- Framework/library DLL mods such as BaseLib and RitsuLib / `STS2-RitsuLib`.
- Mods declaring framework/library/core patch/UI patch/startup patch/save serializer/runtime service style tags.
- Any mod with a missing dependency.
- Any hot-declared mod that depends on a restart-required mod, including transitive dependency chains.
- Content mods that are only safe at the main menu when an active or resumable unfinished run exists.

## State-Aware Main-Menu Content

The first state-aware pass uses these game signals:

- `NGame.Instance.MainMenu` and `NGame.Instance.CurrentRunNode`.
- `RunManager.Instance.IsInProgress`, `IsGameOver`, and `IsAbandoned`.
- `SaveManager.Instance.HasRunSave` and `HasMultiplayerRunSave`.
- `SaveManager.Instance.CurrentRunSaveTask` when a run save is still in progress.
- `NMainMenu.ContinueRunInfo.HasResult` when the main menu has loaded continue-run information.

If those signals show a safe main menu with no unfinished run, a recognized future-run content candidate may appear in Hot-Apply. If the mod is not already enabled in the current session, it still requires the launcher because ModTheSpire2 does not true-load previously unloaded DLL/PCK files.

`Game state:` in the overlay/log now includes the source of the unfinished-run
decision: run save, multiplayer save, continue panel, and active save task. If
any of those signals is true, future-run content candidates stay blocked with a
main-menu-only reason instead of appearing under `Apply at Main Menu`.

The in-game overlay now tracks two separate signals:

- `Enabled in settings`: the current `settings.save` entry says the mod is enabled.
- `Loaded this session`: the current process has evidence that the mod DLL/assembly is already loaded.

Main-menu content Hot-Apply requires the loaded-session signal, not only the settings-enabled signal. This prevents a mod that was just enabled in settings, but not loaded into the current process, from being treated as safe to apply. The current conservative runtime hint comes from already-loaded assembly names, with `ModTheSpire2` itself seeded because its companion DLL is executing. A future version can replace or strengthen this with a public ModManager loaded-mod API if one becomes stable enough to use directly.

Current recognized examples:

- `wuwancients`: main-menu/future-run content candidate when already loaded and no unfinished run exists.
- `STS2-RitsuLib`: framework dependency root, Restart Required by default.

The scanner also recognizes manifest-declared future-run content through English
metadata tokens. A mod can use fields such as `hot_apply_scope`,
`hot_reload_scope`, `hot_apply_content`, `content_type`, `content_types`,
`content_tags`, `mod_type`, `mod_types`, `tags`, or `categories` with values like
`main_menu_no_run`, `future_run_content`, `run_generation`, `events`,
`characters`, `relics`, `cards`, `encounters`, or `ancients`. These declarations
only make the mod a state-aware main-menu candidate. They do not bypass the safe
main-menu/no-unfinished-run check, loaded-mod check, dependency checks, or
rollback behavior.

The scanner also recognizes explicit runtime/config scope through fields such
as `hot_apply_scope`, `hot_reload_scope`, `hot_apply_content`, `content_type`,
`content_types`, `content_tags`, `mod_type`, `mod_types`, `tags`, or
`categories` with values like `runtime`, `runtime_safe`, `settings`, `config`,
`configuration`, or `utility`. This path is deliberately narrow: it only
classifies a mod as `Runtime Hot-Apply` when the mod has no DLL/PCK payload and
does not affect gameplay. DLL/PCK or gameplay payloads remain Restart Required
even if they claim a runtime/config scope.

The scanner and launcher also accept legacy `pck_name` manifests as an id
fallback, but only when the same directory does not contain another JSON file
with a real `id`. This supports simple `mod_manifest.json`/`mod_mainfest.json`
layouts while avoiding duplicate entries for mods that ship both a sidecar
`pck_name` manifest and a full `<mod>.json` manifest.

Runtime data folders named `ModTheSpire2Data` are skipped during manifest
scanning. This keeps settings backups, launcher logs, generated profiles, and
other runtime JSON from being mistaken for installed mods.

Dependency ids are resolved conservatively. Dependency object keys can use
`id`, `mod_id`, `modId`, `workshop_id`, `workshopId`, `steam_id`, `steamId`,
`published_file_id`, or `publishedFileId`. Exact mod-id matches win. If a
dependency string does not match any id but uniquely matches one discovered
mod's display `name`, ModTheSpire2 canonicalizes that dependency to the matched
mod id. If the display-name match is ambiguous, the dependency remains
unresolved and is shown as missing. This supports mods that accidentally write a
display name in `dependencies` while avoiding unsafe guesses.

Workshop numeric dependency aliases are also supported conservatively. If a
dependency string does not match an id or unique display name, but uniquely
matches the numeric Workshop folder id of a discovered mod, ModTheSpire2
canonicalizes it to that mod id. This only uses already discovered local
Workshop folders; it does not query Steam or guess missing subscriptions.

The scanner also recognizes conservative restart-required tags in the same
manifest vocabulary. Values such as `framework`, `library`, `shared_library`,
`dependency_root`, `api`, `core_patch`, `ui_patch`, `startup_patch`,
`harmony_patch`, `save_serializer`, or `runtime_service` keep the mod in
Restart Required even if another field declares `hot_apply`. This avoids
treating shared APIs, UI patches, save serializers, or startup/runtime services
as safe hot-apply candidates just because they do not directly affect gameplay.
Save serializer tags use the more specific reason
`save serializer; restart required`, because save structure changes can affect
active or resumable runs even when the manifest otherwise looks like a small
configuration mod.

Framework-root tags are tracked separately from other restart-required patch
tags. A mod tagged as `framework`, `library`, `shared_library`,
`dependency_root`, `api`, or `runtime_service` remains Restart Required, but it
can be treated as an already-loaded dependency root for a loaded future-run
content candidate at a safe main menu. Patch-style tags such as `core_patch`,
`ui_patch`, `startup_patch`, `harmony_patch`, or `save_serializer` stay Restart
Required and are not treated as reusable framework roots for Hot-Apply.

## Rollback Behavior

Before hot apply, ModTheSpire2:

- Backs up the newest `settings.save` into `ModTheSpire2Data/hot-apply-backups`.
- Snapshots the in-memory `SettingsSave.ModSettings.ModList` state.
- Blocks the operation if it would disable a hot candidate that is still required by another enabled mod.
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

When the launcher overwrites ModTheSpire2-owned data files such as
`load-order.txt`, `enabled-mods.txt`, or named order profile files, it first
copies the previous file into:

```text
ModTheSpire2Data/file-backups
```

This gives load-order/profile changes the same practical rollback path as
`settings.save` changes without touching other game or Steam files.

## Remaining Work

- Manual in-game visual verification of the ModTheSpire2 management window.
- Better UI styling using game-native controls where practical.
- Possible deeper integration into the original mod settings page.
- Localization after the English-only optimization phase stabilizes.
- Stage B true hot-load only if a safe game-supported API is found later.
