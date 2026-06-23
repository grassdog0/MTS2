#!/usr/bin/env bash
set -euo pipefail

APP_ID="2868840"
APP_NAME="Slay the Spire 2"

SCRIPT_PATH="${BASH_SOURCE[0]}"
while [ -L "$SCRIPT_PATH" ]; do
  SCRIPT_DIR="$(cd -P "$(dirname "$SCRIPT_PATH")" >/dev/null 2>&1 && pwd)"
  SCRIPT_PATH="$(readlink "$SCRIPT_PATH")"
  case "$SCRIPT_PATH" in
    /*) ;;
    *) SCRIPT_PATH="$SCRIPT_DIR/$SCRIPT_PATH" ;;
  esac
done
APP_DIR="$(cd -P "$(dirname "$SCRIPT_PATH")" >/dev/null 2>&1 && pwd)"
DATA_DIR="$APP_DIR/ModTheSpire2Data"
PROFILES_DIR="$DATA_DIR/order-profiles"
mkdir -p "$DATA_DIR" "$PROFILES_DIR"

LOG_FILE="$DATA_DIR/launcher-script.log"
log() {
  printf '%s %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" >>"$LOG_FILE"
}

die() {
  printf '\nModTheSpire2: %s\n' "$*" >&2
  log "ERROR: $*"
  exit 1
}

pause_if_terminal() {
  if [ -t 0 ]; then
    printf '\nPress Enter to close...'
    read -r _ || true
  fi
}

ORIGINAL_CMD=()
PASSTHROUGH=()
found_separator=0
for arg in "$@"; do
  if [ "$found_separator" -eq 0 ] && [ "$arg" = "--" ]; then
    found_separator=1
    continue
  fi
  if [ "$found_separator" -eq 1 ]; then
    ORIGINAL_CMD+=("$arg")
  else
    PASSTHROUGH+=("$arg")
  fi
done
if [ "$found_separator" -eq 0 ] && [ "$#" -gt 0 ]; then
  ORIGINAL_CMD=("$@")
fi

append_unique() {
  local value="$1"
  shift
  local existing
  for existing in "$@"; do
    [ "$existing" = "$value" ] && return 1
  done
  printf '%s\n' "$value"
}

STEAM_LIBRARY_DIRS=()
add_steam_library() {
  local dir="$1"
  [ -n "$dir" ] || return 0
  [ -d "$dir/steamapps" ] || return 0
  local item
  for item in "${STEAM_LIBRARY_DIRS[@]:-}"; do
    [ "$item" = "$dir" ] && return 0
  done
  STEAM_LIBRARY_DIRS+=("$dir")
}

discover_steam_libraries() {
  case "$(uname -s)" in
    Darwin)
      add_steam_library "$HOME/Library/Application Support/Steam"
      ;;
    *)
      add_steam_library "$HOME/.steam/steam"
      add_steam_library "$HOME/.local/share/Steam"
      add_steam_library "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
      ;;
  esac

  local libfile line path base
  for base in "${STEAM_LIBRARY_DIRS[@]:-}"; do
    libfile="$base/steamapps/libraryfolders.vdf"
    [ -f "$libfile" ] || continue
    while IFS= read -r line; do
      case "$line" in
        *'"path"'*)
          path="$(printf '%s\n' "$line" | sed -n 's/.*"path"[[:space:]]*"\(.*\)".*/\1/p')"
          path="${path//\\\\/\\}"
          [ -n "$path" ] && add_steam_library "$path"
          ;;
      esac
    done <"$libfile"
  done
}

command_game_dir() {
  [ "${#ORIGINAL_CMD[@]}" -gt 0 ] || return 1
  local first="${ORIGINAL_CMD[0]}"
  [ -n "$first" ] || return 1
  case "$first" in
    steam://*) return 1 ;;
  esac
  if [ -f "$first" ] || [ -x "$first" ]; then
    cd -P "$(dirname "$first")" >/dev/null 2>&1 && pwd
    return 0
  fi
  return 1
}

find_game_dir() {
  local dir
  if dir="$(command_game_dir 2>/dev/null)"; then
    printf '%s\n' "$dir"
    return 0
  fi

  local current="$APP_DIR"
  while [ "$current" != "/" ] && [ -n "$current" ]; do
    if [ -d "$current/mods" ] && { [ -f "$current/SlayTheSpire2.x86_64" ] || [ -f "$current/SlayTheSpire2" ] || [ -d "$current/Slay the Spire 2.app" ]; }; then
      printf '%s\n' "$current"
      return 0
    fi
    current="$(dirname "$current")"
  done

  local lib candidate
  for lib in "${STEAM_LIBRARY_DIRS[@]:-}"; do
    candidate="$lib/steamapps/common/Slay the Spire 2"
    if [ -d "$candidate" ]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  return 1
}

