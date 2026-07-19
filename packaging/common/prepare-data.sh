#!/usr/bin/env sh
# Prepare a durable data/ directory and migrate legacy in-place configs once.
set -eu

root=${1:-.}
cd "$root"

mkdir -p data

# Prefer an existing data tree. Only migrate when data is still empty.
has_data_bots=0
if [ -d data/bots ] && [ -n "$(find data/bots -mindepth 1 -maxdepth 1 2>/dev/null | head -n 1)" ]; then
	has_data_bots=1
fi
has_data_core=0
if [ -f data/ts3audiobot.toml ] || [ -f data/ts3audiobot.db ]; then
	has_data_core=1
fi

if [ "$has_data_bots" -eq 0 ] && [ "$has_data_core" -eq 0 ]; then
	if [ -d bots ] && [ -n "$(find bots -mindepth 1 -maxdepth 1 2>/dev/null | head -n 1)" ]; then
		printf '%s\n' "Migrating legacy bots/ into data/bots ..."
		mkdir -p data/bots
		# Copy then leave originals; upgrade scripts never delete data/.
		cp -a bots/. data/bots/ 2>/dev/null || true
	fi
	for f in ts3audiobot.toml ts3audiobot.db rights.toml NLog.config; do
		if [ -f "$f" ] && [ ! -f "data/$f" ]; then
			printf '%s\n' "Migrating legacy $f into data/ ..."
			cp -a "$f" "data/$f"
		fi
	done
fi

# Ensure a core config exists under data/ with paths pointing into data/.
if [ ! -f data/ts3audiobot.toml ]; then
	cat > data/ts3audiobot.toml <<'EOF'
[configs]
bots_path = "bots"

[db]
path = "ts3audiobot.db"

[rights]
path = "rights.toml"

[plugins]
# Keep plugins with the program package, not user data.
path = "../plugins"

[web]
hosts = ["*"]
port = 58913

[web.api]
enabled = true

[web.interface]
enabled = true
EOF
fi

# If an older data config still points plugins into data/plugins, rewrite to package plugins.
if [ -f data/ts3audiobot.toml ]; then
	if grep -Eq '^\s*path\s*=\s*"plugins"\s*$' data/ts3audiobot.toml 2>/dev/null; then
		# Only rewrite the plugins.path default when it is still relative "plugins".
		tmp=$(mktemp)
		awk '
			BEGIN { in_plugins=0 }
			/^\[plugins\]/ { in_plugins=1; print; next }
			/^\[/ { in_plugins=0 }
			in_plugins && $0 ~ /^[[:space:]]*path[[:space:]]*=[[:space:]]*"plugins"[[:space:]]*$/ {
				print "path = \"../plugins\""
				next
			}
			{ print }
		' data/ts3audiobot.toml > "$tmp"
		mv "$tmp" data/ts3audiobot.toml
	fi
fi

mkdir -p data/bots
printf '%s\n' "data"
