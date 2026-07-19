# Foolproof non-destructive upgrade entry for Windows packages.
param(
	[Parameter(Mandatory = $false)]
	[string] $Source = "",
	[string] $InstallDir = ""
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$script = Join-Path $repo "packaging\common\update-in-place.ps1"

if (-not (Test-Path -LiteralPath $script)) {
	throw "update-in-place.ps1 not found. Use a release package that includes packaging/common/."
}

if ([string]::IsNullOrWhiteSpace($Source)) {
	Write-Host "用法: .\run\update-windows.ps1 -Source <新版本目录或.zip> [-InstallDir <安装目录>]"
	Write-Host "示例: .\run\update-windows.ps1 -Source ..\TS3AudioBot-KuwoPlugin-windows-x64-new"
	Write-Host "      .\run\update-windows.ps1 -Source ..\TS3AudioBot-KuwoPlugin-windows-x64.zip"
	Write-Host "说明: 只会替换程序文件，data\ 里的机器人与账号不会被覆盖。"
	exit 0
}

if ([string]::IsNullOrWhiteSpace($InstallDir)) {
	$InstallDir = $repo
}

& $script -Source $Source -InstallDir $InstallDir
