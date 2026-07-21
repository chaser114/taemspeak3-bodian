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

# Install + verify ffmpeg/libopus. Creates local lib/libopus.so* symlinks for the bot loader.
if [ -f "$repo/packaging/common/ensure-linux-deps.sh" ]; then
	sh "$repo/packaging/common/ensure-linux-deps.sh" "$repo"
elif command -v apt-get >/dev/null 2>&1; then
	if [ "$(id -u)" -eq 0 ]; then SUDO=""; else SUDO="sudo"; fi
	export DEBIAN_FRONTEND=noninteractive
	$SUDO apt-get update
	# libopus-dev provides libopus.so (unversioned); libopus0 alone is not enough for DllImport("libopus").
	$SUDO apt-get install -y ffmpeg libopus0 libopus-dev
fi

# Durable data dir: bots, accounts, rights. Survives package overwrite upgrades.
if [ -f "$repo/packaging/common/prepare-data.sh" ]; then
	sh "$repo/packaging/common/prepare-data.sh" "$repo" >/dev/null
else
	mkdir -p "$repo/data/bots"
	if [ ! -f "$repo/data/ts3audiobot.toml" ]; then
		printf '%s\n' '[configs]' 'bots_path = "bots"' '' '[db]' 'path = "ts3audiobot.db"' '' '[rights]' 'path = "rights.toml"' '' '[plugins]' 'path = "../plugins"' > "$repo/data/ts3audiobot.toml"
	fi
fi

chmod +x "$repo/TS3AudioBot"
# Help native loader find local symlinks even when CWD is data/.
export LD_LIBRARY_PATH="$repo/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

# Record PID for stop scripts; keep process in foreground so Ctrl+C still works.
cd "$repo/data"
"$repo/TS3AudioBot" --config ts3audiobot.toml --non-interactive "$@" &
pid=$!
echo "$pid" > "$repo/ts3audiobot.pid"
trap 'rm -f "$repo/ts3audiobot.pid"; kill $pid 2>/dev/null || true' INT TERM EXIT
wait "$pid"
status=$?
rm -f "$repo/ts3audiobot.pid"
exit "$status"
