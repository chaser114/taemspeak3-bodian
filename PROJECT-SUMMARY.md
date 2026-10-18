# TS3AudioBot 波点项目交接与维护总结

> 这份文档是给后续维护者和 AI 使用的项目事实记录。开始改代码前先读本文件，再读对应源码。文档只记录项目结构、行为约束和运维流程，不包含任何账号、密码、服务器地址、Token、Secret 或其他凭证。

## 1. 当前状态

| 项目 | 当前值 |
| --- | --- |
| 本地仓库 | `C:\Users\Administrator\Documents\ts3bot 波点\TS3AudioBot` |
| 主分支 | `main` |
| GitHub | <https://github.com/chaser114/taemspeak3-bodian> |
| GitCode 源码镜像 | <https://gitcode.com/chaser114/taemspeak3-bodian> |
| 最新已知提交 | 见 `git log -1 --oneline`；本地语音识别已移除、控制台已改为 Apple Music 风格 |
| 最新已知 Actions | run `70`，构建、打包、Docker 和 GitHub Release 均成功 |
| 当前工作区 | 修复提交后干净；本次文档创建后会产生唯一的未提交文件 |

### 最近一次构建失败的根因

GitHub Actions run `69` 的失败不是服务器上传问题，而是 C# 编译错误：

1. `TS3AudioBot/Audio/VoiceControlService.cs` 使用了不存在的小写变量 `sampleRate`，正确名称是 `SampleRate`。
2. 同文件使用了 C# 9 的 `is not null`，但项目锁定 C# 8.0，已改为 `WakeRecognizer != null`。

这两个问题已在 `f7264b3` 修复，并由 run `70` 验证通过。

## 2. 项目定位

