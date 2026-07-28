#!/usr/bin/env sh
set -eu

output_root=${1:?package output directory is required}
model_name=vosk-model-small-cn-0.22
model_mirror_url=${VOICE_MODEL_MIRROR_URL:-https://github.com/chaser114/taemspeak3-bodian/releases/download/voice-model-vosk-model-small-cn-0.22/${model_name}.zip}
model_official_url=https://alphacephei.com/vosk/models/${model_name}.zip
archive_override=${VOICE_MODEL_ARCHIVE:-}
model_dir=$output_root/voice-models/$model_name

if [ -f "$model_dir/conf/model.conf" ]; then
	printf '%s\n' "Voice model already present: $model_dir"
	exit 0
fi

temp_dir=$(mktemp -d)
trap 'rm -rf "$temp_dir"' EXIT HUP INT TERM
archive="$temp_dir/$model_name.zip"
unpack_dir="$temp_dir/unpacked"

validate_archive() {
	rm -rf "$unpack_dir"
	mkdir -p "$unpack_dir"
	if ! unzip -q "$1" -d "$unpack_dir"; then
		return 1
	fi
	if [ ! -f "$unpack_dir/$model_name/conf/model.conf" ]; then
		return 1
	fi
}

download_archive() {
	url=$1
	printf '%s\n' "Downloading local voice model from $url"
	if ! curl --fail --location --connect-timeout 15 --max-time 600 --retry 3 --retry-all-errors --silent --show-error \
		--output "$archive" "$url"; then
		rm -f "$archive"
		return 1
	fi
	if ! validate_archive "$archive"; then
		printf '%s\n' "Downloaded voice model archive is invalid: $url" >&2
		rm -f "$archive"
		return 1
	fi
	return 0
}

if [ -n "$archive_override" ]; then
	if [ ! -f "$archive_override" ]; then
		printf '%s\n' "VOICE_MODEL_ARCHIVE does not exist: $archive_override" >&2
		exit 1
	fi
	cp "$archive_override" "$archive"
	if ! validate_archive "$archive"; then
		printf '%s\n' "VOICE_MODEL_ARCHIVE is invalid: $archive_override" >&2
		exit 1
	fi
elif ! download_archive "$model_mirror_url"; then
	printf '%s\n' "GitHub model mirror unavailable; falling back to the official Vosk URL." >&2
	if ! download_archive "$model_official_url"; then
		printf '%s\n' "Unable to download a valid local voice model archive." >&2
		exit 1
	fi
fi

mkdir -p "$output_root/voice-models"
if [ -e "$model_dir" ]; then
	rm -rf "$model_dir"
fi
mv "$unpack_dir/$model_name" "$model_dir"
test -f "$model_dir/conf/model.conf"
printf '%s\n' "Voice model ready: $model_dir"
