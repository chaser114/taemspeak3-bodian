# Docker 本地构建部署

此方式不会从 GitHub 拉取 Docker 成品镜像。服务器在当前项目目录本地构建机器人、酷我插件和网页控制台。

```bash
chmod +x packaging/docker/install-docker.sh
./packaging/docker/install-docker.sh
```

构建完成后访问：

```text
http://服务器IP:58913
```

首次访问时创建管理员账号，再填写 TeamSpeak 服务器地址和密码。`data/` 目录保存机器人配置、网页账号和数据；不要删除它。

升级时只需重新构建并启动（数据卷会保留）：

```bash
./packaging/docker/install-docker.sh
# 或
docker compose up -d --build
```

停止服务：

```bash
docker compose down
```
