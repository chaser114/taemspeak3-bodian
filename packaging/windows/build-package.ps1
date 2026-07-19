param(
	[string] $OutputDirectory = "..\发布版\TS3AudioBot-KuwoPlugin-web-console-windows-x64-fixed",
	[string] $FfmpegPath = "..\搭建包\TS3AudioBot-KuwoPlugin-windows-x64\ffmpeg.exe"
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$workspaceRoot = (Resolve-Path "$projectRoot\..").Path
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
$ffmpegPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $FfmpegPath))

if (-not (Test-Path -LiteralPath $ffmpegPath -PathType Leaf)) { throw "ffmpeg.exe was not found: $ffmpegPath" }

$dotnet = Join-Path $workspaceRoot ".dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { $dotnet = "dotnet" }

if (Test-Path -LiteralPath $outputPath) { throw "Output directory already exists: $outputPath" }

$env:NODE_OPTIONS = "--openssl-legacy-provider"
Push-Location (Join-Path $projectRoot "WebInterface")
try { & npm.cmd run build; if ($LASTEXITCODE -ne 0) { throw "Web build failed." } } finally { Pop-Location }

& $dotnet restore (Join-Path $projectRoot "TS3AudioBot\TS3AudioBot.csproj") --runtime win-x64 -p:SkipGitVersion=true
if ($LASTEXITCODE -ne 0) { throw "Windows runtime restore failed." }

& $dotnet restore (Join-Path $projectRoot "KuwoMusicPlugin\KuwoMusicPlugin.csproj") --runtime win-x64 -p:SkipGitVersion=true
if ($LASTEXITCODE -ne 0) { throw "Plugin runtime restore failed." }

& $dotnet build (Join-Path $projectRoot "KuwoMusicPlugin\KuwoMusicPlugin.csproj") -c Release -p:SkipGitVersion=true --no-restore
if ($LASTEXITCODE -ne 0) { throw "Kuwo plugin build failed." }

& $dotnet publish (Join-Path $projectRoot "TS3AudioBot\TS3AudioBot.csproj") -c Release -r win-x64 -p:SkipGitVersion=true --self-contained true --no-restore -o $outputPath
if ($LASTEXITCODE -ne 0) { throw "Windows publish failed." }

$pluginOutput = Join-Path $projectRoot "KuwoMusicPlugin\bin\Release\net6.0\KuwoMusicPlugin.dll"
if (-not (Test-Path -LiteralPath $pluginOutput -PathType Leaf)) { throw "Kuwo plugin build output was not found: $pluginOutput" }
Copy-Item -LiteralPath (Join-Path $projectRoot "WebInterface\dist") -Destination (Join-Path $outputPath "WebInterface") -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $outputPath "plugins") | Out-Null
Copy-Item -LiteralPath $pluginOutput -Destination (Join-Path $outputPath "plugins\KuwoMusicPlugin.dll") -Force
Copy-Item -LiteralPath $ffmpegPath -Destination (Join-Path $outputPath "ffmpeg.exe") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "start.bat") -Destination (Join-Path $outputPath "start.bat") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "start-web-console.bat") -Destination (Join-Path $outputPath "start-web-console.bat") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $outputPath "run") | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot "run\start-web-console.bat") -Destination (Join-Path $outputPath "run\start-web-console.bat") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "run\update-windows.ps1") -Destination (Join-Path $outputPath "run\update-windows.ps1") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "run\update-windows.bat") -Destination (Join-Path $outputPath "run\update-windows.bat") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "run\update-windows.bat") -Destination (Join-Path $outputPath "update-windows.bat") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "run\stop-windows.bat") -Destination (Join-Path $outputPath "run\stop-windows.bat") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "run\stop-windows.bat") -Destination (Join-Path $outputPath "stop.bat") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $outputPath "packaging\common") | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot "packaging\common\prepare-data.ps1") -Destination (Join-Path $outputPath "packaging\common\prepare-data.ps1") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "packaging\common\update-in-place.ps1") -Destination (Join-Path $outputPath "packaging\common\update-in-place.ps1") -Force
# Empty data placeholder so users see the durable data directory in the package.
$dataDir = Join-Path $outputPath "data"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
Set-Content -LiteralPath (Join-Path $dataDir "README.txt") -Value "This folder stores bots, web accounts and rights. Keep it when upgrading." -Encoding UTF8
# Local builds get a timestamp version; CI overwrites with build-N.
$versionPath = Join-Path $outputPath "VERSION"
if (-not (Test-Path -LiteralPath $versionPath)) {
	Set-Content -LiteralPath $versionPath -Value ("local-" + (Get-Date -Format "yyyyMMddHHmmss")) -Encoding UTF8
}

Write-Host "Windows package created: $outputPath"
