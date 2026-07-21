# Linux 部署

Linux 发布包名称为 `TS3AudioBot-KuwoPlugin-linux-x64.tar.gz`，包含主程序、
`plugins/KuwoMusicPlugin.dll`、网页控制台和启动脚本。适用于 64 位 x86 Linux。

## 首次安装

```bash
tar -xzf TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
cd TS3AudioBot-KuwoPlugin-linux-x64
chmod +x run/start-linux.sh
./run/start-linux.sh
```

`run/start-linux.sh` 会：

1. 安装并**校验** `ffmpeg`、`libopus0`、`libopus-dev`（Debian/Ubuntu）
2. 在包内创建 `lib/libopus.so*` 符号链接（双保险）
3. 准备 `data/` 后启动机器人

说明：系统里**没有**名为 `libopus` 的包。  
- `libopus0` 只有运行库 `libopus.so.0`  
- 机器人按名字加载 `libopus`，需要 **`libopus-dev`** 提供的无版本号链接 `libopus.so`

启动后访问 `http://服务器IP:58913`，完成网页端首次管理员设置和 TeamSpeak 连接配置。

若仍提示找不到 libopus，手动执行：

```bash
sudo apt-get install -y libopus0 libopus-dev ffmpeg
ldconfig -p | grep opus
./run/start-linux.sh
```

也可以直接运行包根目录的 `start.sh`。

## 数据目录（重要）

所有用户数据都在 **`data/`** 中，与程序文件分开：

- `data/bots/`：每个机器人的连接、频道、昵称等
- `data/ts3audiobot.db`：网页账号、会话、播放历史
- `data/rights.toml`：权限配置
- `data/ts3audiobot.toml`：主配置

**升级时不要删除 `data/`。** 首次从旧版本启动时，若根目录还有旧的 `bots/`、
`ts3audiobot.db` 等，启动脚本会自动迁入 `data/`。

## 无损升级（推荐）

不要把新包直接覆盖到旧目录。请用升级脚本，它只替换程序，永不覆盖 `data/`：

```bash
# 在旧安装目录中执行，参数为新版本包或解压目录
chmod +x run/update-linux.sh
./run/update-linux.sh /path/to/TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
# 或
./run/update-linux.sh /path/to/TS3AudioBot-KuwoPlugin-linux-x64-new
```

脚本会：

1. 把当前 `data/`（及旧版根目录配置）备份到 `backup/pre-update-时间戳/`
2. 只更新主程序、`plugins/`、`WebInterface/` 等程序文件
3. 保留机器人、网页账号和管理权限

升级后仍用原来的启动方式：

```bash
./run/start-linux.sh
```

## 从源码构建

在 Linux 服务器或 Linux 构建机的仓库根目录执行：

```bash
chmod +x run/build-linux-package.sh
./run/build-linux-package.sh
```

脚本会构建网页、restore `linux-x64` 运行时、发布自包含机器人、复制酷我插件，
并在生成 `.tar.gz` 前校验网页入口、当前前端 bundle、插件 DLL 和启动脚本。
