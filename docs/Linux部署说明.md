# Linux 部署说明

本项目的 Linux 发布包目标为 `linux-x64`，适用于 64 位 x86 Linux 服务器。

## 安装系统依赖

Debian / Ubuntu：

```bash
sudo apt update
sudo apt install -y ffmpeg libopus0
```

CentOS / Rocky / AlmaLinux：

```bash
sudo dnf install -y ffmpeg opus
```

## 启动

```bash
tar -xzf TS3AudioBot-Kuwo-linux-x64.tar.gz
cd TS3AudioBot-Kuwo-linux-x64
chmod +x TS3AudioBot
./TS3AudioBot
```

首次运行会创建配置文件并要求完成 TeamSpeak 连接设置。此发布包是自包含版本，不需要另外安装 .NET。

点歌命令：

```text
!search 稻香
!play 1
!play 稻香
```

`!play` 不支持普通链接播放；它只用于酷我 API 点歌或选择上一次的搜索结果。
