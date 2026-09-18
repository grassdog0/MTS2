#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Isolate process dispatch so no game, Steam client, or real settings are touched.
source <(sed -n '/^launch_game() {/,/^}/p' "$root/NativeLauncher/ModTheSpire2Launcher.sh")
log() { :; }
die() { printf '%s\n' "$*" >&2; exit 1; }
APP_ID=2868840
ORIGINAL_CMD=(bash -c 'printf "<%s>\n" "$@"' fixture --rendering-driver opengl3)
vanilla="$(launch_game vanilla)"
[[ "$vanilla" == $'<--rendering-driver>\n<opengl3>' ]]
ORIGINAL_CMD+=(--nomods)
selected="$(launch_game selected)"
[[ "$selected" == $'<--rendering-driver>\n<opengl3>' ]]
ORIGINAL_CMD=(bash -c 'printf "<%s>\n" "$@"' fixture --path 'a path with spaces' -- --custom 'two words')
vanilla="$(launch_game vanilla)"
[[ "$vanilla" == $'<--path>\n<a path with spaces>\n<-->\n<--custom>\n<two words>' ]]
ORIGINAL_CMD=()
if (launch_game vanilla) 2>/dev/null; then
  printf 'Vanilla incorrectly fell back without original command\n' >&2
  exit 1
fi
printf 'Shell Vanilla dispatch tests passed (stub command; no native game run).\n'
source <(sed -n '/^write_settings() {/,/^launch_game() {/p' "$root/NativeLauncher/ModTheSpire2Launcher.sh" | sed '$d')
mkdir -p "$root/dist/verify-fixtures"
DATA_DIR="$(mktemp -d "$root/dist/verify-fixtures/vanilla-shell-XXXXXX")"
settings="$DATA_DIR/settings.save"
printf '%s' '{"language":"eng","mod_settings":{"mods_enabled":false,"mod_list":[{"id":"Unknown","is_enabled":true}]}}' > "$settings"
MOD_IDS=(A B)
MOD_SOURCE_KEYS=(mods_directory steam_workshop)
SELECTED_IDS=(A)
write_settings "$settings" 0
perl -MJSON::PP -0777 -e 'my $j=decode_json(<>); die unless $j->{mod_settings}{mods_enabled} && @{$j->{mod_settings}{mod_list}}==3; for(@{$j->{mod_settings}{mod_list}}){die if $_->{is_enabled}}' "$settings"
write_settings "$settings" 1
perl -MJSON::PP -0777 -e 'my $j=decode_json(<>); my $m=$j->{mod_settings}{mod_list}; die unless $j->{language} eq "eng" && $m->[0]{id} eq "A" && $m->[0]{is_enabled} && !$m->[1]{is_enabled} && !$m->[2]{is_enabled}' "$settings"
printf 'Shell settings roundtrip passed: all disabled then selected, including unknown entries.\n'
