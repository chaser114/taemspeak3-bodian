# TS3AudioBot 波点音乐插件

基于 [Splamy/TS3AudioBot](https://github.com/Splamy/TS3AudioBot) 的开源定制版：通过 `KuwoMusicPlugin.dll` 搜索/播放波点（酷我）音乐，并提供账号密码登录的网页控制台。

- 源码 / 自动构建： [GitHub](https://github.com/chaser114/taemspeak3-bodian)
- 源码镜像（可选）： [GitCode](https://gitcode.com/chaser114/taemspeak3-bodian)

## 功能概览

### TeamSpeak 内命令

| 命令 | 说明 |
|------|------|
| `!search 歌曲名` | 搜索歌曲 |
| `!play 歌曲名` / `!play 序号` | 播放搜索结果 |
| `!add 歌曲名` / `!add 序号` | 加入待播队列 |
| `!next` | 切到下一首 |

- 普通用户默认可搜索、点歌、切歌  
- **只有管理员**可清空整队  
- 播放时把机器人简介更新为歌名；空闲显示「休眠中」（需 TS 简介权限）

### 网页控制台（默认端口 `58913`）

独立账号体系，与 TeamSpeak UID 无关。

- 搜索点歌、封面、播放 / 加队、最近播放
- Apple Music 风格界面：左侧分区导航、桌面右侧常驻「待播队列」面板、深浅色主题
- 底部播放器：上一首 / 暂停 / 下一首、进度条**可点击或拖动跳转**、音量、顺序 / 列表 / 单曲循环、随机
- 点击左下角当前歌曲展开 **全屏播放页**（大封面 + 滚动歌词，点歌词可跳转）
- 管理员：多机器人管理、网页账号（创建 / 启停 / 改密 / **指定可控制的机器人**）、品牌名称
- 管理员：**访客模式**（免登录访问，只能操作指定机器人）、程序更新、服务日志、重启 / 停止服务
- 桌面 + 手机自适应布局（手机底部四 Tab：点歌 / 最近 / 队列 / 管理）

首次打开网页：

1. 创建管理员账号
2. 在「管理」里填写 TeamSpeak 地址、机器人名称（可选服务器密码）
3. 需要时再创建普通用户账号，并按需指定其可控制的机器人

### 权限模型

| 身份 | 能做什么 |
| --- | --- |
| 管理员 | 全部机器人、全部设置、程序更新、服务控制、账号与访客管理 |
| 普通用户 | 搜索、点歌、加队、切歌、调音量；管理员可限制其只能控制指定机器人 |
| 访客 | 免登录进入；只能搜索、点歌、加队、切歌，且仅限管理员预先指定的机器人 |

- 访客模式默认关闭，开启时必须至少选择一个机器人，并需要管理员密码确认。
- 访客不能清空队列、查看日志、修改任何设置，也不能看到管理入口。
- 普通用户若不指定机器人，默认可控制全部机器人（保持旧行为）。

## 下载

在 GitHub [Releases](https://github.com/chaser114/taemspeak3-bodian/releases) 下载：

- Windows：`TS3AudioBot-KuwoPlugin-windows-x64.zip`
- Linux：`TS3AudioBot-KuwoPlugin-linux-x64.tar.gz`

推送到 `main` 后由 **GitHub Actions** 自动构建并创建 GitHub Release。也可在对应 Actions 任务的 Artifacts 中下载测试包。
每次发布会同时生成 `VERSION.txt`，方便手动同步到国内下载源。

## 快速部署

### Windows

1. 解压发布包  
2. 双击 `start-web-console.bat`（或 `run\start-web-console.bat`）  
3. 浏览器打开 `http://127.0.0.1:58913`  

包内已含运行时、`ffmpeg.exe`、网页与插件。启动时会自动准备 `data\`。

### Linux（Ubuntu / Debian 等）

```bash
tar -xzf TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
cd TS3AudioBot-KuwoPlugin-linux-x64
chmod +x run/start-linux.sh
./run/start-linux.sh
```

也可运行包内 `start.sh` / `install-linux.sh`。  
启动脚本会安装并校验：

- `ffmpeg`
- `libopus0` + **`libopus-dev`**（系统没有名为 `libopus` 的包；缺 `libopus-dev` 时会报找不到 libopus）

然后打开 `http://服务器IP:58913` 完成网页初始化。

### Docker

```bash
chmod +x run/start-docker.sh
./run/start-docker.sh
# 等价于 packaging/docker 下 docker compose up -d --build
```

- 网页：`http://服务器IP:58913`  
- 数据在挂载的 `data/`，重建容器不要删卷  
- 停止：`docker compose down`  

需要本机已安装 Docker Engine 与 Compose V2。

## 数据与升级

程序与用户数据分离：

| 路径 | 内容 | 升级时 |
|------|------|--------|
| `data/` | 机器人、网页账号、权限、主配置 | **必须保留** |
| 主程序、`plugins/`、`WebInterface/` | 程序与界面 | 可被新版本替换 |

### 脚本升级（推荐）

**不要**把新包直接解压覆盖整个旧目录。

**Linux**

```bash
./run/update-linux.sh /path/to/TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
./run/start-linux.sh
```

**Windows**

```bat
update-windows.bat ..\TS3AudioBot-KuwoPlugin-windows-x64-new
start-web-console.bat
```

脚本会备份 `data/`、只替换程序。旧版若数据仍在安装根目录，下次启动会迁入 `data/`。

### 网页一键更新（管理员）

1. 登录管理员 → 侧栏「程序更新」，或**管理**页的「当前版本」一行
2. 选择更新源并输入管理员密码确认
3. 只替换程序文件，**不覆盖 `data/`**，完成后自动重启

更新源：

| 选项 | 说明 |
|------|------|
| **GitHub 镜像源** | GitCode 仓库的 Releases 安装包 |
| **GitHub 官方源** | 直连 GitHub Releases |

构建与发版由 **GitHub Actions → GitHub Releases** 完成；发布后手动把同一构建的安装包上传到 GitCode Release。GitHub Actions 不再自动请求 WebDAV 或 EdgeOne。

#### 手动同步 GitHub 镜像源

从同一次 GitHub Release 或 Actions Artifacts 下载以下三个文件，并上传到 GitCode 仓库 `chaser114/taemspeak3-bodian` 的同名 Release：

```text
TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
TS3AudioBot-KuwoPlugin-windows-x64.zip
VERSION.txt
```

上传到 GitCode Release 时，请按这个顺序操作：

1. 先上传 Linux 和 Windows 压缩包并确认上传完成；
2. 最后上传 `VERSION.txt`，避免机器人先发现新版本却下载不到完整压缩包；
3. 保持文件名不变，并让 `VERSION.txt` 与两个压缩包来自同一次构建。

运维（管理页 → 服务与日志）：

- 查看后台日志  
- 重启 / 停止服务（密码确认弹窗）  
- 备选脚本：Windows `stop.bat`，Linux `./run/stop-linux.sh`  
- 日志文件：`logs/console.log`、`data/logs/`  

Docker 请继续用 `docker compose up -d --build`；容器内网页升级需要可写安装目录，一般不适用。

包内 `VERSION` 记录当前 `build-N`（或等价版本号）。

## 本地构建

**Windows 一键打包**

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\windows\build-package.ps1
```

**Linux 一键打包**

```bash
chmod +x run/build-linux-package.sh
./run/build-linux-package.sh
```

生成物约在 `dist/TS3AudioBot-KuwoPlugin-linux-x64.tar.gz`。构建使用 `--runtime linux-x64` 与 `-p:SkipGitVersion=true`。

**手动编译（示例）**

```bash
dotnet restore TS3AudioBot/TS3AudioBot.csproj --runtime linux-x64 -p:SkipGitVersion=true
dotnet restore KuwoMusicPlugin/KuwoMusicPlugin.csproj --runtime linux-x64 -p:SkipGitVersion=true
dotnet build -c Release TS3AudioBot/TS3AudioBot.csproj -p:SkipGitVersion=true --no-restore
dotnet build -c Release KuwoMusicPlugin/KuwoMusicPlugin.csproj -p:SkipGitVersion=true --no-restore
cd WebInterface
NODE_OPTIONS=--openssl-legacy-provider npm ci
NODE_OPTIONS=--openssl-legacy-provider npm run build
```

入口脚本在仓库 `run/`；细节实现见 `packaging/docker`、`packaging/linux`、`packaging/windows`。

## 更换音乐 API

搜索、播放、歌词共用一个接口，定义在 [KuwoMusicPlugin/KuwoMusicPlugin.cs](KuwoMusicPlugin/KuwoMusicPlugin.cs)：

```csharp
private const string ApiUrl = "https://api.xcvts.cn/api/music/bdyy";
```

```text
# 搜索列表（不要带 n）
{ApiUrl}?msg=关键词&sc=10&type=json

# 选中第 n 首：同时返回 play_url 与 lrc
{ApiUrl}?msg=关键词&n=序号&type=json
```

可选音质参数 `bf`（如 `320kmp3`、`2000kflac`）。  
接口变更时只改 `ApiUrl` 及同文件底部 JSON 模型映射。网页歌词接口：`GET /console-api/music/lyrics`。不要改 `MainCommands.cs` 中对插件的调用方式。

## 网页 API

所有控制台接口前缀为 `/console-api`，使用同源 Cookie 会话（`ts3ab_session` 为登录账号，`ts3ab_guest` 为访客）。

| 接口 | 说明 |
| --- | --- |
| `GET status` | 初始化状态、站点名称、是否开放访客 |
| `POST login` / `POST guest/login` / `POST logout` | 账号登录 / 访客进入 / 退出 |
| `GET me` | 当前身份，含 `role`（admin / user / guest）与 `allowedBots` |
| `GET bots` | **只返回调用者有权控制的机器人** |
| `GET/POST accounts`、`accounts/enabled`、`accounts/password` | 账号管理 |
| `POST accounts/bots` | 指定某账号可控制的机器人（空数组表示全部） |
| `GET/POST guest/settings` | 访客模式开关与可控制机器人（保存需要管理员密码） |
| `music/*` | 播放控制，全部带 `botId`，服务端逐个校验授权 |
| `update/*`、`service/*`、`settings/brand` | 仅管理员 |

`music/*` 未传 `botId` 时，受限账号会回退到其被允许的第一个机器人，而不是全局默认机器人。

## GitHub Actions

推送到 `main` / `master` 或手动运行 **Build release packages** 时会：

- 构建网页、机器人、酷我插件  
- 生成 Windows / Linux 发布包  
- 上传 Artifact  
- 生成 `VERSION.txt`
- 创建 GitHub Release（`build-N`）  
- 不自动上传到 WebDAV / EdgeOne；国内下载源由维护者手动同步

## 许可证与声明

- 基于上游 TS3AudioBot，保留 [OSL-3.0](LICENSE) 及相关版权文件  
- 请自行确认所用第三方音乐 API 的使用条款  
- 本项目便于个人 / 小团队自建 TeamSpeak 点歌，按「开源工具」方式维护，非商业音乐产品  
- 音乐与歌词接口当前使用 [小尘 API](https://api.xcvts.cn/)  
