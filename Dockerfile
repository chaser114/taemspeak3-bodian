FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
RUN apt-get update && apt-get install -y curl ca-certificates && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && apt-get install -y nodejs && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY . .
RUN cd WebInterface && npm ci && NODE_OPTIONS=--openssl-legacy-provider npm run build
RUN dotnet restore TS3AudioBot/TS3AudioBot.csproj --runtime linux-x64 -p:SkipGitVersion=true \
    && dotnet restore KuwoMusicPlugin/KuwoMusicPlugin.csproj --runtime linux-x64 -p:SkipGitVersion=true \
    && dotnet build KuwoMusicPlugin/KuwoMusicPlugin.csproj -c Release -p:SkipGitVersion=true --no-restore \
    && dotnet publish TS3AudioBot/TS3AudioBot.csproj -c Release -r linux-x64 -p:SkipGitVersion=true --self-contained true --no-restore -o /app
RUN mkdir -p /app/plugins /app/WebInterface \
    && cp KuwoMusicPlugin/bin/Release/net6.0/KuwoMusicPlugin.dll /app/plugins/ \
    && cp -a WebInterface/dist/. /app/WebInterface/ \
    && test -s /app/plugins/KuwoMusicPlugin.dll \
    && test -s /app/WebInterface/index.html \
    && test -s /app/WebInterface/bundle.js \
    && grep -Fq "bot-select-icon" /app/WebInterface/bundle.js
FROM debian:12-slim
RUN apt-get update && apt-get install -y ffmpeg libopus0 ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=build /app /app
RUN chmod +x /app/TS3AudioBot
EXPOSE 58913
VOLUME ["/data"]
WORKDIR /data
ENTRYPOINT ["/app/TS3AudioBot"]
CMD ["--config", "/data/ts3audiobot.toml"]
