# Local Voice Control Work Intent

## Requested outcome

Implement a complete, optional, local-only voice-control capability for the TS3AudioBot-based project. It must remain a single-machine one-click deployment and include wake-word control, fixed playback commands, and song-name voice search/playback under one global switch.

## Scope

- Per-bot voice enable switch, default off.
- Fixed default wake word `音乐机器人`.
- Admin-editable custom wake word independent from bot nickname.
- Speaker-aware TeamSpeak voice ingress.
- Local bounded wake detection and post-wake command recognition.
- Existing player/Kuwo command integration.
- Windows/Linux package and GitHub Actions distribution.

## Non-goals

- No cloud or second-server ASR.
- No persistent recording, raw audio logs, or voice upload.
- No separate user-facing switch for song recognition or individual commands.
- No unrelated UI redesign or playback behavior change.

## Success evidence

- Existing project builds with voice disabled and behaves as before.
- Voice settings persist from the existing admin settings page.
- A synthetic or live TeamSpeak voice frame identifies its speaker and reaches the local recognizer.
- Wake-word-free speech produces no command.
- Wake word followed by a supported command reaches the existing command owner.
- Package artifacts contain the offline runtime/model and start without a second service.
- One-core playback benchmark shows whether idle and post-wake CPU stays within the audio stability budget.

## Stop conditions

- `needs-verification`: code builds but live TeamSpeak/model benchmark is unavailable.
- `blocked`: the selected local engine cannot be obtained or licensed for packaged single-machine use after safe alternatives are exhausted.
- `scope-exceeded`: implementation would require a second service, a new public deployment contract, or removing a user-approved command class.

## BaselineReadSetHint

- Required: local voice plan, TSLib audio packet/decoder seams, Bot lifecycle, ConfigStructs, generic settings UI, packaging scripts, release workflow.
- Acknowledged: existing `docs/aegis/` records are untracked project records; preserve them and do not mix them into source commits accidentally.

## BaselineUsageDraft

- Required baseline refs: listed in `docs/aegis/plans/2026-07-27-local-voice-control.md`.
- Delivered context refs: previous investigation summary and current repository inspection.
- Acknowledged before plan refs: TSLib voice receive support, existing Opus decoder, current 20 ms playback buffer, generic settings API.
- Cited in plan refs: yes.
- Missing refs: actual offline engine/model package availability and live one-core benchmark.
- Decision: needs-verification at engine/performance boundary; continue with bounded foundational slice.

## ImpactStatementDraft

- Affected layers: TSLib audio ingress, per-bot lifecycle, per-bot config, web settings, command/player integration, package distribution.
- Canonical owners: TSLib owns packet/codec metadata; bot voice service owns recognition state; ConfigStructs owns persisted settings; existing MainCommands/PlayManager own playback actions; packaging scripts own runtime/model distribution.
- Compatibility: disabled-by-default and no change to existing text/audio paths.
- Risks: CPU contention with the 20 ms playback path; native runtime/model size and licensing; arbitrary custom wake-word support.
