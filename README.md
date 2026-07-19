# TS3AudioBot 波点音乐插件

基于 [Splamy/TS3AudioBot](https://github.com/Splamy/TS3AudioBot) 的自用定制版。机器人通过独立的 `KuwoMusicPlugin.dll` 调用酷我音乐 API 搜索和播放歌曲，并提供账号密码登录的网页控制台。

## 功能

- `!search 歌曲名`：搜索歌曲
- `!play 歌曲名`：搜索并直接播放第一首
- `!play 序号`：播放最近一次搜索结果中的对应歌曲
- `!add 歌曲名`：搜索并加入待播队列
- `!add 序号`：将搜索结果加入待播队列
- `!next`：立即切换到队列下一首
- 普通 TeamSpeak 用户默认可以搜索、点歌、立即播放、加入队列和切歌
- 只有管理员可以清空整个待播队列
- 播放时将机器人 TeamSpeak 简介更新为歌曲名，空闲时显示“休眠中”
- 插件独立于主程序，文件位于 `plugins/KuwoMusicPlugin.dll`

## 网页控制台

网页端默认监听 `58913` 端口。它使用独立的账号密码体系，不依赖 TeamSpeak UID。

首次访问网页时：

1. 创建第一个管理员账号。
2. 在“管理”页面设置站点名称。
3. 在“新建机器人”区域填写 TeamSpeak 地址、机器人名称和服务器密码。
4. 之后可以在同一页面创建、编辑、删除多个机器人，也可以创建普通子账号和管理员账号。

网页端支持：

- 点歌搜索、封面展示、立即播放和加入队列
- 最近播放独立页面
- 底部播放器、上一首、暂停/继续、下一首和待播队列
- 管理员清空整个待播队列
- 桌面端和手机端响应式布局
- 编辑机器人时弹窗修改连接信息并重新连接

启动后访问 `http://服务器IP:58913`。网页配置和机器人数据保存在运行目录的 **`data/`** 中，请勿删除。

## 数据与无损升级

程序文件和用户数据已分开：

| 目录/文件 | 内容 | 升级时 |
|-----------|------|--------|
| `data/` | 机器人、网页账号、权限、主配置 | **必须保留** |
| 主程序、`plugins/`、`WebInterface/` | 程序与界面 | 可被新版本替换 |

### 推荐升级方式（给最终用户）

**不要**把新包直接解压覆盖旧目录。请用包内升级脚本：

**Linux**

```bash
# 在旧安装目录执行
./run/update-linux.sh /path/to/TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
./run/start-linux.sh
```

**Windows**

```bat
update-windows.bat ..\TS3AudioBot-KuwoPlugin-windows-x64-new
start-web-console.bat
```

脚本会自动：备份 `data/` → 只替换程序 → 保留机器人与账号。  
旧版本若数据还在安装根目录（`bots/`、`ts3audiobot.db` 等），下次启动会自动迁入 `data/`。

**Docker** 继续使用挂载的 `data/` 卷，`docker compose up -d --build` 重建镜像即可，不要删 `data/`。

### 网页一键更新（管理员）

管理员登录后：

- **电脑端**：左侧栏底部显示版本号；有更新时会 **变黄**，点开即可升级  
- **手机端**：进入 **管理** 页，顶部「程序更新」卡片同样可检查/升级  

升级弹层支持选择：

- **Gitee 更新（国内服务器）**（默认）  
- **GitHub 更新（官方源）**  

确认时需再输入一次管理员密码。更新只替换程序文件，**不会覆盖 `data/`**。  

说明：

- **构建只在 GitHub Actions**；Gitee 只作源码镜像 + 国内下载源（Releases）  
- 发布包需同步到 **Gitee Releases**（https://gitee.com/chaser114/taemspeak3-bodian）后，国内源才可用  
- **Docker 部署**请继续用 `docker compose up -d --build`，网页升级需要可写的安装目录，容器镜像层通常不适用  
- 包内 `VERSION` 文件记录当前 `build-N` 版本号

## 下载

在 GitHub 的 [Releases](../../releases) 页面下载带版本号的构建包：

- Windows：`TS3AudioBot-KuwoPlugin-windows-x64.zip`
- Linux：`TS3AudioBot-KuwoPlugin-linux-x64.tar.gz`

每次推送到 `main`、`master` 或手动运行 Actions 时，也可以在 Actions 对应任务的 Artifacts 中下载测试包。

## 部署方式

### Docker 一键部署（推荐服务器使用）

Docker 部署在服务器本地构建镜像，不需要从 GitHub 拉取成品镜像：

```bash
chmod +x run/start-docker.sh
./run/start-docker.sh
```

也可以直接运行：

```bash
chmod +x packaging/docker/install-docker.sh
./packaging/docker/install-docker.sh
```

脚本会执行 `docker compose up -d --build`，网页端地址为 `http://服务器IP:58913`。停止服务：

```bash
docker compose down
```

要求服务器已安装 Docker Engine 和 Docker Compose V2。`data/` 目录通过 Compose 挂载到容器中，容器重建不会丢失账号和机器人配置。

### Linux 一键部署

适用于 Ubuntu 22.04 x64 的 Linux 发布包：

```bash
tar -xzf TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
cd TS3AudioBot-KuwoPlugin-linux-x64
chmod +x run/start-linux.sh
./run/start-linux.sh
```

也可以直接运行包内的 `start.sh`。首次启动后打开网页完成管理员账号和 TeamSpeak 连接配置。启动脚本会检查并安装 `ffmpeg`、`libopus0`（Debian/Ubuntu），并自动准备 `data/` 数据目录。

### Windows 一键部署

解压 Windows 发布包后，双击包根目录的 `start-web-console.bat` 即可启动机器人和网页端；也可以双击 `run/start-web-console.bat`。包内已包含 .NET runtime、`ffmpeg.exe`、网页控制台和酷我插件。启动时会自动准备 `data\` 数据目录。

网页地址：`http://127.0.0.1:58913`

### 本地构建

Windows 下可以在仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\windows\build-package.ps1
```

或者双击 `run/build-windows-package.ps1`。脚本会构建网页端、机器人、酷我插件并生成可直接运行的 Windows x64 包。

Linux 下可以在仓库根目录执行：

```bash
chmod +x run/build-linux-package.sh
./run/build-linux-package.sh
```

脚本会在 `dist/TS3AudioBot-KuwoPlugin-linux-x64.tar.gz` 生成带网页端的自包含 Linux x64 包，
并在打包前校验 `WebInterface/bundle.js`、`plugins/KuwoMusicPlugin.dll`、主程序和 Linux 启动脚本。
Linux 构建会显式使用 `--runtime linux-x64` 和 `-p:SkipGitVersion=true`，不会再安装不兼容 .NET 6 的
`dotnet-script` 或 `GitVersion.Tool`。

手动构建代码：

```bash
dotnet restore TS3AudioBot/TS3AudioBot.csproj --runtime linux-x64 -p:SkipGitVersion=true
dotnet restore KuwoMusicPlugin/KuwoMusicPlugin.csproj --runtime linux-x64 -p:SkipGitVersion=true
dotnet build -c Release TS3AudioBot/TS3AudioBot.csproj -p:SkipGitVersion=true --no-restore
dotnet build -c Release KuwoMusicPlugin/KuwoMusicPlugin.csproj -p:SkipGitVersion=true --no-restore
cd WebInterface
NODE_OPTIONS=--openssl-legacy-provider npm ci
NODE_OPTIONS=--openssl-legacy-provider npm run build
```

上面的手动命令是 Linux 构建参数；需要完整压缩包时请直接使用 `run/build-linux-package.sh`。

## 部署入口位置

仓库根目录的 `run/` 放置容易找到的入口脚本；底层实现仍由 `packaging/docker`、`packaging/linux` 和 `packaging/windows` 维护。

## 更换 API

当前 API 地址只在 [KuwoMusicPlugin.cs](KuwoMusicPlugin/KuwoMusicPlugin.cs) 中定义一次：

```csharp
private const string ApiUrl = "https://api.xingzhige.com/API/Kuwo_BD_new/";
```

接口失效时，先把这行替换成新 API 的基础地址。新接口必须支持关键词和结果序号请求；当前请求格式为：

```text
{ApiUrl}?name=歌曲名&n=序号
```

若新接口的 JSON 字段不同，在同一文件底部的 `KuwoResponse`、`KuwoSong` 和 `KuwoRaw` 中调整字段，并在 `ToResource` 内映射歌曲 ID、歌名、歌手、封面链接和可播放音频链接。不要修改 `MainCommands.cs`，`!search` 和 `!play` 已固定调用该插件。

## GitHub Actions

推送到 `main` 或 `master` 后，Actions 会自动：

- 构建网页控制台、机器人和酷我插件
- 生成 Linux x64 压缩包
- 生成 Windows x64 压缩包
- 上传 Actions Artifact
- 在主分支创建 GitHub Release

也可以在 GitHub Actions 页面手动运行 `Build release packages`。

## 开源说明

本仓库基于上游 TS3AudioBot，保留原有 [OSL-3.0](LICENSE) 许可证和版权文件。发布前请确认使用的第三方音乐 API 允许你的使用方式。

## 声明

- 本插件由 ChatGPT 协助开发，主要用于个人使用。
- 使用[星之阁 API](https://api.xingzhige.com/)。