find_workshop_dir() {
  local lib candidate
  for lib in "${STEAM_LIBRARY_DIRS[@]:-}"; do
    candidate="$lib/steamapps/workshop/content/$APP_ID"
    if [ -d "$candidate" ]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  return 1
}

find_settings_file() {
  local newest=""
  case "$(uname -s)" in
    Darwin)
      search_roots=(
        "$HOME/Library/Application Support/SlayTheSpire2"
        "$HOME/Library/Application Support/Godot/app_userdata/SlayTheSpire2"
      )
      ;;
    *)
      search_roots=(
        "$HOME/.local/share/SlayTheSpire2"
        "$HOME/.local/share/godot/app_userdata/SlayTheSpire2"
        "$HOME/.config/SlayTheSpire2"
        "$HOME/.steam/steam/steamapps/compatdata/$APP_ID/pfx/drive_c/users/steamuser/AppData/Roaming/SlayTheSpire2"
      )
      ;;
  esac

  local root found
  for root in "${search_roots[@]}"; do
    [ -d "$root" ] || continue
    while IFS= read -r -d '' found; do
      if [ -z "$newest" ] || [ "$found" -nt "$newest" ]; then
        newest="$found"
      fi
    done < <(find "$root" -name settings.save -type f -print0 2>/dev/null)
  done

  [ -n "$newest" ] && printf '%s\n' "$newest"
}

json_value() {
  local file="$1"
  local key="$2"
  perl -0777 -e '
    my ($key, $file) = @ARGV;
    open my $fh, "<", $file or exit 0;
    local $/;
    my $json = <$fh>;
    if (/"\Q$key\E"\s*:\s*"((?:\\.|[^"\\])*)"/s) {
      my $v = $1;
      $v =~ s/\\"/"/g;
      $v =~ s/\\\\/\\/g;
      print $v;
      exit;
    }
  ' "$key" "$file" 2>/dev/null || true
}

manifest_identity() {
  local file="$1"
  local id name
  id="$(json_value "$file" id)"
  if [ -z "$id" ]; then
    id="$(json_value "$file" pck_name)"
  fi
  [ -n "$id" ] || return 1
  name="$(json_value "$file" name)"
  [ -n "$name" ] || name="$id"
  printf '%s\t%s\n' "$id" "$name"
}

MOD_IDS=()
MOD_NAMES=()
MOD_SOURCES=()
MOD_SOURCE_KEYS=()

add_mod() {
  local id="$1"
  local name="$2"
  local source="$3"
  local source_key="$4"
  local existing
  for existing in "${MOD_IDS[@]:-}"; do
    [ "$existing" = "$id" ] && return 0
  done
  MOD_IDS+=("$id")
  MOD_NAMES+=("$name")
  MOD_SOURCES+=("$source")
  MOD_SOURCE_KEYS+=("$source_key")
}

