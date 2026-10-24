# Prepare a durable data/ directory and migrate legacy in-place configs once.
param(
	[string] $Root = "."
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path -LiteralPath $Root).Path
$data = Join-Path $root "data"
New-Item -ItemType Directory -Force -Path $data | Out-Null

function Test-HasContent([string] $Path) {
	if (-not (Test-Path -LiteralPath $Path)) { return $false }
	return @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue).Count -gt 0
}

$hasDataBots = Test-HasContent (Join-Path $data "bots")
$hasDataCore = (Test-Path -LiteralPath (Join-Path $data "ts3audiobot.toml")) -or (Test-Path -LiteralPath (Join-Path $data "ts3audiobot.db"))

if (-not $hasDataBots -and -not $hasDataCore) {
	$legacyBots = Join-Path $root "bots"
	if (Test-HasContent $legacyBots) {
		Write-Host "Migrating legacy bots/ into data/bots ..."
		$targetBots = Join-Path $data "bots"
		New-Item -ItemType Directory -Force -Path $targetBots | Out-Null
		Copy-Item -Path (Join-Path $legacyBots "*") -Destination $targetBots -Recurse -Force -ErrorAction SilentlyContinue
	}
	foreach ($name in @("ts3audiobot.toml", "ts3audiobot.db", "rights.toml", "NLog.config")) {
		$src = Join-Path $root $name
		$dst = Join-Path $data $name
		if ((Test-Path -LiteralPath $src -PathType Leaf) -and -not (Test-Path -LiteralPath $dst)) {
			Write-Host "Migrating legacy $name into data/ ..."
			Copy-Item -LiteralPath $src -Destination $dst -Force
		}
	}
}

$configPath = Join-Path $data "ts3audiobot.toml"
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
	@"
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
"@ | Set-Content -LiteralPath $configPath -Encoding UTF8
}

if (Test-Path -LiteralPath $configPath -PathType Leaf) {
	$lines = @(Get-Content -LiteralPath $configPath)
	$desired = 'path = "../plugins"'

	# The program resolves [plugins] path relative to the working directory, which is
	# data/ for the packaged launcher. Any value pointing at the data folder (the
	# built-in default is "plugins") silently disables every plugin, so normalise the
	# value to the plugins folder that ships next to data/.
	$sectionStart = -1
	for ($i = 0; $i -lt $lines.Count; $i++) {
		if ($lines[$i].Trim() -eq '[plugins]') { $sectionStart = $i; break }
	}

	$changed = $false
	if ($sectionStart -ge 0) {
		# Find the end of the section (the next [header] or end of file).
		$sectionEnd = $lines.Count
		for ($i = $sectionStart + 1; $i -lt $lines.Count; $i++) {
			if ($lines[$i] -match '^\s*\[') { $sectionEnd = $i; break }
		}

		$pathIndex = -1
		for ($i = $sectionStart + 1; $i -lt $sectionEnd; $i++) {
			if ($lines[$i] -match '^\s*path\s*=') { $pathIndex = $i; break }
		}

		if ($pathIndex -ge 0) {
			if ($lines[$pathIndex] -ne $desired) {
				$lines[$pathIndex] = $desired
				$changed = $true
			}
		}
		else {
			# Section exists without a path key: insert one right after the header.
			$lines = @($lines[0..$sectionStart]) + @($desired) + @($lines[($sectionStart + 1)..($lines.Count - 1)])
			$changed = $true
		}
	}
	else {
		$lines = @($lines) + @('', '[plugins]', $desired)
		$changed = $true
	}

	if ($changed) {
		Set-Content -LiteralPath $configPath -Value $lines -Encoding UTF8
		Write-Host "Normalised [plugins] path to ../plugins in $configPath"
	}
}

New-Item -ItemType Directory -Force -Path (Join-Path $data "bots") | Out-Null
Write-Output $data
