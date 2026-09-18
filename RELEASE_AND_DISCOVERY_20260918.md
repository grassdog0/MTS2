# Release and shell discovery follow-up

## Published baseline

- GitHub branch: v0.111-vanilla-order-restore
- Commit: bcfef3a (normal Vanilla startup and profile refresh)
- Workshop item: 3747911678; official uploader reported success.
- Existing Workshop visibility preserved via null. Description discloses the shell discovery issue.
- Snapshot: dist/snapshots/20260918-published-before-discovery-fix
- Published shell SHA256: 7047C0CE27A799639133D703CBE0DCDBB058E78F83642C26D53F0E8427A651B7

## Unpublished discovery candidate

The old Perl extractor read into $json but matched against the default $_ variable, causing valid manifests to yield empty identities. It is replaced by one JSON::PP parse per candidate, with top-level id/pck_name, name, BOM, Unicode, and explicit-id sidecar support. Non-manifest JSON and invalid JSON have separate diagnostics.

Steam libraries are canonicalized and deduplicated. Script/game ancestors and libraryfolders.vdf are considered. All unique Workshop roots are scanned, including roots after an empty first library. Directory links are followed, runtime data is pruned, and filesystem traversal errors are logged rather than silently lost.

Diagnostics shows canonical roots, per-root candidate/accepted/skipped/error/duplicate counts, and the detailed log path. Missing Perl/JSON::PP produces an actionable error. No settings-write, launch-mode, or Windows executable logic is changed in this follow-up.

## Evidence

- TestShellDiscovery.sh: 36 fixture mods across two libraries discovered; empty first library; nested/space paths; BOM/Unicode; legacy pck_name and explicit-id sidecar; invalid JSON; config/runtime exclusion; duplicate IDs; missing files/runtime; full script Diagnostics entry. Adding two compatibility fixtures yields 38 unique mods.
- TestShellAlias.sh: Windows junction and target canonicalize to one Steam library; repeated alias scans retain 18 unique mods.
- TestOfficialVanilla.sh: argument dispatch and all-disabled/selected settings roundtrip passed.
- Bash syntax check passed.
- Native Unix symlink creation is unavailable on this Windows host; the portable fixture test attempts it and explicitly reports the skip. Real Linux/macOS game runs remain unverified.

The discovery candidate is not pushed or uploaded. Ask the reporting player to run its Diagnostics on their existing install; do not ask them to remove Steam's symlink. Expected: all libraries listed, nonzero accepted mods (36 if that is the unique installed count), and useful reasons for any excluded files.
