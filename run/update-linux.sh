#!/usr/bin/env sh
# Foolproof non-destructive upgrade entry for Linux packages / servers.
set -eu

repo="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
script="$repo/packaging/common/update-in-place.sh"

if [ ! -f "$script" ]; then
	printf '%s\n' "update-in-place.sh not found. Use a release package that includes packaging/common/." >&2
	exit 1
fi

if [ -z "${1:-}" ]; then
	printf '%s\n' "用法: ./run/update-linux.sh <新版本目录或.tar.gz> [安装目录]"
	printf '%s\n' "示例: ./run/update-linux.sh ../TS3AudioBot-KuwoPlugin-linux-x64-new"
	printf '%s\n' "      ./run/update-linux.sh ../TS3AudioBot-KuwoPlugin-linux-x64.tar.gz"
	printf '%s\n' "说明: 只会替换程序文件，data/ 里的机器人与账号不会被覆盖。"
	exit 0
fi

exec sh "$script" "$@"
