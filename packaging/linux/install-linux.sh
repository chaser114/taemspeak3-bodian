#!/usr/bin/env sh
set -eu
base="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if command -v apt-get >/dev/null 2>&1; then
	if [ "$(id -u)" -eq 0 ]; then SUDO=""; else SUDO="sudo"; fi
	$SUDO apt-get update
	$SUDO apt-get install -y ffmpeg libopus0
fi
chmod +x "$base/TS3AudioBot" "$base/start.sh"
exec "$base/start.sh"
