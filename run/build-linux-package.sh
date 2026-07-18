#!/usr/bin/env sh
set -eu

repo=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
exec "$repo/packaging/linux/build-package.sh" "$@"
