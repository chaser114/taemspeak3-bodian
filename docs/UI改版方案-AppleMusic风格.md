# 改造方案：Apple Music 风格 UI + 移除语音识别

> 状态：**已完成**（2026-10 实施；最终实现与本稿略有差异，以源码为准）
> 视觉稿：`docs/ui-mockup/`（HTML 可直接双击打开，`shots/` 内是渲染图）
> 基线：`main` @ `f7264b3`，GitHub Actions run 70 构建通过

---

## 一、视觉稿一览

| 稿 | 文件 | 截图 | 说明 |
| --- | --- | --- | --- |
| 桌面 · 点歌主页 | `01-桌面-点歌.html` | `shots/01-desktop.png` | 正在播放卡片 + 搜索结果行 + 最近播放网格 |
| 桌面 · 待播队列 | `02-桌面-待播队列.html` | `shots/02-queue.png` | 右侧常驻 “Up Next” 面板 |
| 正在播放 · 歌词 | `03-正在播放-歌词.html` | `shots/03-nowplaying.png` | 全屏沉浸式，封面取色晕染背景 |
| 桌面 · 管理 | `04-桌面-管理.html` | `shots/04-admin.png` | 设置分组卡片，**已无语音控制** |
| 移动端 | `05-移动端.html` | `shots/05-mobile.png` | 点歌页 + 全屏播放页 |
| 深色 · 点歌 | `06-桌面-深色.html` | `shots/06-dark.png` | 深色主题 |
| 深色 · 队列 | `07-桌面-待播队列-深色.html` | `shots/07-queue-dark.png` | 深色 + 队列面板 |

打开方式：双击任意 HTML 即可；或从 `shots/` 直接看渲染图。

---

## 二、设计规范（Apple Music 风格，但适配我们的功能）

### 2.1 布局骨架

```
┌──────────┬────────────────────────────────┬──────────────┐
│ 侧栏 232  │ 顶栏 64（搜索 + 状态）           │  Up Next 300 │
│ 分区导航  ├────────────────────────────────┤  （可开关）   │
│ 账号底部  │ 内容区（滚动）                   │              │
├──────────┴────────────────────────────────┴──────────────┤
│ 播放条 90：封面/信息 · 控制+进度 · 队列+音量                  │
└───────────────────────────────────────────────────────────┘
```

- **侧栏分区**：`音乐`（点歌 / 最近播放 / 待播队列）、`管理`（机器人 / 网页账号 / 设置 / 程序更新）。
- **队列从抽屉改为右侧常驻列**，这是 Apple Music 最标志性的改法：桌面上不再遮挡内容，窄屏（<1200px）自动收起为抽屉，手机上仍从底部弹出。
- **顶部账号区**保留：状态胶囊 + 主题按钮 + 退出（符合原项目“退出固定顶部”的约束）。

### 2.2 设计令牌（替换现有 `App.vue` 变量）

| 令牌 | 浅色 | 深色 | 用途 |
| --- | --- | --- | --- |
| `--bg` | `#ffffff` | `#000000` | 主画布 |
| `--side` | `#f5f5f7` | `#1c1c1e` | 侧栏/面板 |
| `--ink` | `#1d1d1f` | `#f5f5f7` | 主文字 |
| `--ink-2` | `#6e6e73` | `#98989d` | 次文字 |
| `--line` | `rgba(0,0,0,.08)` | `rgba(255,255,255,.10)` | 分隔线 |
| `--fill` | `rgba(0,0,0,.06)` | `rgba(255,255,255,.10)` | 输入框/胶囊底 |
| `--brand` | `#fa233b` | 同 | 品牌红（Apple Music 红） |

- 保留现有青色变量名 `--console-brand` 做兼容映射，**不重命名现有 CSS 变量**，避免连锁改动。
- 字体栈沿用 `-apple-system, "PingFang SC", "Microsoft YaHei"`，标题用 `font-weight: 750/800` + `letter-spacing: -.02em` 得到苹果式紧凑标题。
- 圆角：卡片 12px、封面 7px、按钮 999px（胶囊）、输入 8px。
- 阴影：只保留 `0 1px 3px rgba(0,0,0,.14)` 级别的轻阴影；深色下改用分隔线，不用阴影。

