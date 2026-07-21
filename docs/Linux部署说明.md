# Linux 部署说明

Linux 发布包为 `TS3AudioBot-KuwoPlugin-linux-x64.tar.gz`，目标运行时为
`linux-x64`，适用于 64 位 x86 Linux 服务器。包内同时包含网页控制台和
`plugins/KuwoMusicPlugin.dll`，不再使用旧的 `TS3AudioBot-Kuwo-linux-x64` 包名。

## 启动

```bash
tar -xzf TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
cd TS3AudioBot-KuwoPlugin-linux-x64
chmod +x run/start-linux.sh
./run/start-linux.sh
```

`run/start-linux.sh` 会从脚本所在目录启动自包含的 `TS3AudioBot`，并在
Debian/Ubuntu 上安装 `ffmpeg`、`libopus0`、`libopus-dev`（没有叫 `libopus` 的包）。也可以直接运行根目录的
`start.sh`。首次启动后访问 `http://服务器IP:58913`，完成网页端账号和
TeamSpeak 连接配置。

## 从源码构建

```bash
chmod +x run/build-linux-package.sh
./run/build-linux-package.sh
```

构建脚本会构建 WebInterface、restore `linux-x64` runtime、发布主程序、复制
酷我插件和启动脚本，并在打包前校验 `WebInterface/bundle.js`、插件 DLL、
自包含可执行文件和 Linux 启动脚本。

## 点歌命令

```text
!search 稻香
!play 1
!play 稻香
```

`!play` 用于酷我 API 点歌或选择上一次搜索结果；网页端与 TeamSpeak 命令均可用。
