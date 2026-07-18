# Linux 部署

Linux 发布包名称为 `TS3AudioBot-KuwoPlugin-linux-x64.tar.gz`，包含主程序、
`plugins/KuwoMusicPlugin.dll`、网页控制台和启动脚本。适用于 64 位 x86 Linux。

```bash
tar -xzf TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
cd TS3AudioBot-KuwoPlugin-linux-x64
chmod +x run/start-linux.sh
./run/start-linux.sh
```

`run/start-linux.sh` 会切换到发布包目录，安装 Debian/Ubuntu 所需的
`ffmpeg` 和 `libopus0`，再以非交互方式启动机器人。启动后访问
`http://服务器IP:58913`，完成网页端首次管理员设置和 TeamSpeak 连接配置。

也可以直接运行包根目录的 `start.sh`。数据保存在运行目录的 `data/` 中，请勿删除。

## 从源码构建

在 Linux 服务器或 Linux 构建机的仓库根目录执行：

```bash
chmod +x run/build-linux-package.sh
./run/build-linux-package.sh
```

脚本会构建网页、restore `linux-x64` 运行时、发布自包含机器人、复制酷我插件，
并在生成 `.tar.gz` 前校验网页入口、当前前端 bundle、插件 DLL 和启动脚本。
