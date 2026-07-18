#!/usr/bin/env sh
set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo=$(CDPATH= cd -- "$script_dir/../.." && pwd)
dotnet_cmd=${DOTNET:-dotnet}
npm_cmd=${NPM:-npm}
output_arg=${1:-dist/TS3AudioBot-KuwoPlugin-linux-x64}

case "$output_arg" in
	.|./|..|../*)
		printf '%s\n' "Choose a package directory, not the repository root or its parent." >&2
		exit 1
		;;
esac

case "$output_arg" in
	/*) output_dir=$output_arg ;;
	*) output_dir=$repo/$output_arg ;;
esac

case "$output_dir" in
	"$repo"|"$repo/"|"/")
		printf '%s\n' "Refusing to remove the repository root as the package output." >&2
		exit 1
		;;
esac

if ! command -v "$dotnet_cmd" >/dev/null 2>&1; then
	printf '%s\n' "dotnet SDK was not found. Install .NET 6 SDK or set DOTNET to its path." >&2
	exit 1
fi
if ! command -v "$npm_cmd" >/dev/null 2>&1; then
	printf '%s\n' "npm was not found. Install Node.js 20 or set NPM to its path." >&2
	exit 1
fi

printf '%s\n' "Building web console..."
(
	cd "$repo/WebInterface"
	"$npm_cmd" ci
	NODE_OPTIONS=--openssl-legacy-provider "$npm_cmd" run build
)

test -s "$repo/WebInterface/dist/index.html"
test -s "$repo/WebInterface/dist/bundle.js"
grep -Fq "bot-select-icon" "$repo/WebInterface/dist/bundle.js"

printf '%s\n' "Restoring Linux runtime assets..."
"$dotnet_cmd" restore "$repo/TS3AudioBot/TS3AudioBot.csproj" --runtime linux-x64 -p:SkipGitVersion=true
"$dotnet_cmd" restore "$repo/KuwoMusicPlugin/KuwoMusicPlugin.csproj" --runtime linux-x64 -p:SkipGitVersion=true

printf '%s\n' "Building bot and plugin..."
"$dotnet_cmd" build "$repo/KuwoMusicPlugin/KuwoMusicPlugin.csproj" -c Release -p:SkipGitVersion=true --no-restore
rm -rf "$output_dir"
mkdir -p "$output_dir"
"$dotnet_cmd" publish "$repo/TS3AudioBot/TS3AudioBot.csproj" -c Release -r linux-x64 -p:SkipGitVersion=true --self-contained true --no-restore -o "$output_dir"

plugin="$repo/KuwoMusicPlugin/bin/Release/net6.0/KuwoMusicPlugin.dll"
test -s "$plugin"
mkdir -p "$output_dir/plugins" "$output_dir/WebInterface" "$output_dir/run"
cp "$plugin" "$output_dir/plugins/KuwoMusicPlugin.dll"
cp -a "$repo/WebInterface/dist/." "$output_dir/WebInterface/"
cp "$repo/packaging/linux/start.sh" "$output_dir/start.sh"
cp "$repo/packaging/linux/install-linux.sh" "$output_dir/install-linux.sh"
cp "$repo/packaging/linux/README.md" "$output_dir/README.md"
cp "$repo/run/start-linux.sh" "$output_dir/run/start-linux.sh"

chmod +x "$output_dir/TS3AudioBot" "$output_dir/start.sh" "$output_dir/install-linux.sh" "$output_dir/run/start-linux.sh"
test -x "$output_dir/TS3AudioBot"
test -x "$output_dir/run/start-linux.sh"
test -s "$output_dir/plugins/KuwoMusicPlugin.dll"
test -s "$output_dir/WebInterface/index.html"
test -s "$output_dir/WebInterface/bundle.js"
grep -Fq "bot-select-icon" "$output_dir/WebInterface/bundle.js"

archive="$output_dir.tar.gz"
rm -f "$archive"
tar -C "$(dirname -- "$output_dir")" -czf "$archive" "$(basename -- "$output_dir")"
test -s "$archive"
printf '%s\n' "Linux package created: $archive"
