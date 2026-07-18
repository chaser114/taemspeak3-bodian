#!/usr/bin/env sh
set -eu

cd "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if [ ! -s "./WebInterface/index.html" ] || [ ! -s "./WebInterface/bundle.js" ] || [ ! -s "./plugins/KuwoMusicPlugin.dll" ]; then
	printf '%s\n' "Linux package is incomplete: WebInterface or KuwoMusicPlugin.dll is missing." >&2
	exit 1
fi
chmod +x ./TS3AudioBot
exec ./TS3AudioBot --non-interactive "$@"
