#!/usr/bin/env sh
set -eu

cd "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if [ ! -s "./WebInterface/index.html" ] || [ ! -s "./WebInterface/bundle.js" ] || [ ! -s "./plugins/KuwoMusicPlugin.dll" ]; then
	printf '%s\n' "Linux package is incomplete: WebInterface or KuwoMusicPlugin.dll is missing." >&2
	exit 1
fi

# Durable data dir: bots, accounts, rights. Survives package overwrite upgrades.
if [ -f "./packaging/common/prepare-data.sh" ]; then
	sh "./packaging/common/prepare-data.sh" "." >/dev/null
else
	mkdir -p data/bots
	if [ ! -f data/ts3audiobot.toml ]; then
		printf '%s\n' '[configs]' 'bots_path = "bots"' '' '[db]' 'path = "ts3audiobot.db"' '' '[rights]' 'path = "rights.toml"' '' '[plugins]' 'path = "../plugins"' > data/ts3audiobot.toml
	fi
fi

chmod +x ./TS3AudioBot
# Run with CWD=data so relative bots_path / db paths stay under data/.
# Record PID for stop scripts / web-update restarts; keep foreground so Ctrl+C works.
cd data
../TS3AudioBot --config ts3audiobot.toml --non-interactive "$@" &
pid=$!
echo "$pid" > ../ts3audiobot.pid
trap 'rm -f ../ts3audiobot.pid; kill $pid 2>/dev/null || true' INT TERM EXIT
wait "$pid"
status=$?
rm -f ../ts3audiobot.pid
exit "$status"
