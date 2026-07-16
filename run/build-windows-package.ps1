param(
    [string] $OutputDirectory = "..\鍙戝竷鐗圽TS3AudioBot-KuwoPlugin-web-console-windows-x64-local"
)

$repo = (Resolve-Path "$PSScriptRoot\..").Path
& (Join-Path $repo "packaging\windows\build-package.ps1") -OutputDirectory $OutputDirectory
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