这是基于 [Splamy/TS3AudioBot](https://github.com/Splamy/TS3AudioBot) 的 TeamSpeak 3 音乐机器人定制版，主要定制内容包括：

- `KuwoMusicPlugin.dll`：通过波点音乐 API 搜索、播放和获取歌词；
- 账号密码登录的网页控制台；
- 多机器人管理、网页账号管理、播放器、歌词、待播队列和程序更新；
- 账号级机器人权限（可指定每个网页账号能控制哪些机器人）；
- GitHub Actions 自动构建和 GitHub Release 发布。

这里的“波点音乐”是项目历史名称和音乐 API 方案名称，不代表要在网页每个位置显示“酷我”或其他渠道说明。用户界面中的资源类型应统一显示为“歌曲”；内部资源类型仍然可以是 `kuwo`，不要为了隐藏文字而删除协议字段或服务端解析逻辑。

## 3. 不可误改的硬约束

这些约束不是建议，而是当前项目能够稳定构建和升级的前提。

### 3.1 C# 版本

- 后端目标框架是 `.NET 6`。
- `TS3AudioBot/TS3AudioBot.csproj` 明确设置 `<LangVersion>8.0</LangVersion>`。
- 所有 C# 修改必须保持 C# 8 语法。
- 不要使用 `is not null`、关系模式、扩展属性模式等 C# 9 及以上语法。
- nullable reference types 已启用，新增代码要按现有的 `?`、空值检查和项目风格处理。
- 本机当前没有可用的 `dotnet` 时，不要声称本地 C# 已验证；应依赖 GitHub Actions 或具备 .NET 6 的环境验证。

### 3.2 前端版本

- 前端是 Vue 2、TypeScript 3.9、Buefy 0.8、Less、Webpack 4。
- CI 固定 Node 18。
- Webpack 4 在现代 Node/OpenSSL 下需要 `NODE_OPTIONS=--openssl-legacy-provider`。
- 不要擅自升级 Vue、Webpack、TypeScript 或 Buefy；旧依赖之间存在兼容性约束。
- 修改源码后要构建 `WebInterface/dist`，运行时读取的是构建产物，不是 TypeScript 源码。

### 3.3 数据不能覆盖

- `data/` 保存机器人配置、网页账号、权限以及主配置相关数据，升级时必须保留。
- 不要把新压缩包直接解压覆盖整个旧安装目录。
- 推荐使用 `run/update-linux.sh`、`run/update-windows.bat` 或网页管理员更新功能。
- 网页更新会先备份数据，再只替换程序文件；不要改成删除整个安装目录的升级方式。
- 旧版本可能还把数据放在安装根目录的 `bots`、`ts3audiobot.toml`、`ts3audiobot.db`、`rights.toml`，升级脚本需要兼容迁移，不能无条件删除。

### 3.4 下载源和发布方式

- GitHub Actions **不再**自动上传 WebDAV，也不再请求 EdgeOne。
- 国内下载包由维护者从同一次 GitHub 构建手动同步到 GitCode Release。
- `bodian` 是为了兼容已有网页客户端而保留的内部 source id，不是 WebDAV 地址，也不是 EdgeOne 地址。
- 当前 `bodian` 实际调用 GitCode Release API：
  `https://gitcode.com/api/v5/repos/chaser114/taemspeak3-bodian/releases/latest`
- 当前 `github` 实际调用 GitHub Release API：
  `https://api.github.com/repos/chaser114/taemspeak3-bodian/releases/latest`
- 后端现有的 `bodian` 显示文案仍可能写作“GitHub 镜像源”，这是历史兼容文案；判断真实来源必须看 `WebUpdateService.cs` 的 API 地址，不要根据文案推断它使用了 GitHub、WebDAV 或 EdgeOne。

### 3.5 不要重新暴露旧设置界面

- 当前项目有旧版 Buefy 页面和新版控制台并存的历史背景。
- 新 UI 修改优先限制在 `WebInterface/src/ts/` 的控制台组件，不要把原生旧设置页面重新嵌入新管理员页。
- 不要为了“说明功能”增加没有实际作用的小字、来源标签或解释性描述。
- 机器人名称显示必须来自实际配置的机器人名称，不要写死“波点音乐”。
- 桌面端退出登录固定在顶部账号区域；移动端要考虑 `100dvh`、`env(safe-area-inset-bottom)` 和浏览器底栏。
- 播放器、歌词、待播队列必须保持可点击和可操作，不能只做静态视觉修改。

### 3.6 Bulma 类名冲突（改 UI 前必读）

`Main.ts` 仍然 `import "buefy/dist/buefy.css"`，Bulma 有大量全局类名：
`hero / card / field / label / control / tag / modal / content / box / title / subtitle / notification / menu / nav` 等。

**所有自定义控制台组件的类名必须带 `bd-` 前缀**，例如 `bd-card`、`bd-field`、`bd-tag`、`bd-modal`、`bd-content`。
否则会出现难以定位的错乱：卡片被强加边框阴影、标题被放大、`content` 变成富文本样式导致手机端横向溢出。

改动 UI 后至少要检查：`?theme=dark` 以及手机宽度（390px）下没有横向滚动、卡片标题字号正常。

### 3.7 进度条必须真的能跳转

播放进度条不是装饰。实现要点：

- 用 `pointerdown/move/up` 计算比例，或使用铺满轨道的透明 `input[type=range]`；**不要给父容器加 `pointer-events: none`**。
- 旧实现把折叠态 `.timeline` 整体设为 `pointer-events: none`，导致进度条永远点不动。
- 点击后要先乐观更新本地位置，再发 `music/seek`，否则等待轮询会出现视觉回跳。

## 4. 目录结构和职责

```text
TS3AudioBot/
├─ TS3AudioBot/                 主程序、TeamSpeak 连接、网页 API、配置
├─ KuwoMusicPlugin/             搜索/解析/播放/歌词的波点音乐插件
├─ TSLib/                       TeamSpeak 协议、音频包和 Opus 相关底层库
├─ TS3ABotUnitTests/             现有单元测试
├─ WebInterface/                Vue 2 网页控制台源码和构建配置
├─ packaging/                   Windows/Linux/Docker 打包与模型准备脚本
├─ run/                         启动、停止、更新和 Linux 打包入口脚本
├─ .github/workflows/            GitHub Actions 构建发布工作流
├─ docs/                         面向使用者的项目说明
├─ data/                         运行时用户数据（发布包内会保留目录说明）
├─ Dockerfile / docker-compose.yml
├─ README.md                     用户安装、升级和构建说明
└─ PROJECT-SUMMARY.md            本交接文档
```

生成目录 `bin/`、`obj/`、`WebInterface/node_modules/`、`WebInterface/dist/` 和临时打包目录不属于主要源码入口。排查功能时先看源码，不要直接改生成文件；前端构建后 `dist/` 会被重新生成。

## 5. 关键源码文件

### 后端和网页 API

| 文件 | 责任 |
| --- | --- |
| `TS3AudioBot/Web/WebServer.cs` | `/console-api` 路由、登录鉴权、管理员权限和请求分发 |
| `TS3AudioBot/Web/WebConsoleService.cs` | 网页控制台业务、音乐状态、搜索、歌词和机器人管理 |
| `TS3AudioBot/Web/WebUpdateService.cs` | 版本检查、Release 资产选择、下载、备份、程序替换和重启 |
| `TS3AudioBot/Config/ConfigStructs.cs` | 主配置结构（`ConfVoice` 已删除） |
| `KuwoMusicPlugin/KuwoMusicPlugin.cs` | 波点音乐 API 的搜索、资源解析和播放 URL |
| `TS3AudioBot/Web/WebAccountService.cs` | 账号、会话、`WebPrincipal` 鉴权与访客设置 |
| `TS3AudioBot/Audio/IVoiceTarget.cs` | TS 语音播报目标接口（**与语音识别无关，不要删**） |
| `WebInterface/src/ts/Pages/Home.vue` | 登录 / 初始化 / 访客入口 |
| `TS3AudioBot/TS3AudioBot.csproj` | .NET 6、C# 8（Vosk / WebRTC VAD 依赖已移除） |

### 前端

| 文件 | 责任 |
| --- | --- |
| `WebInterface/src/ts/Main.ts` | 前端入口与路由表（`/`、`/music`、`/recent`、`/admin`） |
| `WebInterface/src/ts/ConsoleApi.ts` | 控制台 API 请求封装 |
| `WebInterface/src/ts/Components/ConsoleShell.vue` | 布局壳层、分区侧栏、顶栏、右侧常驻队列面板、移动端四 Tab |
| `WebInterface/src/ts/Components/ConsolePlayerBar.vue` | 播放条 + 全屏播放页，**可点击/拖动的进度条**与歌词跳转 |
| `WebInterface/src/ts/App.vue` | Apple Music 设计令牌（黑/白/红）与深浅色变量 |
| `WebInterface/src/ts/Pages/ConsoleOverview.vue` | 搜索、点歌、当前状态、最近播放 |
| `WebInterface/src/ts/Pages/ConsoleAdmin.vue` | 机器人、账号与授权、访客、更新和服务管理 |
| `WebInterface/package.json` | Vue 2 / Webpack 4 / TypeScript 3.9 依赖和脚本 |

## 6. 网页 API 契约

所有控制台接口前缀是 `/console-api`，大部分接口要求登录；管理员接口还会检查账号角色。修改前后端字段时必须同时检查 `ConsoleApi.ts`、页面调用和 `WebServer.cs`。

### 音乐

- `GET /console-api/music/state`
- `POST /console-api/music/search`
- `POST /console-api/music/play`
- `POST /console-api/music/add`
- `POST /console-api/music/pause`
- `POST /console-api/music/next`
- `POST /console-api/music/previous`
- `POST /console-api/music/seek`
- `GET /console-api/music/lyrics`
- 另外还有 `clear`、`volume`、`loop`、`random` 等播放器控制接口。

`music/play` 和 `music/add` 接收服务端认可的资源对象。内部资源类型仍然使用 `kuwo`，只在 UI 展示层将类型显示为“歌曲”。不要把 `resource.type` 删除后再让服务端猜资源，也不要把歌词接口改成另一条不一致的搜索链路。

### 管理和更新

- `GET/POST /console-api/guest/settings`：访客模式开关与允许的机器人，仅管理员可用。
- `GET /console-api/update/status`：读取当前版本和可用更新源。
- `POST /console-api/update/check`：检查更新。
- `POST /console-api/update/apply`：验证管理员密码后下载、备份、替换程序并重启。
- `GET /console-api/service/status`、`GET /console-api/service/logs`、`POST /console-api/service/stop`、`POST /console-api/service/restart`：服务管理，仅管理员可用。

更新替换的是程序文件，不应覆盖 `data/`。Release 资产选择优先匹配：

```text
TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
TS3AudioBot-KuwoPlugin-windows-x64.zip
```

不要把 GitHub 自动生成的源码压缩包误当成安装包。

## 7. 音乐 API 约束

搜索、播放资源解析和歌词当前共用波点接口：

```text
https://api.xcvts.cn/api/music/bdyy
```

当前调用形态：

```text
搜索：{ApiUrl}?msg=关键词&sc=10&type=json
选中第 n 首并取得播放地址/歌词：{ApiUrl}?msg=关键词&n=序号&type=json
```

后端入口是 `KuwoMusicPlugin/KuwoMusicPlugin.cs`，网页歌词补充接口在 `WebConsoleService.cs`。如果更换 API，必须同时确认搜索结果、播放 URL、歌词、语音点歌和网页点歌仍然走一致的结果编号；不要只改网页搜索而留下旧的服务端解析路径。


## 8. 权限模型与访客模式

### 三种身份

| 身份 | 判定方式 | 能力 |
| --- | --- | --- |
| 管理员 | `WebAccountRole.Admin` | 全部机器人、全部设置、更新与服务控制、账号与访客管理 |
| 普通用户 | `WebAccountRole.User` | 搜索、点歌、加队、切歌、音量；受 `AllowedBots` 限制 |
| 访客 | 无账号，持 `ts3ab_guest` Cookie | 只有播放类操作，且仅限管理员指定的机器人 |

### 关键实现

- `WebAccountService.cs` 中的 `WebPrincipal` 是唯一的鉴权主体，`IsAdmin`、`IsGuest`、`CanControlBot`、`ResolveBotId` 都在这里判定。
- `GET bots` 会**过滤**掉调用者无权的机器人，前端因此不会显示不该出现的机器人。
- 所有 `music/*` 路由在调用业务方法前都经过 `EnsureBotAccess`，越权直接返回 403；不是只靠前端隐藏按钮。
- 受限账号调用 `music/*` 且未传 `botId` 时，`ResolveBotId` 回退到其允许的第一个机器人，避免落到全局默认机器人上。
- 账号的 `AllowedBots` 为空数组表示“不限制”（管理员永远不受限）；访客的允许列表为空时访客模式不可开启。
- 访客会话以 `guest:` 前缀存放在同一个 session 集合里，每次请求都从当前访客设置重新构造权限，因此关闭访客或改动机器人后立即生效。
- `POST guest/settings` 需要管理员密码二次确认，与重启/停服其它敏感操作一致。

### 新增/变更的接口

- `GET status` 增加 `guestEnabled`。
- `GET me` 增加 `allowedBots`，`role` 可能是 `guest`。
- `POST guest/login`（免登录进入）、`GET/POST guest/settings`（管理员）。
- `POST accounts/bots`（管理员，设置账号可控制机器人）。
- `GET accounts` 返回值增加 `allowedBots`。

## 9. 构建、发布和手动同步

### GitHub Actions

工作流是 `.github/workflows/build-release.yml`，推送 `main`/`master` 或手动运行 `Build release packages` 时执行：

1. 安装 .NET 6 和 Node 18；
2. 还原并编译主程序和插件；
3. 用 `npm ci` 和 `npm run build` 构建网页；
4. 校验 Docker Compose 并构建 Docker 镜像；
5. 生成 Linux `tar.gz` 和 Windows `zip`；
6. 上传 Actions Artifact；
7. 生成 `VERSION.txt` 和 GitHub Release，版本格式通常是 `build-N`；
8. （语音模型步骤已移除，构建不再下载任何语音模型）

当前工作流只负责 GitHub 构建和 GitHub Release，不负责国内下载站同步。不要重新添加 WebDAV Secret、EdgeOne 上传步骤，除非用户明确重新选择并验证了新的发布方案。

### GitCode 手动同步

从同一次 GitHub Release 或 Actions Artifact 取得以下三个文件：

```text
TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
TS3AudioBot-KuwoPlugin-windows-x64.zip
VERSION.txt
```

上传到 GitCode 同名 Release 时：

1. 先上传 Linux 和 Windows 压缩包；
2. 确认两个压缩包上传完成后，再上传 `VERSION.txt`；
3. 保持文件名不变；
4. 三个文件必须来自同一次 Actions 构建。

这样可以避免机器人先读到新版本号，却下载不到完整安装包。不要把 GitCode 源码镜像误当成 GitHub Actions 构建环境；当前构建仍在 GitHub Actions 上执行。

## 10. 本地验证命令

### 前端

在仓库根目录执行：

```powershell
Set-Location .\WebInterface
$env:NODE_OPTIONS = "--openssl-legacy-provider"
npm ci
npm run build
```

至少确认 `WebInterface/dist/index.html` 和 `WebInterface/dist/bundle.js` 存在且非空。

### 后端

在有 .NET 6 SDK 的环境执行：

```powershell
dotnet restore TS3AudioBot/TS3AudioBot.csproj -p:SkipGitVersion=true
dotnet build TS3AudioBot/TS3AudioBot.csproj --configuration Release -p:SkipGitVersion=true --no-restore
```

完整 CI 还会编译 `KuwoMusicPlugin/KuwoMusicPlugin.csproj`、构建 Docker 和两个平台的发布包。修改 C# 后必须至少等 Actions 通过，不能仅凭编辑器没有红线就提交。

### 提交前

```powershell
git diff --check
git status --short
```

不要把 `data/`、账号文件、日志、模型临时包、`node_modules/` 或 `bin/obj` 误提交。提交前检查 `git diff` 中没有凭证和本机路径泄露。

## 11. 常见误区和处理原则

| 误区 | 正确处理 |
| --- | --- |
| 把 `bodian` 当成 WebDAV 或 EdgeOne | 它是兼容旧客户端的 source id，实际解析 GitCode Release API |
| 把 GitCode 当成构建服务器 | 构建在 GitHub Actions；GitCode 只做源码镜像和手动同步的 Release |
| 把 `kuwo` 从资源 JSON 删除 | 只在 UI 隐藏内部类型，服务端需要它解析播放资源 |
| 只在 `GET bots` 里过滤机器人 | 每个 `music/*` 路由都必须过 `EnsureBotAccess`，否则可直接构造请求越权 |
| 把访客当成普通用户 | 访客永远不是管理员，`IsAdmin` 对访客强制为 false |
| 用源码目录测试已经修改的前端 | 运行时使用 `WebInterface/dist`，改完必须重新构建 |
| 直接覆盖整个安装目录升级 | 保留 `data/`，使用更新脚本或网页更新服务 |
| 用 C# 9 语法修编译错误 | 项目是 C# 8；先按 C# 8 改写 |
| 看到 InvalidPacket 就让 worker 退出 | 当前设计应忽略无效包并继续；需看是否有 worker stopped 日志 |
| 给受限账号传空 botId 就当成全局默认 | `ResolveBotId` 应回退到该账号允许的第一个机器人 |
| 为了视觉效果重新露出原生设置页 | 保持新控制台边界，先复用现有 API 和组件 |

## 12. 后续 AI 的修改流程

1. 先运行 `git status --short --branch` 和 `git log -1 --oneline`，确认没有覆盖用户未提交的工作。
2. 先阅读本文件、`README.md` 和目标模块源码，确认当前真实实现，不按聊天记录里的旧方案猜测。
3. 修改前明确影响范围：前端、API、数据格式、权限判定、构建或发布中的哪一层。
4. 不要把与当前任务无关的 UI 文案、源地址、账号逻辑或发布流程一起重写。
5. 修改后运行针对性的构建或测试，并执行 `git diff --check`。
6. 报告时明确写出：改了哪些文件、验证了什么、哪些验证因环境缺失没有做、是否提交/推送。
7. 除非用户明确要求，不要自动登录服务器、使用凭证、更新生产机器人、创建 Secret、上传 Release 或推送提交。

## 13. 安全边界

- 本文及仓库中不得保存服务器登录凭证、GitCode 密钥、GitHub Token、Actions Secret 或网页账号密码。
- 任何需要账号或 Secret 的操作都应由维护者在自己的环境中完成；文档只描述变量名和流程，不写真实值。
- 不要把日志中的服务器地址、用户 UID、Cookie 或密码回显到提交、Issue 或交接文档。
- 音频识别是本地处理，但 TeamSpeak 音频仍会在机器人所在服务器内被解码和分析；开启前应评估 CPU 和使用者隐私预期。

## 14. 版权和外部依赖

- 项目继承上游 TS3AudioBot 的 OSL-3.0 及相关版权文件。
- 音乐和歌词接口使用第三方波点 API，使用者需要自行确认其服务条款和内容合规性。
- FFmpeg、Opus、Vue/Buefy 等依赖的版本和许可证不要在没有核对的情况下替换。

