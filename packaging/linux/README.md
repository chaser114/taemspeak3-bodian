# Linux 部署

Ubuntu 22.04 x64：

```bash
sudo apt update
sudo apt install -y ffmpeg libopus-dev
chmod +x start.sh
./start.sh
```

`ffmpeg` 用于音频转码，`libopus-dev` 提供 TeamSpeak 音频编码所需的 `libopus.so`。
