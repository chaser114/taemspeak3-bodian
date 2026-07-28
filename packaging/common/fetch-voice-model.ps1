param(
	[Parameter(Mandatory = $true)]
	[string] $OutputRoot
)

$ErrorActionPreference = "Stop"
$modelName = "vosk-model-small-cn-0.22"
$modelMirrorUrl = if ([string]::IsNullOrWhiteSpace($env:VOICE_MODEL_MIRROR_URL)) { "https://github.com/chaser114/taemspeak3-bodian/releases/download/voice-model-vosk-model-small-cn-0.22/$modelName.zip" } else { $env:VOICE_MODEL_MIRROR_URL }
$modelOfficialUrl = "https://alphacephei.com/vosk/models/$modelName.zip"
$archiveOverride = $env:VOICE_MODEL_ARCHIVE
$modelDir = Join-Path (Join-Path $OutputRoot "voice-models") $modelName
$modelConfig = Join-Path (Join-Path $modelDir "conf") "model.conf"

if (Test-Path -LiteralPath $modelConfig -PathType Leaf) {
	Write-Host "Voice model already present: $modelDir"
	return
}

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ts3abot-voice-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
try {
	$archive = Join-Path $tempDir "$modelName.zip"
	$unpackDir = Join-Path $tempDir "unpacked"
	$unpackedModelDir = Join-Path $unpackDir $modelName

	function Test-VoiceModelArchive([string] $ArchivePath) {
		if (Test-Path -LiteralPath $unpackDir) {
			Remove-Item -LiteralPath $unpackDir -Recurse -Force
		}
		New-Item -ItemType Directory -Path $unpackDir -Force | Out-Null
		try {
			Expand-Archive -LiteralPath $ArchivePath -DestinationPath $unpackDir -Force
		} catch {
			return $false
		}
		return Test-Path -LiteralPath (Join-Path $unpackedModelDir "conf\model.conf") -PathType Leaf
	}

	function Get-VoiceModelArchive([string] $Url, [string] $ArchivePath) {
		Write-Host "Downloading local voice model from $Url"
		try {
			Invoke-WebRequest -Uri $Url -OutFile $ArchivePath -TimeoutSec 600 -MaximumRedirection 10
		} catch {
			if (Test-Path -LiteralPath $ArchivePath) { Remove-Item -LiteralPath $ArchivePath -Force }
			Write-Warning "Voice model download failed: $Url"
			return $false
		}
		if (-not (Test-VoiceModelArchive $ArchivePath)) {
			Remove-Item -LiteralPath $ArchivePath -Force -ErrorAction SilentlyContinue
			Write-Warning "Downloaded voice model archive is invalid: $Url"
			return $false
		}
		return $true
	}

	if (-not [string]::IsNullOrWhiteSpace($archiveOverride)) {
		if (-not (Test-Path -LiteralPath $archiveOverride -PathType Leaf)) {
			throw "VOICE_MODEL_ARCHIVE does not exist: $archiveOverride"
		}
		Copy-Item -LiteralPath $archiveOverride -Destination $archive -Force
		if (-not (Test-VoiceModelArchive $archive)) {
			throw "VOICE_MODEL_ARCHIVE is invalid: $archiveOverride"
		}
	} elseif (-not (Get-VoiceModelArchive $modelMirrorUrl $archive)) {
		Write-Warning "GitHub model mirror unavailable; falling back to the official Vosk URL."
		if (-not (Get-VoiceModelArchive $modelOfficialUrl $archive)) {
			throw "Unable to download a valid local voice model archive."
		}
	}

	$modelParent = Split-Path -Parent $modelDir
	New-Item -ItemType Directory -Path $modelParent -Force | Out-Null
	if (Test-Path -LiteralPath $modelDir) {
		Remove-Item -LiteralPath $modelDir -Recurse -Force
	}
	Move-Item -LiteralPath $unpackedModelDir -Destination $modelDir
	if (-not (Test-Path -LiteralPath $modelConfig -PathType Leaf)) {
		throw "Voice model installation did not produce conf/model.conf."
	}
	Write-Host "Voice model ready: $modelDir"
}
finally {
	if (Test-Path -LiteralPath $tempDir) {
		Remove-Item -LiteralPath $tempDir -Recurse -Force
	}
}
