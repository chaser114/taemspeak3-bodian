# Goal

Publish the web-console-enabled TS3AudioBot fork to `chaser114/taemspeak3-bodian` with a README based on the current remote README and working Docker, Linux, Windows, and local-build entry points.

# Architecture

The `TS3AudioBot` repository is the publish root. `WebInterface` is built into `WebInterface/dist` and hosted by `WebServer`; the Docker image builds the bot, plugin, and web console locally; packaged Linux and Windows builds carry their platform launcher files.

# Tech Stack

.NET 6, Vue 2 + TypeScript + webpack, Docker Compose, POSIX shell, Windows batch and PowerShell.

# Baseline/Authority Refs

- `origin/main:README.md` is the documentation baseline.
- `Dockerfile`, `docker-compose.yml`, and `packaging/**` are the deployment owners.
- `WebInterface/src/ts/**` and `TS3AudioBot/Web/**` are the web-console source owners.

# Compatibility Boundary

Existing web-console routes, persisted `data/` configuration, TeamSpeak connection fields, and release artifact names remain compatible. Documentation and convenience launchers must not alter runtime configuration ownership.

# Verification

- Build the web console with `NODE_OPTIONS=--openssl-legacy-provider npm.cmd run build`.
- Build the bot and plugin with the repository .NET runtime.
- Run shell syntax checks where a POSIX shell is available.
- Run the Windows packaging script and inspect the output contents.
- Run Docker build/compose checks when Docker is available; otherwise report the unavailable tool explicitly.
- Inspect the staged diff, commit, and push only intended repository files.

# Tasks

1. Merge web-console and all four deployment paths into the remote-baseline README.
2. Add root-level `run/` convenience launchers and a Docker build context ignore file.
3. Validate builds and package contents.
4. Commit and push the release branch or current publish branch using the configured `origin`.

# Risks and Retirement

The local machine has no Docker CLI or GitHub CLI, so Docker runtime validation and draft-PR creation may remain unavailable. Existing `packaging/**` launchers remain the canonical package entry points; `run/**` is a convenience wrapper and contains no duplicate runtime logic.
