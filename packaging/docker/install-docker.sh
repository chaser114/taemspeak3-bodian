#!/usr/bin/env sh
set -eu

base="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"
if ! command -v docker >/dev/null 2>&1; then
    printf '%s\n' "Docker was not found. Install Docker Engine and Docker Compose first." >&2
    exit 1
fi

if docker compose version >/dev/null 2>&1; then
    compose="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
    compose="docker-compose"
else
    printf '%s\n' "Docker Compose was not found. Install Docker Compose first." >&2
    exit 1
fi

cd "$base"
mkdir -p data
exec $compose up -d --build
