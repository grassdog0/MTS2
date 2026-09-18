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
WORKSHOP_DIRS=()
SCAN_REPORTS=()
SCAN_CANDIDATES=0
SCAN_ACCEPTED=0
SCAN_SKIPPED=0
SCAN_ERRORS=0
SCAN_DUPLICATES=0

canonical_dir() {
  (cd -P "$1" 2>>"$LOG_FILE" && pwd -P)
}

add_steam_library() {
  local dir="$1"
  [ -n "$dir" ] || return 0
  [ -d "$dir/steamapps" ] || return 0
  local original="$dir"
  dir="$(canonical_dir "$dir")" || return 0
  log "Steam library path: $original -> $dir"
  local item
  for item in "${STEAM_LIBRARY_DIRS[@]:-}"; do
    [ "$item" = "$dir" ] && return 0
  done
  STEAM_LIBRARY_DIRS+=("$dir")
}

discover_steam_libraries() {
  local ancestor="$APP_DIR"
  while [ "$ancestor" != / ] && [ -n "$ancestor" ]; do
    add_steam_library "$ancestor"
    ancestor="$(dirname "$ancestor")"
  done
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

  local libfile line path base index=0
  while [ "$index" -lt "${#STEAM_LIBRARY_DIRS[@]}" ]; do
    base="${STEAM_LIBRARY_DIRS[$index]}"
    index=$((index + 1))
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
      canonical_dir "$candidate"
      return 0
    fi
  done
  return 1
}

