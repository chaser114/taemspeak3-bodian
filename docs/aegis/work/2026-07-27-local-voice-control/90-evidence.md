# Local Voice Control Evidence

## Verified

- `npm.cmd run build` in `WebInterface`: exit 0.
- `sh -n packaging/common/fetch-voice-model.sh` and `sh -n packaging/linux/build-package.sh`: exit 0.
- Windows PowerShell parser check for `packaging/common/fetch-voice-model.ps1`: no syntax errors.
- `git diff --check`: exit 0.
- `dotnet build TS3AudioBot/TS3AudioBot.csproj --configuration Release -p:SkipGitVersion=true --no-restore`: exit 0, 0 errors.
- `dotnet build KuwoMusicPlugin/KuwoMusicPlugin.csproj --configuration Release -p:SkipGitVersion=true --no-restore`: exit 0, 0 errors.
- `DOTNET_ROLL_FORWARD=Major dotnet test TS3ABotUnitTests/TS3ABotUnitTests.csproj --configuration Release -p:SkipGitVersion=true --no-restore`: 45 passed, 0 failed.
- Linux self-contained publish with `-r linux-x64`: exit 0; after the RID path fix the output contains `libvosk.so`.
- Official `vosk-model-small-cn-0.22.zip`: downloaded successfully, size `43,898,754` bytes, SHA-256 `3AF8B0E7E0F835AE9D414CE5DF580237A3CFB08D586C9FBBB0F7FF29AD5B14BA`, and contains `vosk-model-small-cn-0.22/conf/model.conf`.
- `fetch-voice-model.sh` and `fetch-voice-model.ps1`: both installed the real archive successfully when supplied through `VOICE_MODEL_ARCHIVE`; each output contained `voice-models/vosk-model-small-cn-0.22/conf/model.conf`.
- `.github/workflows/build-release.yml`: now stages the validated Linux model into a reusable archive, reuses it for Windows, and creates/repairs the pinned GitHub Release mirror asset.

## Not verified here

- The mirror Release asset is not yet present because the updated workflow has not been executed on the main branch.
- No live TeamSpeak voice frame was injected, so wake-word detection and command execution were not tested against real speech.
- No one-core playback benchmark was run, so CPU contention and music jitter remain an external verification item.
- The latest local build initially failed only because `OperatingMode.Quality` is not present in `WebRtcVadSharp` 1.3.2; the corrected enum is `OperatingMode.HighQuality`.
- `dotnet test` with the project-local .NET 6 runtime completed with 75 passed and 0 failed.
- Linux self-contained publish completed with 0 errors and contains `TS3AudioBot`, `TS3AudioBot.dll`, `libwebrtcvad.so`, and `libvosk.so`.
- The isolated server v2 process is running at `/root/TS3AudioBot-KuwoPlugin-linux-x64.voice-test-20260730-v2`, enabled with wake word `音乐机器人`, and connected to TeamSpeak.
- No real microphone utterance has been collected in this slice, so recognition success rate and endpoint-to-execution latency remain unverified.
- The v2 `HighQuality` trial caused the user-reported headset no-response regression. A single-variable v3 build restored `OperatingMode.LowBitrate`; local build and 75-test verification passed, and the v3 server process connected successfully. User microphone confirmation is still pending.

## Boundary

The package scripts download the model at build time; the runtime only reads the packaged local model and does not upload or persist voice audio. Voice remains disabled by default.
