#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
mkdir -p "$root/dist/verify-fixtures"
fixture="$(mktemp -d "$root/dist/verify-fixtures/discovery-XXXXXX")"
export HOME="$fixture/home"
APP_ID=2868840
APP_NAME='Slay the Spire 2'
DATA_DIR="$fixture/data"
mkdir -p "$DATA_DIR"
LOG_FILE="$DATA_DIR/launcher-script.log"
log() { printf '%s\n' "$*" >> "$LOG_FILE"; }
die() { printf '%s\n' "$*" >&2; exit 1; }
fail() { printf 'FAILED: %s (fixture: %s)\n' "$*" "$fixture" >&2; exit 1; }
source <(sed -n '/^append_unique() {/,/^load_lines() {/p' "$root/NativeLauncher/ModTheSpire2Launcher.sh" | sed '$d')
source <(sed -n '/^show_diagnostics() {/,/^}/p' "$root/NativeLauncher/ModTheSpire2Launcher.sh")
check_json_runtime
first="$HOME/.local/share/Steam"
second="$fixture/Library With Spaces"
empty="$fixture/Empty Library"
mkdir -p "$first/steamapps/workshop/content/$APP_ID" "$second/steamapps/workshop/content/$APP_ID" "$empty/steamapps/workshop/content/$APP_ID"
APP_DIR="$first/steamapps/workshop/content/$APP_ID/3747911678"
GAME_DIR="$second/steamapps/common/$APP_NAME"
mkdir -p "$APP_DIR" "$GAME_DIR/mods" "$HOME/.steam"
printf '"libraryfolders" {\n"path" "%s"\n"path" "%s"\n}\n' "$second" "$empty" > "$first/steamapps/libraryfolders.vdf"
for ((i=1;i<=36;i++)); do
  lib="$first"; [ "$i" -le 18 ] || lib="$second"
  dir="$lib/steamapps/workshop/content/$APP_ID/$i/nested mod"
  mkdir -p "$dir"
  printf '{"id":"Mod%s","name":"Mod %s","version":"1"}' "$i" "$i" > "$dir/mod_manifest.json"
done
ORIGINAL_CMD=()
discover_steam_libraries
[ "${#STEAM_LIBRARY_DIRS[@]}" = 3 ] || fail 'VDF multi-library discovery'
STEAM_LIBRARY_DIRS=("$empty" "$first" "$second")
discover_workshop_dirs
[ "${#WORKSHOP_DIRS[@]}" = 3 ] || fail 'all Workshop roots, including empty first root'
for workshop in "${WORKSHOP_DIRS[@]}"; do scan_mod_root "$workshop" Workshop; done
[ "${#MOD_IDS[@]}" = 36 ] || fail '36 installed mods were not discovered'
[ "$SCAN_ACCEPTED" = 36 ] && [ "$SCAN_ERRORS" = 0 ] || fail 'initial counters'

localmods="$GAME_DIR/mods"
mkdir -p "$localmods/legacy" "$localmods/sidecar" "$localmods/bad" "$localmods/config" "$localmods/ModTheSpire2Data"
printf '\357\273\277{"pck_name":"Legacy","name":"A \\"quoted\\" \\u4e2d\\u6587 name"}' > "$localmods/legacy/legacy.json"
printf '{"id":"Sidecar"}' > "$localmods/sidecar/Sidecar.manifest"
printf '{"variants":[{"id":"NotTopLevel"}]}' > "$localmods/sidecar/variants.manifest"
printf '{"id":"NoIdentity","name":"Not enough metadata"}' > "$localmods/config/other.json"
printf '{"id":"FakeConfig","name":"Config","version":"1"}' > "$localmods/config/config.json"
printf '{"id":"Ghost","name":"Ghost","version":"1"}' > "$localmods/ModTheSpire2Data/ghost.json"
printf '{"id":' > "$localmods/bad/broken.json"
printf '{"id":"Mod1","name":"Duplicate","version":"1"}' > "$localmods/bad/duplicate.json"
scan_mod_root "$localmods" Local
[ "${#MOD_IDS[@]}" = 38 ] || fail 'legacy/sidecar identity or false positive guard'
[ "$SCAN_ERRORS" = 1 ] && [ "$SCAN_DUPLICATES" = 1 ] || fail 'error and duplicate counters'
[ "$SCAN_SKIPPED" = 3 ] || fail 'config and non-manifest counters'
grep -q 'Invalid manifest JSON' "$LOG_FILE" || fail 'parse diagnostic missing'
if manifest_identity "$fixture/not-found.json"; then fail 'missing file accepted'; else [ "$?" = 3 ] || fail 'missing file status'; fi

# Native Unix links also exercise a symlink in the middle of the Steam path.
if ln -s "$first" "$HOME/.steam/steam" 2>>"$LOG_FILE" && [ -L "$HOME/.steam/steam" ]; then
  add_steam_library "$HOME/.steam/steam"
  [ "${#STEAM_LIBRARY_DIRS[@]}" = 3 ] || fail 'symlink library was not deduplicated'
  before="${#MOD_IDS[@]}"
  scan_mod_root "$HOME/.steam/steam/steamapps/workshop/content/$APP_ID" Workshop
  [ "${#MOD_IDS[@]}" = "$before" ] || fail 'alias scan duplicated mods'
  printf 'Native symlink alias test passed.\n'
else
  printf 'Native symlink creation unavailable; alias test needs Unix verification.\n'
fi
SETTINGS_FILE="$fixture/settings.save"
show_diagnostics > "$fixture/diagnostics.txt"
grep -q 'Discovered mods: 38' "$fixture/diagnostics.txt" || fail 'diagnostics total'
if (PATH=/nonexistent; check_json_runtime) 2>"$fixture/missing-runtime.txt"; then fail 'missing Perl silently accepted'; fi
grep -q 'Perl is unavailable' "$fixture/missing-runtime.txt" || fail 'runtime error not actionable'
cp "$root/NativeLauncher/ModTheSpire2Launcher.sh" "$APP_DIR/ModTheSpire2Launcher.sh"
printf 'd\n' | bash "$APP_DIR/ModTheSpire2Launcher.sh" > "$fixture/full-diagnostics.txt"
grep -q 'Discovered mods: 38' "$fixture/full-diagnostics.txt" || fail 'full launcher diagnostics discovery'
printf 'Discovery tests passed: 36 mods, multi-library, BOM/Unicode, legacy/sidecar, errors and diagnostics.\nFixture: %s\n' "$fixture"
