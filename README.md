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
- 底部播放器：上一首 / 暂停 / 下一首、**音量**、**顺序 / 列表循环 / 单曲循环**、**随机**、待播队列  
- 点击当前歌曲展开 **滚动歌词**（无歌词显示「暂无歌词」）  
- 管理员：多机器人管理、网页账号（创建 / 启停 / **改密**）、品牌名称  
- 管理员：程序更新、服务日志、重启 / 停止服务（密码弹窗确认）  
- 桌面 + 手机自适应布局  

首次打开网页：

1. 创建管理员账号  
2. 在「管理」里填写 TeamSpeak 地址、机器人名称（可选服务器密码）  
3. 需要时再创建普通用户账号  

## 下载

在 GitHub [Releases](https://github.com/chaser114/taemspeak3-bodian/releases) 下载：

- Windows：`TS3AudioBot-KuwoPlugin-windows-x64.zip`
- Linux：`TS3AudioBot-KuwoPlugin-linux-x64.tar.gz`

推送到 `main` 后由 **GitHub Actions** 自动构建并发布。也可在对应 Actions 任务的 Artifacts 中下载测试包。

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

1. 登录管理员 → 侧栏版本号 / **管理**页「程序更新」  
2. 选择更新源并输入管理员密码确认  
3. 只替换程序文件，**不覆盖 `data/`**，完成后自动重启  

更新源：

| 选项 | 说明 |
|------|------|
| **国内加速（推荐）** | 版本信息来自 GitHub，安装包经国内代理（如 ghproxy）下载 |
| **GitHub 官方源** | 直连 GitHub Releases |

构建与发版只依赖 **GitHub Actions → GitHub Releases**。  
**不需要**再把安装包手动传到 Gitee / GitCode。GitCode 如有仓库，仅作源码镜像即可。

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

## GitHub Actions

推送到 `main` / `master` 或手动运行 **Build release packages** 时会：

- 构建网页、机器人、酷我插件  
- 生成 Windows / Linux 发布包  
- 上传 Artifact  
- 创建 GitHub Release（`build-N`）  

## 许可证与声明

- 基于上游 TS3AudioBot，保留 [OSL-3.0](LICENSE) 及相关版权文件  
- 请自行确认所用第三方音乐 API 的使用条款  
- 本项目便于个人 / 小团队自建 TeamSpeak 点歌，按「开源工具」方式维护，非商业音乐产品  
- 音乐与歌词接口当前使用 [小尘 API](https://api.xcvts.cn/)  
