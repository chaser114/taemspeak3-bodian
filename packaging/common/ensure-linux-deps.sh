#!/usr/bin/env sh
# Install and verify Linux runtime deps required by the bot (ffmpeg + libopus).
set -eu

install_root=${1:-.}
cd "$install_root"

have_opus=0
find_opus() {
	# Prefer known sonames used by Debian/Ubuntu package libopus0.
	for p in \
		/usr/lib/x86_64-linux-gnu/libopus.so.0 \
		/usr/lib/aarch64-linux-gnu/libopus.so.0 \
		/usr/lib64/libopus.so.0 \
		/usr/lib/libopus.so.0 \
		/lib/x86_64-linux-gnu/libopus.so.0 \
		/lib64/libopus.so.0 \
		/usr/lib/x86_64-linux-gnu/libopus.so \
		/usr/lib64/libopus.so \
		/usr/lib/libopus.so
	do
		if [ -e "$p" ]; then
			printf '%s\n' "$p"
			return 0
		fi
	done
	# ldconfig cache (when available)
	if command -v ldconfig >/dev/null 2>&1; then
		hit=$(ldconfig -p 2>/dev/null | awk '/libopus\.so/{print $NF; exit}')
		if [ -n "${hit:-}" ] && [ -e "$hit" ]; then
			printf '%s\n' "$hit"
			return 0
		fi
	fi
	return 1
}

printf '%s\n' "Checking Linux dependencies (ffmpeg, libopus)..."

if command -v apt-get >/dev/null 2>&1; then
	if [ "$(id -u)" -eq 0 ]; then SUDO=""; else SUDO="sudo"; fi
	# Non-interactive installs for one-click scripts.
	export DEBIAN_FRONTEND=noninteractive
	$SUDO apt-get update
	$SUDO apt-get install -y ffmpeg libopus0 || true
elif command -v dnf >/dev/null 2>&1; then
	if [ "$(id -u)" -eq 0 ]; then SUDO=""; else SUDO="sudo"; fi
	$SUDO dnf install -y ffmpeg opus || $SUDO dnf install -y opus || true
elif command -v yum >/dev/null 2>&1; then
	if [ "$(id -u)" -eq 0 ]; then SUDO=""; else SUDO="sudo"; fi
	$SUDO yum install -y opus || true
elif command -v pacman >/dev/null 2>&1; then
	if [ "$(id -u)" -eq 0 ]; then SUDO=""; else SUDO="sudo"; fi
	$SUDO pacman -Sy --noconfirm opus ffmpeg || true
elif command -v apk >/dev/null 2>&1; then
	if [ "$(id -u)" -eq 0 ]; then SUDO=""; else SUDO="sudo"; fi
	$SUDO apk add --no-cache opus ffmpeg || true
else
	printf '%s\n' "No supported package manager found. Please install ffmpeg and libopus manually." >&2
fi

opus_path=""
if opus_path=$(find_opus); then
	have_opus=1
	printf '%s\n' "Found libopus: $opus_path"
	# Help DllImport("libopus") by placing a local soname next to the binary.
	mkdir -p "./lib"
	# Prefer so.0 name and also unversioned name for loaders that strip versions differently.
	ln -sfn "$opus_path" "./lib/libopus.so.0" 2>/dev/null || true
	ln -sfn "$opus_path" "./lib/libopus.so" 2>/dev/null || true
	# Some loaders look for bare "libopus"
	ln -sfn "$opus_path" "./lib/libopus" 2>/dev/null || true
else
	printf '%s\n' "ERROR: libopus was not found after dependency install." >&2
	printf '%s\n' "Debian/Ubuntu: sudo apt-get install -y libopus0" >&2
	printf '%s\n' "Fedora/RHEL:   sudo dnf install -y opus" >&2
	printf '%s\n' "Alpine:         sudo apk add opus" >&2
	exit 1
fi

if ! command -v ffmpeg >/dev/null 2>&1; then
	printf '%s\n' "WARNING: ffmpeg was not found in PATH. Audio decoding may fail." >&2
else
	printf '%s\n' "Found ffmpeg: $(command -v ffmpeg)"
fi

printf '%s\n' "Linux dependencies OK."
