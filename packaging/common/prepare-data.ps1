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
	$content = Get-Content -LiteralPath $configPath -Raw
	if ($content -match '(?ms)(\[plugins\][^\[]*?)^\s*path\s*=\s*"plugins"\s*$') {
		$updated = [regex]::Replace(
			$content,
			'(?ms)(\[plugins\][^\[]*?)^\s*path\s*=\s*"plugins"\s*$',
			'$1path = "../plugins"',
			1
		)
		if ($updated -ne $content) {
			Set-Content -LiteralPath $configPath -Value $updated -Encoding UTF8
		}
	}
}

New-Item -ItemType Directory -Force -Path (Join-Path $data "bots") | Out-Null
Write-Output $data
