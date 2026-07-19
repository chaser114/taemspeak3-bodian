#!/usr/bin/env sh
set -eu
repo="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
pid_file="$repo/ts3audiobot.pid"
printf '%s\n' "Stopping TS3AudioBot under: $repo"
if [ -f "$pid_file" ]; then
	pid=$(cat "$pid_file" 2>/dev/null || true)
	if [ -n "${pid:-}" ]; then
		printf '%s\n' "Stopping PID $pid from pid file"
		kill "$pid" 2>/dev/null || true
		sleep 1
		kill -9 "$pid" 2>/dev/null || true
	fi
	rm -f "$pid_file"
fi
if command -v pgrep >/dev/null 2>&1; then
	pids=$(pgrep -f "$repo/TS3AudioBot" 2>/dev/null || true)
	if [ -n "$pids" ]; then
		printf '%s\n' "Stopping leftover: $pids"
		# shellcheck disable=SC2086
		kill $pids 2>/dev/null || true
		sleep 1
		# shellcheck disable=SC2086
		kill -9 $pids 2>/dev/null || true
	fi
fi
printf '%s\n' "Done. Logs: logs/console.log and data/logs/"
printf '%s\n' "Start again: ./run/start-linux.sh"