discover_workshop_dirs() {
  WORKSHOP_DIRS=()
  local ancestor="$GAME_DIR"
  while [ "$ancestor" != / ] && [ -n "$ancestor" ]; do
    add_steam_library "$ancestor"
    ancestor="$(dirname "$ancestor")"
  done
  local lib candidate
  for lib in "${STEAM_LIBRARY_DIRS[@]:-}"; do
    candidate="$lib/steamapps/workshop/content/$APP_ID"
    if [ -d "$candidate" ]; then
      local resolved existing seen=0
      resolved="$(canonical_dir "$candidate")" || continue
      log "Workshop path: $candidate -> $resolved"
      for existing in "${WORKSHOP_DIRS[@]:-}"; do
        [ "$existing" = "$resolved" ] && seen=1
      done
      [ "$seen" = 1 ] || WORKSHOP_DIRS+=("$resolved")
    fi
  done
  return 0
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

check_json_runtime() {
  command -v perl >/dev/null 2>&1 || die "Perl is unavailable. Diagnostics cannot parse mod manifests."
  perl -MJSON::PP -e 'exit 0' 2>>"$LOG_FILE" ||
    die "Perl JSON::PP is unavailable. See launcher-script.log for the runtime error."
}

manifest_identity() {
  perl -MJSON::PP -MFile::Basename=basename,dirname -e '
    use strict;
    use warnings;
    my $file = shift;
    open my $fh, "<:raw", $file or do { warn "Cannot read manifest $file: $!\n"; exit 3; };
    local $/;
    my $bytes = <$fh>;
    $bytes =~ s/^\xEF\xBB\xBF//;
    my $doc = eval { JSON::PP::decode_json($bytes) };
    if ($@) { warn "Invalid manifest JSON: $file\n"; exit 3; }
    exit 2 unless ref($doc) eq "HASH";
    my $id = $doc->{id};
    $id = $doc->{pck_name} unless defined($id) && !ref($id) && length($id);
    exit 2 unless defined($id) && !ref($id) && length($id);
    if ($id =~ /[\x00-\x1f\x7f\/\\\\]/) { warn "Unsupported manifest id: $file\n"; exit 3; }
    my $base = basename($file);
    my $dir = dirname($file);
    if ($base =~ /\.manifest$/i) {
      exit 2 unless defined($doc->{id}) && !ref($doc->{id}) && length($doc->{id});
    } else {
      my $identity_file = lc($base) eq "mod_manifest.json" || lc($base) eq lc("$id.json");
      my $metadata = defined($doc->{name}) && !ref($doc->{name}) &&
        defined($doc->{version}) && !ref($doc->{version});
      my $payload = -f "$dir/$id.dll" || -f "$dir/$id.pck";
      exit 2 unless $identity_file || $metadata || $payload ||
        exists($doc->{has_dll}) || exists($doc->{has_pck}) || exists($doc->{pck_name});
    }
    my $name = $doc->{name};
    $name = $id unless defined($name) && !ref($name) && length($name);
    $name =~ s/[\x00-\x1f\x7f]/ /g;
    binmode STDOUT, ":encoding(UTF-8)";
    print "$id\t$name\n";
  ' "$1" 2>>"$LOG_FILE"
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
    [ "$existing" = "$id" ] && return 1
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
  root="$(canonical_dir "$root")" || return 0
  local manifest ident id name source_key status
  local candidates=0 accepted=0 skipped=0 errors=0 duplicates=0
  local inventory
  inventory="$(mktemp "$DATA_DIR/discovery.XXXXXX")" || die "Could not create discovery file."
  if ! find -L "$root" -type d \( -name ModTheSpire2Data -o -name .git \) -prune -o \
       -type f \( -iname '*.json' -o -iname '*.manifest' \) -print0 >"$inventory" 2>>"$LOG_FILE"; then
    errors=$((errors + 1))
    log "Scan incomplete (permissions or symlink loop): $root"
  fi
  while IFS= read -r -d '' manifest; do
    candidates=$((candidates + 1))
    case "$manifest" in
      */config.json) skipped=$((skipped + 1)); log "Skipped config: $manifest"; continue ;;
    esac
    if ident="$(manifest_identity "$manifest")"; then
      :
    else
      status=$?
      if [ "$status" = 2 ]; then
        skipped=$((skipped + 1)); log "Not a mod manifest: $manifest"
      else
        errors=$((errors + 1)); log "Manifest read/parse failed (exit $status): $manifest"
      fi
      continue
    fi
    id="${ident%%	*}"
    name="${ident#*	}"
    source_key="mods_directory"
    if [ "$source" = "Workshop" ]; then
      source_key="steam_workshop"
    fi
    if add_mod "$id" "$name" "$source" "$source_key"; then
      accepted=$((accepted + 1)); log "Discovered $source mod: $id ($manifest)"
    else
      duplicates=$((duplicates + 1)); log "Duplicate mod id skipped: $id ($manifest)"
    fi
  done < <(perl -0e 'print sort <>' "$inventory")
  rm -f -- "$inventory"
  SCAN_CANDIDATES=$((SCAN_CANDIDATES + candidates))
  SCAN_ACCEPTED=$((SCAN_ACCEPTED + accepted))
  SCAN_SKIPPED=$((SCAN_SKIPPED + skipped))
  SCAN_ERRORS=$((SCAN_ERRORS + errors))
  SCAN_DUPLICATES=$((SCAN_DUPLICATES + duplicates))
  SCAN_REPORTS+=("$source: $root | candidates=$candidates accepted=$accepted skipped=$skipped errors=$errors duplicates=$duplicates")
  log "${SCAN_REPORTS[${#SCAN_REPORTS[@]}-1]}"
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

use JSON::PP ();
my $root = JSON::PP::decode_json($json);
die "settings.save must contain a JSON object\n" unless ref($root) eq 'HASH';
my $settings = exists($root->{mod_list}) && !exists($root->{mod_settings})
  ? $root : ($root->{mod_settings} //= {});
die "mod_settings must be an object\n" unless ref($settings) eq 'HASH';
$settings->{mods_enabled} = JSON::PP::true;
my @entries;
for my $mod (@discovered) {
  my ($id, $source) = @$mod;
  push @entries, { id => $id, source => $source,
    is_enabled => ($modded && $selected{$id}) ? JSON::PP::true : JSON::PP::false };
}
for my $entry (@{ $settings->{mod_list} // [] }) {
  next if $discovered_id{$entry->{id}};
  $entry->{is_enabled} = JSON::PP::false unless $modded;
  push @entries, $entry;
}
$settings->{mod_list} = \@entries;
print JSON::PP->new->utf8->pretty->encode($root);
PERL
  mv "$tmp_file" "$settings_file" || die "Could not write settings.save."
}

launch_game() {
  local mode="${1:-selected}"
  if [ "${#ORIGINAL_CMD[@]}" -gt 0 ]; then
    local args=("${ORIGINAL_CMD[0]}")
    local arg key
    for arg in "${ORIGINAL_CMD[@]:1}"; do
      key="$arg"
      while [[ "$key" == -* ]]; do key="${key#-}"; done
      case "$key" in nomods|nomods=*) continue ;; esac
      args+=("$arg")
    done
    log "Launching original command ($mode): ${args[*]}"
    exec "${args[@]}"
  fi

  [ "$mode" != vanilla ] || die "Configure Steam Launch Options with -- %command% to start Vanilla through the original native game command."

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
  printf 'Workshop folders (canonical):\n'
  printf '  %s\n' "${WORKSHOP_DIRS[@]:-not found}"
  printf 'Discovered mods: %s\n' "${#MOD_IDS[@]}"
  printf 'Manifest candidates: %s; accepted: %s; skipped: %s; errors: %s; duplicates: %s\n' \
    "$SCAN_CANDIDATES" "$SCAN_ACCEPTED" "$SCAN_SKIPPED" "$SCAN_ERRORS" "$SCAN_DUPLICATES"
  printf '  %s\n' "${SCAN_REPORTS[@]:-no scan roots}"
  if [ "$SCAN_CANDIDATES" -gt 0 ] && [ "${#MOD_IDS[@]}" -eq 0 ]; then
    printf 'Manifest files exist but none were recognized. Check parse/skip reasons in the log.\n'
  fi
  printf 'Log file: %s\n' "$LOG_FILE"
}

check_json_runtime
discover_steam_libraries
GAME_DIR="$(find_game_dir || true)"
[ -n "$GAME_DIR" ] || die "Could not find the native $APP_NAME folder."
LOCAL_MODS_DIR="$GAME_DIR/mods"
discover_workshop_dirs
SETTINGS_FILE="$(find_settings_file || true)"

scan_mod_root "$LOCAL_MODS_DIR" "Local"
for workshop in "${WORKSHOP_DIRS[@]:-}"; do
  [ -n "$workshop" ] && scan_mod_root "$workshop" "Workshop"
done
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
    launch_game vanilla
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
