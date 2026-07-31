# Voice Control Latency and Accuracy Repair Plan

## Goal

Repair the existing optional local voice-control feature so that game-headset
speech is recognized reliably and supported commands respond promptly. The
release target is a complete voice path, not a diagnostic-only threshold tweak.

The acceptance target is:

- pause, resume, and next execute within 700 ms after the speaker stops;
- play-song commands begin search within 1.2 s after the speaker stops;
- wake-word false activation is not introduced by ordinary conversation;
- idle and multi-speaker recognition does not create playback underruns on the
  one-core target server;
- headset, phone, and two-speaker tests are all recorded before release.

## Architecture

Keep `TsFullClient.OutStream -> AudioPacketReader -> VoiceControlService` as
the canonical audio owner. Add a speech-activity gate that uses WebRTC VAD on
decoded 16 kHz mono PCM with pre-roll and hangover, but never discards PCM
inside an active speech segment. TeamSpeak `EmptyTick` remains the primary
segment-end signal.

Use a per-speaker state machine:

`Idle -> WakeCandidate -> Armed -> Command -> Committed`

Wake recognition runs only for VAD-active segments. A single partial result does
not arm the command stage. A complete wake-only segment arms the speaker for a
short follow-up command window; a wake-plus-command segment is parsed as one
utterance. Control commands commit after two stable partial results and are
deduplicated at segment finalization. Song commands wait for the endpoint,
produce up to three final alternatives, and pass candidate queries through the
existing resolver/search owner before playback.

The audio worker remains isolated from the TeamSpeak packet callback. It must
not write per-frame logs, perform network search, or block playback threads.

## Tech Stack

- .NET 6 / C# 8
- Existing `TSLib.Audio.AudioPacketReader` and Opus decoder
- Vosk 0.3.38 for offline wake and command recognition
- `WebRtcVadSharp` 1.3.2 for Windows and `WebRtcVadSharpLinux` 1.3.2 for
  Linux, selected by `RuntimeIdentifier`; both expose the same WebRTC VAD API
- Existing `ResolveContext`, `PlayManager`, and Kuwo resolver for candidate search
- NUnit tests in `TS3ABotUnitTests`
- Existing Linux and Windows package scripts

## Baseline / Authority Refs

- `docs/aegis/plans/2026-07-27-local-voice-control.md`
- `TS3AudioBot/Audio/VoiceControlService.cs`
- `TS3AudioBot/Audio/VoiceCommandParser.cs`
- `TS3AudioBot/Audio/PlayManager.cs`
- `TS3AudioBot/ResourceFactories/ResolveContext.cs`
- `TSLib/Audio/AudioPacketReader.cs`
- `TS3ABotUnitTests/VoiceCommandParserTests.cs`
- `TS3ABotUnitTests/VoiceCommandTimingTests.cs`
- `packaging/linux/build-package.sh`
- `packaging/windows/build-package.ps1`
- `.github/workflows/build-release.yml`

## Compatibility Boundary

- Voice control remains disabled by default.
- Existing text commands, playback, web console behavior, bot nickname, and
  voice configuration API remain unchanged.
- No raw PCM or recognition audio is written to disk or sent to a remote ASR
  service.
- No GitHub/GitCode push, release version, or automatic server update is part
  of this repair.
- Temporary server validation uses an external backup and restores the original
  program files before cleanup.
- The existing small Chinese model remains the default. A larger model is only
  considered if the measured song-query acceptance target cannot be met without
  it; it is not silently substituted during this repair.

## Verification

Automated:

- `dotnet test TS3ABotUnitTests/TS3ABotUnitTests.csproj --no-restore`
- `dotnet build TS3AudioBot/TS3AudioBot.csproj --configuration Release -p:SkipGitVersion=true --no-restore`
- Linux self-contained publish and package-content checks
- Windows self-contained publish and native-runtime checks

Manual server matrix:

- 20 headset utterances: wake + pause/resume/next;
- 10 headset song queries including artist and title;
- 10 phone utterances at the previously tested distance;
- 5 minutes of ordinary conversation without the wake word;
- two users speaking at overlapping and separate times;
- CPU and playback-underrun observation with music playing.

