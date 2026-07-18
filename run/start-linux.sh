#!/usr/bin/env sh
set -eu

repo="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"

# This launcher is copied into release packages under run/.
if [ ! -f "$repo/TS3AudioBot" ]; then
	printf '%s\n' "请使用 Linux 发布包运行此脚本，或直接运行 packaging/linux/install-linux.sh。" >&2
	exit 1
fi
if [ ! -s "$repo/WebInterface/index.html" ] || [ ! -s "$repo/WebInterface/bundle.js" ] || [ ! -s "$repo/plugins/KuwoMusicPlugin.dll" ]; then
	printf '%s\n' "Linux 发布包缺少网页或酷我插件，请重新下载 TS3AudioBot-KuwoPlugin-linux-x64.tar.gz。" >&2
	exit 1
fi

if command -v apt-get >/dev/null 2>&1; then
	if [ "$(id -u)" -eq 0 ]; then SUDO=""; else SUDO="sudo"; fi
	$SUDO apt-get update
	$SUDO apt-get install -y ffmpeg libopus0
fi

chmod +x "$repo/TS3AudioBot"
exec "$repo/TS3AudioBot" --non-interactive "$@"
