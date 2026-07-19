#!/usr/bin/env sh
# Non-destructive in-place upgrade: replace program files only, never touch data/.
set -eu

usage() {
	printf '%s\n' "Usage: $0 <new-package-dir-or-tar.gz> [install-dir]"
	printf '%s\n' "  new-package-dir-or-tar.gz  Unpacked package folder or .tar.gz archive"
	printf '%s\n' "  install-dir                Existing install (default: current directory)"
}

if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ] || [ -z "${1:-}" ]; then
	usage
	exit 0
fi

src_input=$1
install_dir=${2:-.}
install_dir=$(CDPATH= cd -- "$install_dir" && pwd)

if [ ! -d "$install_dir" ]; then
	printf '%s\n' "Install directory not found: $install_dir" >&2
	exit 1
fi

tmpdir=""
cleanup() {
	if [ -n "$tmpdir" ] && [ -d "$tmpdir" ]; then
		rm -rf "$tmpdir"
	fi
}
trap cleanup EXIT

src_dir=""
case "$src_input" in
	*.tar.gz|*.tgz)
		tmpdir=$(mktemp -d)
		tar -xzf "$src_input" -C "$tmpdir"
		# Prefer the only top-level directory; otherwise use extract root.
		count=$(find "$tmpdir" -mindepth 1 -maxdepth 1 -type d | wc -l | tr -d ' ')
		if [ "$count" = "1" ]; then
			src_dir=$(find "$tmpdir" -mindepth 1 -maxdepth 1 -type d | head -n 1)
		else
			src_dir=$tmpdir
		fi
		;;
	*)
		src_dir=$(CDPATH= cd -- "$src_input" && pwd)
		;;
esac

if [ ! -x "$src_dir/TS3AudioBot" ] && [ ! -f "$src_dir/TS3AudioBot.exe" ]; then
	printf '%s\n' "Source package does not look like a TS3AudioBot release: $src_dir" >&2
	exit 1
fi

# Refuse to upgrade into a source package that is also the target accidentally wiping data without backup.
if [ "$src_dir" = "$install_dir" ]; then
	printf '%s\n' "Source and install directories are the same." >&2
	exit 1
fi

stamp=$(date +%Y%m%d-%H%M%S)
backup_dir="$install_dir/backup/pre-update-$stamp"
mkdir -p "$backup_dir"

printf '%s\n' "Backing up user data to $backup_dir ..."
for item in data bots ts3audiobot.toml ts3audiobot.db rights.toml NLog.config; do
	if [ -e "$install_dir/$item" ]; then
		cp -a "$install_dir/$item" "$backup_dir/" 2>/dev/null || true
	fi
done

# Stop running process if present (best effort).
if command -v pgrep >/dev/null 2>&1; then
	if pgrep -f "$install_dir/TS3AudioBot" >/dev/null 2>&1; then
		printf '%s\n' "Stopping running TS3AudioBot ..."
		pkill -f "$install_dir/TS3AudioBot" 2>/dev/null || true
		sleep 1
	fi
fi

copy_tree() {
	src=$1
	dst=$2
	if [ ! -e "$src" ]; then
		return 0
	fi
	mkdir -p "$dst"
	# Prefer rsync if available for cleaner overwrite.
	if command -v rsync >/dev/null 2>&1; then
		rsync -a --delete "$src"/ "$dst"/
	else
		rm -rf "$dst"
		mkdir -p "$dst"
		cp -a "$src"/. "$dst"/
	fi
}

printf '%s\n' "Updating program files (data/ is never overwritten) ..."
if [ -f "$src_dir/TS3AudioBot" ]; then
	cp -a "$src_dir/TS3AudioBot" "$install_dir/TS3AudioBot"
	chmod +x "$install_dir/TS3AudioBot"
fi
if [ -f "$src_dir/TS3AudioBot.exe" ]; then
	cp -a "$src_dir/TS3AudioBot.exe" "$install_dir/TS3AudioBot.exe"
fi

# Shared libraries next to the binary (self-contained publish).
# Copy package root files except protected user data names.
for entry in "$src_dir"/*; do
	[ -e "$entry" ] || continue
	name=$(basename -- "$entry")
	case "$name" in
		data|bots|ts3audiobot.toml|ts3audiobot.db|rights.toml|NLog.config|logs|backup)
			continue
			;;
		WebInterface|plugins|run|packaging)
			copy_tree "$entry" "$install_dir/$name"
			continue
			;;
		*)
			if [ -d "$entry" ]; then
				copy_tree "$entry" "$install_dir/$name"
			else
				cp -a "$entry" "$install_dir/$name"
			fi
			;;
	esac
done

# Ensure durable data layout after upgrade (migrates legacy once if needed).
if [ -f "$install_dir/packaging/common/prepare-data.sh" ]; then
	sh "$install_dir/packaging/common/prepare-data.sh" "$install_dir" >/dev/null
elif [ -f "$src_dir/packaging/common/prepare-data.sh" ]; then
	sh "$src_dir/packaging/common/prepare-data.sh" "$install_dir" >/dev/null
fi

printf '%s\n' "Upgrade finished."
printf '%s\n' "  Install : $install_dir"
printf '%s\n' "  Backup  : $backup_dir"
printf '%s\n' "  Data    : $install_dir/data  (preserved)"
printf '%s\n' "Start with: ./run/start-linux.sh   or   ./start.sh"
