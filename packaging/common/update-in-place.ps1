# Non-destructive in-place upgrade: replace program files only, never touch data/.
param(
	[Parameter(Mandatory = $true)]
	[string] $Source,
	[string] $InstallDir = "."
)

$ErrorActionPreference = "Stop"

function Get-FullPath([string] $Path) {
	return [System.IO.Path]::GetFullPath($Path)
}

if ($Source -eq "-h" -or $Source -eq "--help") {
	Write-Host "Usage: update-in-place.ps1 -Source <new-package-dir-or-zip> [-InstallDir <existing-install>]"
	exit 0
}

$installDir = Get-FullPath $InstallDir
if (-not (Test-Path -LiteralPath $installDir -PathType Container)) {
	throw "Install directory not found: $installDir"
}

$tempRoot = $null
try {
	$sourcePath = Get-FullPath $Source
	$srcDir = $null

	if ((Test-Path -LiteralPath $sourcePath -PathType Leaf) -and ($sourcePath -match '\.zip$')) {
		$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ts3ab-update-" + [Guid]::NewGuid().ToString("N"))
		New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
		Expand-Archive -LiteralPath $sourcePath -DestinationPath $tempRoot -Force
		$dirs = @(Get-ChildItem -LiteralPath $tempRoot -Directory)
		if ($dirs.Count -eq 1) { $srcDir = $dirs[0].FullName } else { $srcDir = $tempRoot }
	} elseif (Test-Path -LiteralPath $sourcePath -PathType Container) {
		$srcDir = $sourcePath
	} else {
		throw "Source package not found: $Source"
	}

	$hasLinux = Test-Path -LiteralPath (Join-Path $srcDir "TS3AudioBot")
	$hasWindows = Test-Path -LiteralPath (Join-Path $srcDir "TS3AudioBot.exe")
	if (-not $hasLinux -and -not $hasWindows) {
		throw "Source package does not look like a TS3AudioBot release: $srcDir"
	}
	if ($srcDir -eq $installDir) {
		throw "Source and install directories are the same."
	}

	$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
	$backupDir = Join-Path $installDir "backup\pre-update-$stamp"
	New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
	Write-Host "Backing up user data to $backupDir ..."
	foreach ($name in @("data", "bots", "ts3audiobot.toml", "ts3audiobot.db", "rights.toml", "NLog.config")) {
		$item = Join-Path $installDir $name
		if (Test-Path -LiteralPath $item) {
			Copy-Item -LiteralPath $item -Destination (Join-Path $backupDir $name) -Recurse -Force -ErrorAction SilentlyContinue
		}
	}

	Get-Process -Name "TS3AudioBot" -ErrorAction SilentlyContinue | ForEach-Object {
		try {
			if ($_.Path -and $_.Path.StartsWith($installDir, [System.StringComparison]::OrdinalIgnoreCase)) {
				Write-Host "Stopping running TS3AudioBot (PID $($_.Id)) ..."
				$_.CloseMainWindow() | Out-Null
				Start-Sleep -Seconds 1
				if (-not $_.HasExited) { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
			}
		} catch { }
	}

	$protected = @(
		"data", "bots", "ts3audiobot.toml", "ts3audiobot.db", "rights.toml", "NLog.config", "logs", "backup"
	)

	Write-Host "Updating program files (data/ is never overwritten) ..."
	Get-ChildItem -LiteralPath $srcDir -Force | ForEach-Object {
		if ($protected -contains $_.Name) { return }
		$dest = Join-Path $installDir $_.Name
		if ($_.PSIsContainer) {
			if (Test-Path -LiteralPath $dest) {
				Remove-Item -LiteralPath $dest -Recurse -Force
			}
			Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse -Force
		} else {
			Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
		}
	}

	$prepare = Join-Path $installDir "packaging\common\prepare-data.ps1"
	if (-not (Test-Path -LiteralPath $prepare)) {
		$prepare = Join-Path $srcDir "packaging\common\prepare-data.ps1"
	}
	if (Test-Path -LiteralPath $prepare) {
		& $prepare -Root $installDir | Out-Null
	}

	Write-Host "Upgrade finished."
	Write-Host "  Install : $installDir"
	Write-Host "  Backup  : $backupDir"
	Write-Host "  Data    : $(Join-Path $installDir 'data')  (preserved)"
	Write-Host "Start with: start-web-console.bat"
}
finally {
	if ($tempRoot -and (Test-Path -LiteralPath $tempRoot)) {
		Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
	}
}