scan_mod_root() {
  local root="$1"
  local source="$2"
  [ -d "$root" ] || return 0
  local manifest rel dir ident id name source_key
  while IFS= read -r -d '' manifest; do
    case "$manifest" in
      */ModTheSpire2Data/*) continue ;;
      */config.json) continue ;;
    esac
    ident="$(manifest_identity "$manifest" || true)"
    [ -n "$ident" ] || continue
    id="${ident%%	*}"
    name="${ident#*	}"
    dir="$(dirname "$manifest")"
    source_key="mods_directory"
    if [ "$source" = "Workshop" ]; then
      rel="${dir#"$root"/}"
      source_key="steam_workshop"
      case "$rel" in
        */*) ;;
        *) ;;
      esac
    fi
    add_mod "$id" "$name" "$source" "$source_key"
  done < <(find "$root" -type f \( -name '*.json' -o -name '*.manifest' \) -print0 2>/dev/null)
}

load_lines() {
  local file="$1"
  [ -f "$file" ] || return 0
  sed 's/\r$//' "$file" | sed '/^[[:space:]]*$/d'
}

id_known() {
  local id="$1"
  local known
  for known in "${MOD_IDS[@]:-}"; do
    [ "$known" = "$id" ] && return 0
  done
  return 1
}

enabled_from_file() {
  local file="$1"
  local id
  SELECTED_IDS=()
  while IFS= read -r id; do
    id="${id#"${id%%[![:space:]]*}"}"
    id="${id%"${id##*[![:space:]]}"}"
    [ -n "$id" ] || continue
    id_known "$id" && SELECTED_IDS+=("$id")
  done < <(load_lines "$file")
}

apply_order_file() {
  local order_file="$1"
  [ -f "$order_file" ] || return 0
  local NEW_IDS=()
  local NEW_NAMES=()
  local NEW_SOURCES=()
  local NEW_SOURCE_KEYS=()
  local id i
  while IFS= read -r id; do
    for i in "${!MOD_IDS[@]}"; do
      if [ "${MOD_IDS[$i]}" = "$id" ]; then
        NEW_IDS+=("${MOD_IDS[$i]}")
        NEW_NAMES+=("${MOD_NAMES[$i]}")
        NEW_SOURCES+=("${MOD_SOURCES[$i]}")
        NEW_SOURCE_KEYS+=("${MOD_SOURCE_KEYS[$i]}")
        break
      fi
    done
  done < <(load_lines "$order_file")
  for i in "${!MOD_IDS[@]}"; do
    local seen=0
    for id in "${NEW_IDS[@]:-}"; do
      [ "$id" = "${MOD_IDS[$i]}" ] && seen=1 && break
    done
    if [ "$seen" -eq 0 ]; then
      NEW_IDS+=("${MOD_IDS[$i]}")
      NEW_NAMES+=("${MOD_NAMES[$i]}")
      NEW_SOURCES+=("${MOD_SOURCES[$i]}")
      NEW_SOURCE_KEYS+=("${MOD_SOURCE_KEYS[$i]}")
    fi
  done
  MOD_IDS=("${NEW_IDS[@]}")
  MOD_NAMES=("${NEW_NAMES[@]}")
  MOD_SOURCES=("${NEW_SOURCES[@]}")
  MOD_SOURCE_KEYS=("${NEW_SOURCE_KEYS[@]}")
}

selected_contains() {
  local id="$1"
  local selected
  for selected in "${SELECTED_IDS[@]:-}"; do
    [ "$selected" = "$id" ] && return 0
  done
  return 1
}

write_settings() {
  local settings_file="$1"
  local modded="$2"
  [ -f "$settings_file" ] || die "settings.save was not found. Start the game once first."
  local backup_dir="$DATA_DIR/settings-backups"
  mkdir -p "$backup_dir"
  local backup="$backup_dir/settings.save.$(date '+%Y%m%d-%H%M%S').bak"
  cp -p "$settings_file" "$backup" || die "Could not back up settings.save."
  log "Backed up settings.save to $backup"

  local tmp_file="$settings_file.ModTheSpire2.tmp"
  local selected_blob discovered_blob
  selected_blob="$(printf '%s\n' "${SELECTED_IDS[@]:-}")"
  discovered_blob="$(
    for i in "${!MOD_IDS[@]}"; do
      printf '%s\t%s\n' "${MOD_IDS[$i]}" "${MOD_SOURCE_KEYS[$i]}"
    done
  )"

  MODDED="$modded" SELECTED_IDS_BLOB="$selected_blob" DISCOVERED_MODS_BLOB="$discovered_blob" perl -0777 - "$settings_file" >"$tmp_file" <<'PERL'
use strict;
use warnings;
my $json = do { local $/; <> };
my $modded = $ENV{MODDED} eq '1';
my %selected = map { $_ => 1 } grep { length $_ } split /\n/, ($ENV{SELECTED_IDS_BLOB} // '');
my @discovered;
my %discovered_id;
for my $line (split /\n/, ($ENV{DISCOVERED_MODS_BLOB} // '')) {
  next unless length $line;
  my ($id, $source) = split /\t/, $line, 2;
  next unless defined $id && length $id;
  $source = 'mods_directory' unless defined $source && length $source;
  push @discovered, [$id, $source];
  $discovered_id{$id} = 1;
}

sub esc {
  my ($s) = @_;
  $s =~ s/\\/\\\\/g;
  $s =~ s/"/\\"/g;
  return $s;
}

if ($modded) {
  $json =~ s/"mods_enabled"\s*:\s*false/"mods_enabled": true/s;
} else {
  $json =~ s/"mods_enabled"\s*:\s*true/"mods_enabled": false/s;
}

my $key = index($json, '"mod_list"');
if ($key >= 0) {
  my $open = index($json, '[', $key);
  if ($open >= 0) {
    my $i = $open;
    my ($depth, $in, $esc) = (0, 0, 0);
    my $close = -1;
    while ($i < length($json)) {
      my $c = substr($json, $i, 1);
      if ($in) {
        if ($esc) { $esc = 0; }
        elsif ($c eq "\\") { $esc = 1; }
        elsif ($c eq '"') { $in = 0; }
      } else {
        if ($c eq '"') { $in = 1; }
        elsif ($c eq '[') { $depth++; }
        elsif ($c eq ']') {
          $depth--;
          if ($depth == 0) { $close = $i; last; }
        }
      }
      $i++;
    }
    if ($close > $open) {
      my $old = substr($json, $open + 1, $close - $open - 1);
      my @entries;
      for my $mod (@discovered) {
        my ($id, $source) = @$mod;
        my $on = ($modded && $selected{$id}) ? 'true' : 'false';
        push @entries, '{"id":"' . esc($id) . '","is_enabled":' . $on . ',"source":"' . esc($source) . '"}';
      }
      while ($old =~ /(\{(?:[^{}"]+|"(?:\\.|[^"\\])*")*\})/sg) {
        my $obj = $1;
        if ($obj =~ /"id"\s*:\s*"((?:\\.|[^"\\])*)"/s) {
          my $id = $1;
          $id =~ s/\\"/"/g;
          $id =~ s/\\\\/\\/g;
          push @entries, $obj unless $discovered_id{$id};
        }
      }
      substr($json, $open, $close - $open + 1) = '[' . join(',', @entries) . ']';
    }
  }
}

print $json;
PERL
  mv "$tmp_file" "$settings_file" || die "Could not write settings.save."
}

launch_game() {
  if [ "${#ORIGINAL_CMD[@]}" -gt 0 ]; then
    log "Launching original command: ${ORIGINAL_CMD[*]}"
    exec "${ORIGINAL_CMD[@]}"
  fi

  if command -v steam >/dev/null 2>&1; then
    log "Launching through steam app id $APP_ID"
    exec steam "steam://rungameid/$APP_ID"
  fi

  die "No original Steam command was provided and the steam command was not found. Configure Steam Launch Options to call this script with -- %command%."
}

show_diagnostics() {
  printf '\nDiagnostics\n'
  printf 'Script folder: %s\n' "$APP_DIR"
  printf 'Game folder: %s\n' "${GAME_DIR:-not found}"
  printf 'Settings file: %s\n' "${SETTINGS_FILE:-not found}"
  printf 'Workshop folder: %s\n' "${WORKSHOP_DIR:-not found}"
  printf 'Discovered mods: %s\n' "${#MOD_IDS[@]}"
  printf 'Log file: %s\n' "$LOG_FILE"
}

discover_steam_libraries
GAME_DIR="$(find_game_dir || true)"
[ -n "$GAME_DIR" ] || die "Could not find the native $APP_NAME folder."
LOCAL_MODS_DIR="$GAME_DIR/mods"
WORKSHOP_DIR="$(find_workshop_dir || true)"
SETTINGS_FILE="$(find_settings_file || true)"

scan_mod_root "$LOCAL_MODS_DIR" "Local"
[ -n "$WORKSHOP_DIR" ] && scan_mod_root "$WORKSHOP_DIR" "Workshop"
apply_order_file "$DATA_DIR/load-order.txt"

printf '\nModTheSpire2 lightweight launcher\n'
printf 'Game: %s\n' "$GAME_DIR"
printf 'Mods discovered: %s\n' "${#MOD_IDS[@]}"
printf '\n'
printf '1) Launch Vanilla once\n'
printf '2) Launch saved enabled mods\n'

PROFILE_FILES=()
profile_index=3
if [ -d "$PROFILES_DIR" ]; then
  while IFS= read -r -d '' profile; do
    case "$profile" in
      *.enabled.txt)
        PROFILE_FILES+=("$profile")
        printf '%s) Launch profile: %s\n' "$profile_index" "$(basename "$profile" .enabled.txt)"
        profile_index=$((profile_index + 1))
        ;;
    esac
  done < <(find "$PROFILES_DIR" -maxdepth 1 -type f -name '*.enabled.txt' -print0 2>/dev/null)
fi
printf 'd) Diagnostics\n'
printf 'q) Quit\n'
printf '\nChoose: '
read -r choice

case "$choice" in
  1)
    SELECTED_IDS=()
    write_settings "$SETTINGS_FILE" 0
    launch_game
    ;;
  2)
    enabled_from_file "$DATA_DIR/enabled-mods.txt"
    write_settings "$SETTINGS_FILE" 1
    launch_game
    ;;
  d|D)
    show_diagnostics
    pause_if_terminal
    ;;
  q|Q)
    exit 0
    ;;
  *)
    if printf '%s' "$choice" | grep -Eq '^[0-9]+$'; then
      idx=$((choice - 3))
      if [ "$idx" -ge 0 ] && [ "$idx" -lt "${#PROFILE_FILES[@]}" ]; then
        profile_file="${PROFILE_FILES[$idx]}"
        enabled_from_file "$profile_file"
        order_file="${profile_file%.enabled.txt}.txt"
        apply_order_file "$order_file"
        write_settings "$SETTINGS_FILE" 1
        launch_game
      fi
    fi
    die "Unknown menu choice."
    ;;
esac