Record for every recognized command: input profile, sender, final text,
execution timestamp, endpoint-to-execution milliseconds, and whether the
command was committed early or at finalization. Do not record raw audio.

## Change Necessity

- User-visible need: voice control currently has both low recognition quality
  and a 3-4 second perceived response delay.
- No-change option: continue using the current small Vosk recognizers and
  timing fallback; this cannot provide early control execution or robust speech
  segmentation.
- Minimum code boundary: `VoiceControlService`, the parser, the playback
  resolver boundary, tests, and package dependency handling.
- Decision: code-change.

## Existence Check

- Proposed new surface: speech-activity detector and song-candidate matcher.
- Existing owner / reuse candidate: `VoiceControlService` owns audio state;
  `ResolveContext` and `PlayManager` own resource search and playback.
- Why existing surface is insufficient: the current service has no robust VAD
  boundary or multi-candidate song contract, while adding search logic to the
  packet reader would violate the playback boundary.
- Decision: add the two small helpers under `TS3AudioBot/Audio/`, with
  `VoiceControlService` and `PlayManager` remaining the canonical owners.

## Architecture Integrity Lens

- Invariant: TeamSpeak packet ingestion must never wait on recognition or
  network search.
- Canonical owners: audio segmentation/state in `VoiceControlService`; text
  matching in `VoiceCommandParser`; resource search/playback in
  `ResolveContext`/`PlayManager`.
- Responsibility overlap to avoid: no VAD logic in `AudioPacketReader`, no
  search scoring in the Opus callback, and no per-frame logging in the worker.
- Verdict: repair existing owners and add only bounded helpers.

## Tasks

### 1. Add deterministic VAD and segment buffering

Files: `TS3AudioBot/TS3AudioBot.csproj`,
`TS3AudioBot/Audio/SpeechActivityDetector.cs`,
`TS3ABotUnitTests/SpeechActivityDetectorTests.cs`,
`packaging/linux/build-package.sh`, and `packaging/windows/build-package.ps1`.

Add conditional package references to `WebRtcVadSharp` 1.3.2 for `win-x64` and
`WebRtcVadSharpLinux` 1.3.2 for `linux-x64`. Add a WebRTC VAD adapter for 20 ms,
16 kHz mono PCM using aggressiveness mode 2, 200 ms pre-roll, and 300 ms
hangover. Expose only `IsSpeech`, `OnSpeechStart`, and `OnSpeechEnd` behavior
needed by the service. Do not expose raw thresholds as user settings. Test
silence, steady speech, quiet speech, leading speech, trailing speech, and a
speech/noise transition. Package checks must find `WebRtcVad.dll` in Windows
output and `libwebrtcvad.so` in Linux output.

Verification: the new tests pass; publish output contains the required native
VAD assets on both target runtimes; no VAD frame is processed on the packet
receiver thread.

### 2. Replace timing-only recognition with a bounded per-speaker state machine

Files: `TS3AudioBot/Audio/VoiceControlService.cs` and
`TS3ABotUnitTests/VoiceCommandTimingTests.cs`.

Use VAD to open and close recognition segments while retaining all PCM in the
active segment. Use `EmptyTick` as the first endpoint signal and a 500 ms
fallback only when no endpoint arrives. Remove the current per-frame reset
behavior as the normal completion path. Keep command timeout as a safety cap,
not as normal user-visible latency.

Confirm a wake word across stable partial results or a final segment result.
Keep a wake-only speaker armed for 2.5 seconds. Feed a wake-plus-command
utterance through the complete segment path so the wake phrase cannot consume
the first command words. Bound per-speaker buffers and ensure stale frames are
dropped with counters rather than unbounded queue growth.

Verification: timing tests cover wake-only, wake-plus-command, missing
`EmptyTick`, repeated segments, duplicate finalization, and queue saturation.

### 3. Add early commit for control commands

