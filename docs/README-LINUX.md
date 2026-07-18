# Linux 部署说明

请使用带网页端的 `TS3AudioBot-KuwoPlugin-linux-x64.tar.gz`，不要使用旧的
`TS3AudioBot-Kuwo-linux-x64.tar.gz`。

```bash
tar -xzf TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
cd TS3AudioBot-KuwoPlugin-linux-x64
chmod +x run/start-linux.sh
./run/start-linux.sh
```

发布包包含 `plugins/KuwoMusicPlugin.dll`、`WebInterface` 和启动脚本。启动后
访问 `http://服务器IP:58913` 完成首次网页配置。

从源码构建 Linux 包：

```bash
chmod +x run/build-linux-package.sh
./run/build-linux-package.sh
```
