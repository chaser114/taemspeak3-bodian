#!/usr/bin/env sh
set -eu

cd "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
chmod +x ./TS3AudioBot
exec ./TS3AudioBot --non-interactive "$@"
