#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
DATA_DIR="$(mktemp -d "$root/dist/verify-fixtures/alias-XXXXXX")"
LOG_FILE="$DATA_DIR/alias.log"
log() { printf '%s\n' "$*" >> "$LOG_FILE"; }
die() { printf '%s\n' "$*" >&2; exit 1; }
source <(sed -n '/^append_unique() {/,/^load_lines() {/p' "$root/NativeLauncher/ModTheSpire2Launcher.sh" | sed '$d')
check_json_runtime
add_steam_library "$1"
add_steam_library "$2"
[ "${#STEAM_LIBRARY_DIRS[@]}" = 1 ]
scan_mod_root "$1/steamapps/workshop/content/2868840" Workshop
count="${#MOD_IDS[@]}"
[ "$count" -gt 0 ]
scan_mod_root "$2/steamapps/workshop/content/2868840" Workshop
[ "${#MOD_IDS[@]}" = "$count" ]
printf 'Canonical alias deduplication and scanning passed (%s mods).\n' "$count"
