# Local Voice Control Checkpoint

## Current todo

- [completed] Plan and work intent recorded.
- [completed] Add voice configuration and settings UI.
- [completed] Wire speaker-aware TeamSpeak voice ingress.
- [completed] Integrate local recognition engine and command mapping (code/tests).
- [completed] Package model/runtime, bootstrap the GitHub model mirror, and verify builds/performance.
- [in_progress] Validate the updated VAD/phone-microphone path on the isolated server v3 runtime.

## Active slice

Configuration, settings, speaker-aware ingress, Vosk recognition ownership, parser coverage, model-fetch packaging hooks, builds, and parser tests are complete. The v2 trial with `HighQuality` VAD produced a headset no-response regression. Historical diagnostic logs show normal/headset frames around RMS `400~5000` were previously classified as speech, so the current experiment restores the prior `LowBitrate` mode without changing hangover or gain. An isolated v3 runtime is active on the server; no formal release directory or persistent data was changed.

## Evidence

- `TSLib/Audio/AudioPacketReader.cs` preserves `ClientId` in `Meta.In.Sender`.
- `TSLib/Audio/DecoderPipe.cs` already decodes Opus but defaults to 48 kHz output.
- `TSLib/Full/TsFullClient.cs` exposes incoming voice through `OutStream`.
- `TS3AudioBot/Audio/PreciseTimedPipe.cs` maintains only a 20 ms playback buffer by default.
- `docs/aegis/plans/2026-07-27-local-voice-control.md` defines the compatibility and verification boundary.
- `WebInterface`: `npm.cmd run build` passed with webpack exit 0.
- `packaging/common/fetch-voice-model.sh`: shell syntax check passed with Git's `sh`.
- `packaging/common/fetch-voice-model.ps1`: Windows PowerShell parser check passed.
- `git diff --check`: passed.
- `dotnet build TS3AudioBot/TS3AudioBot.csproj -c Release`: passed, 0 errors.
- `dotnet build KuwoMusicPlugin/KuwoMusicPlugin.csproj -c Release`: passed, 0 errors.
- `dotnet test TS3ABotUnitTests/TS3ABotUnitTests.csproj -c Release` with .NET 8 major roll-forward: 45 passed, 0 failed.
- Linux self-contained publish: passed and contains `libvosk.so` after the RID-path fix.
- Official `vosk-model-small-cn-0.22.zip` download: passed, 43,898,754 bytes, SHA-256 `3AF8B0E7E0F835AE9D414CE5DF580237A3CFB08D586C9FBBB0F7FF29AD5B14BA`, ZIP contains `vosk-model-small-cn-0.22/conf/model.conf`.
- Both model-fetch scripts: passed against the real archive override; Linux and Windows outputs each contain `voice-models/vosk-model-small-cn-0.22/conf/model.conf`.
- Packaging workflow now uses a GitHub Release mirror first, official Vosk fallback second, and publishes a validated model archive to the versioned mirror release on the first main-branch run.

## Drift check

- Intent: aligned with local-only, one-toggle, full-feature voice control.
- Compatibility: feature remains disabled by default; playback path was not changed.
- Retirement: no old voice path exists; model packaging is a new build-time path only.
- Evidence boundary: web/scripts/C# build/tests/publish and model archive validation verified; the mirror Release has not yet been created by Actions, and live TeamSpeak audio recognition plus one-core playback behavior remain unverified.
- New evidence: Linux publish contains `TS3AudioBot`, `TS3AudioBot.dll`, `libwebrtcvad.so`, and `libvosk.so`; 75 unit tests pass; server v2 starts on .NET 6.0.36, loads the local voice-control configuration, and connects to TeamSpeak.
- Regression evidence: the v2 source used `OperatingMode.HighQuality`; the old diagnostic path recorded `speech=True` for RMS values from roughly `400` to `5000`. The v3 build restores `OperatingMode.LowBitrate`, passes 75 tests, and connects to TeamSpeak.
- Decision: needs-verification; live phone/headset recognition rate, endpoint latency, CPU, and playback-underrun behavior remain external.

## Next action

Run the manual headset regression check against `/root/TS3AudioBot-KuwoPlugin-linux-x64.voice-test-20260730-lowbitrate`, then retrieve only recognition text/timing logs. Restore the previous temporary runtime after validation; do not publish a release from this slice.
