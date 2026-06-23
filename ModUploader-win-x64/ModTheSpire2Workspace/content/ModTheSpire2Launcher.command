#!/bin/sh
DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec /usr/bin/env bash "$DIR/ModTheSpire2Launcher.sh" "$@"
