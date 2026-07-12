# Kuwo Music Plugin Setup

请阅读同目录的 `插件搭建包说明.md`。本文件使用 ASCII 文件名，方便 Linux 服务器解压后查看。

首次完成机器人连接后，向机器人发送：

```text
!plugin load KuwoMusicPlugin.dll
```

点歌：

```text
!play 稻香
!search 稻香
!play 0
```

Linux 依赖：

```bash
sudo apt update
sudo apt install -y ffmpeg libopus0
```