### 2.3 关键交互变化

| 位置 | 现在 | 改后 |
| --- | --- | --- |
| 搜索结果 | 卡片列表，常驻「加入/播放」两个按钮 | **行式列表**（封面+标题+副标题+时长），操作按钮悬停才出现；当前曲目红色标题 + 跳动均衡器 |
| 最近播放 | 横向卡片条 | **方形封面网格**，悬停浮出圆形播放键（Apple Music 手法） |
| 歌词 | 底部播放条点开、暗色半屏 | **全屏沉浸页**：大封面 + 大字歌词，当前行放大加粗，封面主色晕染背景 |
| 待播队列 | 右侧滑出抽屉 | 桌面**常驻右栏**，窄屏/手机回退抽屉 |
| 播放控制 | 暂停键和上一首/下一首同级 | 播放键视觉层级最高（34px 圆形 + hover 底色），随机/循环降为次级灰 |
| 管理页 | 一长串混杂卡片 | **设置分组卡片**（站点 / 机器人 / 服务与日志 / 程序更新），左标签右控件 |

### 2.4 移动端

- 底部四 Tab：点歌 / 最近 / 队列 / 设置（原为三个）。
- 迷你播放条固定在 Tab 栏上方，点击进入全屏播放页。
- 全屏播放页用 `100dvh` + `env(safe-area-inset-bottom)` 处理刘海与浏览器底栏；下拉手柄 `⌄` 返回，不做容易被底栏遮挡的浮动关闭键。
- 所有触控目标 ≥ 44px。

### 2.5 复用边界（不动的部分）

- 后端 `/console-api/*` 契约、字段名、权限语义**全部不变**。
- Buefy / Vue 2 / Webpack 4 / TS 3.9 **不升级**。
- 不引入新的 UI 组件库（避免 Vue 3-only 组件）。

---

## 三、移除语音识别：改动清单

### 3.1 直接删除的文件（6 个源文件 + 4 个测试 + 2 个脚本）

| 文件 | 说明 |
| --- | --- |
| `TS3AudioBot/Audio/VoiceControlService.cs` | 727 行，Vosk/VAD 主服务 |
| `TS3AudioBot/Audio/VoiceCommandParser.cs` | 唤醒词与指令解析 |
| `TS3AudioBot/Audio/SpeechActivityDetector.cs` | WebRTC VAD |
| `TS3AudioBot/Audio/SpeechAudioPreprocessor.cs` | 音频预处理 |
| `TS3ABotUnitTests/VoiceCommandParserTests.cs` | 随功能删除 |
| `TS3ABotUnitTests/VoiceCommandTimingTests.cs` | 随功能删除 |
| `TS3ABotUnitTests/SpeechActivityDetectorTests.cs` | 随功能删除 |
| `TS3ABotUnitTests/SpeechAudioPreprocessorTests.cs` | 随功能删除 |
| `packaging/common/fetch-voice-model.sh` | 模型下载脚本 |
| `packaging/common/fetch-voice-model.ps1` | 同上 |

> ⚠️ `TS3AudioBot/Audio/IVoiceTarget.cs` **不删**：它是 TeamSpeak 的「语音播报目标」接口（`CustomTargetPipe` 实现），和语音识别无关。

### 3.2 需要修改的文件

