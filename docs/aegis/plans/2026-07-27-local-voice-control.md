# Local Voice Control Implementation Plan

## Goal

Add an optional, single-machine, offline voice-control system for each bot. When enabled, all supported voice commands require a configurable wake word. The default wake word is the fixed phrase `音乐机器人`; it is independent from the TeamSpeak nickname. The feature must be packaged for one-click Windows/Linux deployment and must not depend on a second server, cloud ASR service, external account, or runtime upload of audio.

## Architecture

TeamSpeak incoming voice packets will flow through the existing TSLib audio seam:

`TsFullClient.OutStream -> AudioPacketReader -> per-speaker Opus decode at 16 kHz mono -> local VAD/wake-word recognizer -> short command recognizer -> existing MainCommands/PlayManager operations`

The idle path performs only local silence filtering and wake-word detection. Full command recognition starts only after a wake-word match and uses an in-memory, bounded command window. The playback pipeline remains the protected real-time path; recognition work is never executed synchronously from the playback/audio-send thread.

The first offline engine candidate is Vosk with a small Chinese model because it supports runtime grammar restriction for a custom wake phrase and can run locally without a hosted service. The engine and model choice must pass an actual one-core playback benchmark before the feature is treated as release-ready.

## Tech Stack

- .NET 6 / C# 8
- Existing TSLib Opus decoder and `TsFullClient.OutStream`
- Existing TOML configuration and generic web settings API
- Existing Vue 2/Buefy settings page
- Vosk offline recognizer plus a packaged small Chinese model, subject to dependency/license/build verification

## Baseline / Authority Refs

- `TS3AudioBot/TSLib/Audio/AudioPacketReader.cs`: incoming speaker metadata and codec extraction
- `TS3AudioBot/TSLib/Audio/DecoderPipe.cs`: existing Opus decoding behavior
- `TS3AudioBot/TSLib/Full/TsFullClient.cs`: incoming voice packet callback seam
- `TS3AudioBot/TS3AudioBot/Bot.cs`: per-bot dependency graph and lifecycle owner
- `TS3AudioBot/TS3AudioBot/Config/ConfigStructs.cs`: per-bot configuration owner
- `TS3AudioBot/TS3AudioBot/Audio/Player.cs` and `TSLib/Audio/PreciseTimedPipe.cs`: playback timing and CPU-sensitive path
- `TS3AudioBot/WebInterface/src/ts/Pages/BotSettings.vue`: existing bot settings UI
- `TS3AudioBot/packaging/linux/build-package.sh`, `packaging/windows/build-package.ps1`, and `.github/workflows/build-release.yml`: release packaging boundaries

## Compatibility Boundary

- Voice control is disabled by default; existing text commands, music playback, web console behavior, and package startup remain unchanged when disabled.
- Existing bot nickname configuration remains independent from the wake word.
- Existing audio send timing and codec behavior must not be changed as part of the first voice slice unless a separate playback benchmark proves a bounded buffer improvement.
- No raw voice is written to disk, sent to a remote service, or retained beyond bounded in-memory recognition buffers.
- No per-command user-facing switches are introduced. The feature is enabled or disabled as one complete voice-control capability.

## Verification

- Unit tests for wake-word normalization, command-window state transitions, and command parsing.
- Audio-pipeline tests using synthetic Opus/PCM frames to prove speaker identity is preserved and idle frames are discarded.
- Build tests for Windows and Linux runtime restores/publish output.
- Web build test proving the new global voice switch and wake-word field are present and persisted through the generic settings API.
- Manual benchmark on a one-core target while music is playing: idle voice mode must not create playback underruns; post-wake recognition must be measured separately.

## Tasks

### 1. Add configuration and settings UI

Modify `TS3AudioBot/Config/ConfigStructs.cs` with a per-bot `voice` table containing `enabled = false` and `wake_word = "音乐机器人"`. Add the two fields to `WebInterface/src/ts/Pages/BotSettings.vue`; rely on the existing generic settings get/set path so changes persist without a new web API. Add validation for a non-empty, bounded wake phrase.

### 2. Add speaker-aware local audio ingress

Add a voice-control audio owner under `TS3AudioBot/Audio/` and wire it from `Bot.cs`. Use `TsFullClient.OutStream` and `AudioPacketReader` to obtain the sender client id. Decode voice packets per sender directly to 16 kHz mono PCM to avoid unnecessary stereo/downsampling work. When disabled, the owner must remain detached or discard frames immediately.

### 3. Add bounded local recognition stages

Add an offline recognition adapter with an idle wake-word grammar and a post-wake command grammar/full command recognizer. Keep recognition asynchronous and bounded per sender. Rebuild the wake recognizer when the configured wake word changes. Do not expose partial command toggles.

### 4. Map recognized commands to existing behavior

Normalize recognized Chinese text, require the wake word, and map pause/resume/next and song-search/play requests to the existing player and Kuwo command owners. Preserve the speaker id for auditing/permission decisions, while keeping the first product behavior available to all users as previously agreed.

### 5. Package the offline engine and model

Update the project/package restore and Windows/Linux/GitHub Actions packaging paths so the self-contained packages include the native recognition runtime and small Chinese model. Runtime startup must fail clearly when voice is enabled but the local model is missing; voice remains disabled by default for old installations.

### 6. Verify performance and release boundaries

Build the bot, plugin, web console, Windows package, and Linux package. Run unit tests and a manual audio benchmark. Record CPU and playback-underrun observations before enabling voice by default or changing the playback buffer.

## Risks and Stop Conditions

- A custom runtime wake grammar may be feasible with Vosk but must be measured; do not substitute an always-on full ASR loop if it violates the CPU budget.
- If the selected offline engine cannot keep idle CPU low enough on the target one-core host, stop at the engine boundary and reassess the model/decoder rather than adding a remote service or silently removing song commands.
- If the current baseline playback already underruns without voice enabled, diagnose that separately before using voice benchmarks as evidence.

## Retirement

No existing voice owner or fallback exists. The new ingress/recognition owner replaces no current path. Any temporary probe or benchmark-only logging must remain out of the release path or have an explicit removal step before release.