Files: `TS3AudioBot/Audio/VoiceControlService.cs`,
`TS3AudioBot/Audio/VoiceCommandParser.cs`,
`TS3ABotUnitTests/VoiceCommandParserTests.cs`, and focused service tests if
the existing test seam permits them.

Use the current command parser as the canonical text owner. Add stable-partial
recognition for pause, resume, next, and previous aliases: the same command
must appear twice before commit. Mark the segment committed so its final result
cannot execute the command a second time. Keep song playback on the final-result
path because its query must be complete.

Verification: parser tests cover common Vosk Chinese substitutions, command
aliases, wake-word variants, ordinary conversation, and duplicate suppression.
Manual timing records prove control actions execute within 700 ms after speech
end on the headset path.

### 4. Use bounded recognition alternatives and resolver-side song matching

Files: `TS3AudioBot/Audio/VoiceControlService.cs`,
`TS3AudioBot/Audio/VoiceCommandParser.cs`, `TS3AudioBot/Audio/PlayManager.cs`,
`TS3AudioBot/ResourceFactories/ResolveContext.cs`, and new focused matcher
tests under `TS3ABotUnitTests`.

Keep wake recognition single-result. Enable at most three alternatives only
for final song commands. Preserve the recognized candidate list instead of
logging or storing audio. Add a `PlayManager` path that searches the existing
Kuwo resolver for the candidate queries, scores normalized artist/title matches,
and plays one selected `AudioResource`; it must not start multiple songs while
scoring.

Verification: unit tests cover exact title, artist-plus-title, wrong first
candidate with correct second candidate, empty candidates, and resolver failure.
Manual tests include `周杰伦 稻香`, control commands, and a search timeout.

### 5. Protect latency and playback under load

Files: `TS3AudioBot/Audio/VoiceControlService.cs`,
`TS3AudioBot/Audio/PlayManager.cs`, and focused tests/diagnostic counters.

Keep all network search and playback scheduling off the audio worker. Add
low-frequency metrics for queue depth, oldest frame age, VAD segment duration,
and endpoint-to-execution latency. Do not emit per-frame logs. Serialize command
execution through the existing scheduler while allowing recognition cleanup to
continue independently.

Verification: build and unit tests pass; server test records idle CPU, command
CPU, queue age, and playback underrun observations for headset, phone, and two
speakers. Any playback underrun is a stop condition for release.

### 6. Package and perform temporary server validation

Files: Vosk/VAD package project files and existing Linux/Windows package
scripts only where publish validation proves they are required; no workflow
upload or release changes.

Build with:

`dotnet restore TS3AudioBot/TS3AudioBot.csproj --runtime linux-x64 -p:SkipGitVersion=true`

`dotnet publish TS3AudioBot/TS3AudioBot.csproj --configuration Release --runtime linux-x64 --self-contained true -p:SkipGitVersion=true --no-restore --output <temporary-output>`

Deploy the temporary package to `/root/TS3AudioBot-KuwoPlugin-linux-x64` only
after backing up program files outside the installation directory. Preserve
`data`, `logs`, `backup`, and the PID file boundary. After manual validation,
restore the original package and delete all temporary server and local files.

No GitHub/GitCode push, version increment, or release upload occurs in this
task.

## Risks

- WebRTC VAD native packaging may add Windows/Linux assets; publish checks must
  fail closed if either runtime cannot load it.
- The small Chinese Vosk model may still miss song titles even after segmentation
  and candidate search improvements. If the measured song target fails, stop
  before release and evaluate a larger command-only model rather than hiding the
  failure with more fuzzy matching.
- Early control commit can fire on a stable false partial; the two-result
  confirmation and wake-word requirement are mandatory safeguards.
- Dropping stale queue frames can reduce recognition completeness. Queue age and
  final text must be recorded so this trade-off is visible.

## Retirement

The fixed RMS diagnostic path and per-frame `VOICE_VAD` logging are not release
features and must remain absent. The current final-only control execution is
replaced by early commit with final-result deduplication. Temporary deployment
archives, backups, and logs are deleted after server validation.