| 文件 | 改什么 |
| --- | --- |
| `TS3AudioBot/Bot.cs` | 删 `builder.RequestModule<VoiceControlService>()`（110 行）、`Injector.GetModule<VoiceControlService>()?.Dispose()`（208 行）、`Injector.AddModule(config.Voice)`（90 行） |
| `TS3AudioBot/Config/ConfigStructs.cs` | 删整个 `ConfVoice` 类及其挂载属性 |
| `TS3AudioBot/TS3AudioBot.csproj` | 删 `Vosk`、`WebRtcVadSharp`、`WebRtcVadSharpLinux` 三个 PackageReference，以及对应的 `$(PkgVosk)` 拷贝 ItemGroup |
| `TS3AudioBot/Web/WebServer.cs` | 删 `bots/voice` 的 GET/POST 两个路由分支（375–389 行） |
| `TS3AudioBot/Web/WebConsoleService.cs` | 删 `GetVoiceSettings` / `SetVoiceSettings` 两个方法（135–159 行） |
| `WebInterface/src/ts/Pages/ConsoleAdmin.vue` | 删语音卡片模板、data 字段、`loadVoiceSettings` / `saveVoiceSettings` 方法、相关样式 |
| `WebInterface/src/ts/ConsoleApi.ts` | 若有语音类型声明则一并清理（当前无独立声明） |
| `.github/workflows/build-release.yml` | 删「Cache validated voice model」「Publish GitHub model mirror」两步；删 `VOICE_MODEL_ARCHIVE` 环境变量 |
| `.gitcode/workflows/build-release.yml` | 该文件整体已被 GitHub Actions 取代，建议**直接删除**（同时解决上次发现的“缺模型步骤 + 自动发布”隐患） |
| `packaging/linux/build-package.sh` | 删调用 `fetch-voice-model.sh` 的一行；`run/build-linux-package.sh` 同步 |
| `packaging/windows/build-package.ps1` | 删调用 `fetch-voice-model.ps1` 的一段 |
| `Dockerfile` | 无需改动（原本就没装模型），但需确认构建不因删除脚本而失败 |

### 3.3 删除后需要同步的文档

`README.md`、`docs/项目介绍.md`、`PROJECT-SUMMARY.md`（第 8 章整节「本地语音控制」）、`docs/UI优化交接总结.md`（「语音功能的边界」一节），以及 `docs/aegis/` 下 4 篇语音相关计划/记录。

### 3.4 数据兼容性

- 老用户的 `data/` 里会有 `[voice]` 配置段。**反序列化时未知字段会被忽略**，不会报错，但删除 `ConfVoice` 后该段会变成“多余配置”，下次保存时消失。这属于可接受的平滑降级。
- `voice-models/` 目录在旧安装目录里会残留（约 40 MB），不影响运行；升级脚本本来就不删程序目录外的文件，可在发布说明里提示手动删除。
- **不需要迁移数据库**，语音功能不写库。

### 3.5 风险点

| 风险 | 处理 |
| --- | --- |
| 删 `ConfVoice` 后 `ConfigStructs.cs` 里可能有别处引用 `config.Voice` | 全仓 grep 确认只有 `Bot.cs`、`WebConsoleService.cs` 两处 |
| 删 Vosk 包后 `TS3AudioBot.csproj` 里 `$(PkgVosk)` 引用失效 | 同一个 ItemGroup 一起删 |
| 本机无 .NET SDK，无法本地编译验证 | 只能靠 Actions 验证；**建议先推送让 run 71 跑绿再动 UI** |
| 语音功能是已上线的功能，删掉后无法回滚 | 保留 `git` 历史即可；如需保留可另开 `legacy/voice-control` 分支打标签 |

---

## 四、实施顺序

**阶段 0 · 先验证（半天）**
1. 只做「语音删除」的后端部分，推送一次，等 Actions 跑绿 → 确认 C# 8 与依赖删除无问题。
2. 本地跑 `npm run build` 验证前端仍可构建。

**阶段 1 · UI 骨架（1–2 天）**
3. `App.vue` 换设计令牌（浅色 + 深色两套）。
4. `ConsoleShell.vue` 重做：分区侧栏、顶栏搜索、状态胶囊、移动端四 Tab。
5. 产出可运行骨架，先不接功能，只保证路由与布局正确。

**阶段 2 · 播放器与队列（1–2 天）**
6. `ConsolePlayerBar.vue` 按新规范重排（播放键层级、进度条、音量）。
7. 队列改为右栏常驻 + 窄屏抽屉两种形态。
8. 全屏歌词页重做（背景取色可用 CSS 变量兜底，不引入新依赖）。

**阶段 3 · 页面细节（1–2 天）**
9. `ConsoleOverview.vue` 改行式列表 + 网格卡片。
10. `ConsoleAdmin.vue` 改分组卡片，**移除语音卡片**。
11. 空状态、加载态、错误态统一。

**阶段 4 · 验收（半天）**
12. `cd WebInterface && npm run build`，确认 `dist/index.html`、`dist/bundle.js`。
13. 桌面 1440/1280/1024 + 手机 390/375 全路径走查。
14. 管理员 vs 普通用户可见性、退出位置、连接状态三元色。
15. 推送后等 Actions 出包。

---

## 五、已确认的选择（2026-10）

1. **品牌色**：视觉稿用的是 Apple Music 红 `#fa233b`。保留它，还是回到项目原来的青绿 `#4fb8a8`（`App.vue` 现在是红，旧文档写的是青绿）？
2. **队列形态**：桌面用「右侧常驻面板」（更像 Apple Music），还是「点开才出现的抽屉」（更像现在的版本）？
3. **移动端 Tab**：现在 3 个（点歌/最近/管理）。要不要按稿件改成 4 个（点歌/最近/队列/设置）？
4. **删除范围**：语音是**彻底删除**（连模型下载、CI 步骤、文档一起清），还是只删前端入口、后端代码先留着？

另外一个提醒：本机**没有 .NET SDK**，C# 那部分我改完没法本地编译。建议按阶段 0 的做法，先只推语音删除，让 GitHub Actions 验证一次再继续，避免 UI 和后端问题混在同一个提交里排查。

---

## 六、确认后的交付物

- 修改后的源码（`WebInterface/src/ts/` 为主 + 后端语音删除）
- 重新构建的 `WebInterface/dist`
- 更新后的 `README.md` / `PROJECT-SUMMARY.md`
- 一份 `docs/UI改版说明.md` 记录设计令牌与组件对照

---

## 七、最终实施记录（2026-10）

### 已确认并落地的决定

1. **品牌色**：Apple Music 红 `#fa233b`。
2. **队列形态**：桌面右侧常驻面板；<1280px 自动改为浮层；手机为底部抽屉（72vh）。
3. **移动端 Tab**：四 Tab（点歌 / 最近 / 队列 / 管理）。
4. **语音**：彻底删除（源码、测试、配置、CI、打包脚本、文档）。

### 与初稿的主要差异

- **类名全部加 `bd-` 前缀**：Bulma（Buefy 自带）的 `.hero/.card/.field/.tag/.modal/.content` 会污染自定义样式，这是本次最大的坑，已写入 `PROJECT-SUMMARY.md` 3.6。
- **队列状态通过自定义事件同步**：`ConsoleShell` 与页面组件是兄弟关系，没有共享 store。用 `console-queue`（数据）、`console-queue-open`（打开）、`console-queue-sync`（握手）、`console-queue-play` / `console-queue-clear`（回流）五个事件通信。
- **进度条用 pointer 事件实现**：支持点击、拖动、键盘左右键（±5 秒），点击后乐观更新位置。
- **新增账号级机器人授权与访客模式**（初稿未包含，后续追加需求）。

### 新增的接口

见 `README.md` 的「网页 API」一节与 `PROJECT-SUMMARY.md` 第 8 章。

### 验证情况

- `npm run build` 通过，`WebInterface/dist` 已重新生成。
- 用真实构建产物 + mock API 在无头浏览器中实测：
  - 点击进度条 75% → 跳转 3:21 / 4:29，后端收到 `SEEK {"position":201.75,"botId":"bot-1"}`。
  - 队列面板显示 6 首、当前曲高亮。
  - 访客角色无「管理」入口、无退出按钮、侧栏显示「访客模式」。
  - 390×844 下 `scrollWidth = 390`，无横向溢出；全屏播放页 390×844，进度条 294px 完整可见。
- **C# 未能本地编译**：本机无 .NET SDK / 运行时，需要等 GitHub Actions 验证。
