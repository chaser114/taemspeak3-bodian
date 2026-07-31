# CeruMusic 风格网页控制台实施计划

## Goal

在不改变网页账号、TS3 机器人配置、音乐控制 API 和权限规则的前提下，将网页控制台重构为 CeruMusic 风格的浅色音乐播放器界面，并覆盖桌面、手机、登录和管理员页面。

## Architecture

`ConsoleApi.ts` 与现有 `/console-api/*` 合约保持不变。新增的 Vue 组件只承担展示和事件上抛：控制台壳层负责导航与账号状态，底部播放器负责播放控制，队列抽屉负责队列显示与管理员清空。`ConsoleOverview.vue` 继续是音乐状态与搜索请求的唯一所有者，向子组件传入 `MusicState` 与回调。

## Tech Stack

Vue 2.6、TypeScript 3.9、Vue Router 3、Buefy 与 Material Design Icons、Less、Webpack 4。

## Baseline/Authority Refs

- `docs/aegis/specs/2026-07-16-cerumusic-web-console-design.md`
- `docs/aegis/plans/2026-07-15-web-console.md`
- 当前前端：`WebInterface/src/ts/App.vue`、`Components/Navbar.vue`、`Pages/Home.vue`、`Pages/ConsoleOverview.vue`、`Pages/ConsoleAdmin.vue`
- 参考仅限视觉与交互结构：`https://github.com/xie-maker/CeruMusic`；不复制其 AGPL 源码或资源。

## Compatibility Boundary

- 不修改 `ConsoleApi.ts` 的请求路径、Cookie 会话、后端音乐 API 或 TS3 权限。
- 普通用户保留搜索、立即播放、加入待播、上一首、暂停/继续、下一首；管理员才可清空队列与进入管理页。
- `coverUrl` 继续由当前状态 API 提供；无封面时用现有 Buefy 图标占位。
- 不加入 CeruMusic 源码、依赖、图片、logo、字体或下载功能。

## Verification

```powershell
Set-Location 'C:\Users\Administrator\Documents\ts3bot 波点\TS3AudioBot\WebInterface'
$env:NODE_OPTIONS='--openssl-legacy-provider'
npm.cmd run build
```

预期：Webpack 退出码为 `0` 并更新 `dist\bundle.js`。随后运行本地网页控制台，分别以普通账号、管理员账号在桌面宽度和 390px 手机宽度检查验收清单。

## Plan Basis

- Requirement Ready Check：`ready`。用户已确认规格，验收标准已限定在 UI 重构与现有业务兼容。
- Change Necessity：非代码方式无法把三栏页面改为侧栏、固定播放器和队列抽屉；最小改动边界是当前 Vue 壳层、三张页面与新增展示组件。`Decision: code-change`。
- Existence Check：新增 `ConsoleShell.vue`、`ConsolePlayerBar.vue`、`ConsoleQueueDrawer.vue` 分别拥有壳层、底部播放器和抽屉职责；复用现有 `Navbar.vue` 会让导航、播放器、队列状态继续交叉耦合。`Decision: add-with-proof`。
- Architecture Integrity Lens：音乐状态和 API 仍只归 `ConsoleOverview.vue` 所有；新组件不得自行轮询或调用 API。旧 `Navbar.vue` 将被壳层取代，只保留给未迁移旧路由时使用；当前活跃控制台不再引用它。
- Complexity Budget：`ConsoleOverview.vue` 当前约 16 行压缩源码但内容密度高；拆分后每个组件只有一项职责，预算 `within-budget`。

## File Map

| 文件 | 操作 | 责任 |
| --- | --- | --- |
| `WebInterface/src/ts/App.vue` | 修改 | 登录页外的控制台壳层挂载点与全局浅色设计变量。 |
| `WebInterface/src/ts/Components/ConsoleShell.vue` | 新增 | 侧栏、顶部工具栏、桌面/移动导航、账号退出。 |
| `WebInterface/src/ts/Components/ConsolePlayerBar.vue` | 新增 | 固定底部播放器与移动端全屏播放视图。 |
| `WebInterface/src/ts/Components/ConsoleQueueDrawer.vue` | 新增 | 右侧/底部队列抽屉和管理员清空按钮。 |
| `WebInterface/src/ts/Pages/ConsoleOverview.vue` | 重写 | 搜索、最近播放、音乐状态所有权与对子组件事件的 API 调用。 |
| `WebInterface/src/ts/Pages/ConsoleAdmin.vue` | 重写样式 | CeruMusic 风格设置表单和账号列表。 |
| `WebInterface/src/ts/Pages/Home.vue` | 重写样式 | 同一设计系统下的登录、首次管理员与机器人配置。 |
| `WebInterface/src/ts/Components/Navbar.vue` | 删除或停用 | 避免活跃控制台同时拥有旧顶栏与新壳层。 |
| `WebInterface/src/ts/Main.ts` | 仅按需修改 | 维持 `/`、`/music`、`/admin` 路由，不增加后端路由。 |

## Tasks

### 1. 建立控制台壳层和设计令牌

**Files:** 创建 `Components/ConsoleShell.vue`；修改 `App.vue`、`Main.ts`；停用 `Components/Navbar.vue` 的活跃引用。

**Why:** 固定侧栏、顶部搜索和移动导航是 CeruMusic 体验的基础，且必须由一个组件统一管理。

**Impact/Compatibility:** 壳层只负责路由、账号名称、角色和退出登录；不得访问音乐 API。

- [ ] 先在 `ConsoleShell.vue` 定义 `brandName`、`isAdmin`、退出登录及 `music/admin` 导航；通过 `consoleApi<ConsoleUser>("me")` 初始化。
- [ ] 桌面样式实现 76px 可收起侧栏、白色顶部工具栏、圆形图标按钮和最大内容宽度；移动端改为固定底部图标导航。
- [ ] 在 `App.vue` 中仅对 `/music` 与 `/admin` 挂载 `ConsoleShell`，让 `/` 的登录流程保持无侧栏。
- [ ] 删除 `App.vue` 对旧 `Navbar.vue` 的活跃引用；不要删除旧组件，直到确认没有旧页面引用。
- [ ] 运行 `npm.cmd run build`，预期退出码 `0`；手工检查普通账号不显示管理导航。

### 2. 重构音乐页和底部播放器

**Files:** 创建 `Components/ConsolePlayerBar.vue`；重写 `Pages/ConsoleOverview.vue`。

**Why:** 音乐页必须从当前三栏看板变为 CeruMusic 式主列表加常驻播放器，同时保持所有点歌请求的现有行为。

**Impact/Compatibility:** `ConsoleOverview.vue` 保持 5 秒状态刷新和现有 `music/search`、`music/play`、`music/add`、`music/previous`、`music/pause`、`music/next` 调用；播放器只通过事件调用父级方法。

- [ ] 在播放器组件定义 `state: MusicState` 属性，发出 `previous`、`pause`、`next`、`queue` 事件；有 `coverUrl` 渲染 `img`，否则渲染音乐图标。
- [ ] 实现固定底部条：封面与标题在左、上一首/播放暂停/下一首在中、进度时间和队列按钮在右；所有图标按钮提供 `title`。
- [ ] 为窄屏增加迷你底部条和点击展开的全屏播放视图，使用 `position: fixed`、稳定高度、底部安全留白，保证搜索结果不会被遮挡。
- [ ] 将音乐页改为顶部搜索、搜索结果歌曲行与最近播放歌曲行；立即播放和加入待播保留 icon-only 控件及提示文本。
- [ ] 运行 `npm.cmd run build`，预期退出码 `0`；登录普通账号后手工验证搜索、立即播放、加入待播、上一首、暂停、下一首均请求原 API。

### 3. 建立队列抽屉和管理员边界

**Files:** 创建 `Components/ConsoleQueueDrawer.vue`；修改 `Pages/ConsoleOverview.vue`。

**Why:** 队列不再占用桌面主栏，改为 CeruMusic 参考中的可收起队列面板，同时必须保持清空权限不可绕过。

**Impact/Compatibility:** 抽屉接收队列与 `isAdmin`，只发出 `clear`/`close` 事件；实际清空继续由父级调用现有管理员受保护 API。

- [ ] 实现桌面端右侧滑入抽屉和移动端底部滑入抽屉，打开时显示遮罩并支持关闭按钮与遮罩关闭。
- [ ] 使用 `track.active` 高亮当前歌曲；条目采用序号、标题、来源的紧凑行，不输出删除或排序功能。
- [ ] 仅当 `isAdmin && queue.length > 0` 渲染清空按钮；普通用户 DOM 中不渲染此按钮。
- [ ] 在音乐页响应播放器的 `queue` 事件切换抽屉，并将抽屉 `clear` 事件接到现有 `clearQueue` 方法。
- [ ] 运行 `npm.cmd run build`，预期退出码 `0`；普通账号检查无清空按钮，管理员检查点击后调用 `music/clear`。

### 4. 统一登录、首次配置和管理页面

**Files:** 重写 `Pages/Home.vue` 与 `Pages/ConsoleAdmin.vue` 的模板样式；按需更新 `App.vue` 全局变量。

**Why:** 登录与管理页面需要和播放器共享浅色、圆角、细边框和按钮层级，不能保留旧深色卡片。

**Impact/Compatibility:** 不改 `Home.vue` 的 `status/setup/login/setup/bot` 请求，也不改管理页的账号、品牌、机器人保存 API。

- [ ] 登录页建立无侧栏的浅色背景、居中表单、明确可见的品牌文字与高对比提交按钮；机器人名称输入保持 `type="text"`。
- [ ] 管理页使用左对齐标题、分组设置面板、紧凑账号列表和管理员专属状态开关；所有输入在 390px 宽度内不溢出。
- [ ] 为输入框、主按钮、次要图标按钮、错误提示定义同一组颜色、边框、焦点与禁用状态；不得使用 CeruMusic 的图片或 logo。
- [ ] 运行 `npm.cmd run build`，预期退出码 `0`；手工验证首次管理员创建、登录、机器人配置、创建子账号、停用账号仍调用原路径。

### 5. 生成和审查视觉证据，完成交付包

**Files:** 仅生成 `docs/web-console-ceru-desktop-reference.*`、`docs/web-console-ceru-mobile-reference.*`、`docs/web-console-ceru-login-reference.*`；重新构建 `WebInterface/dist`。

**Why:** 用户要求先看效果图；图必须来自实际运行页面，不能与实现脱节。

**Impact/Compatibility:** 不改变 API、机器人或部署配置。

- [ ] 启动本地网页控制台并使用已初始化账号打开 `/music`、`/admin` 与 `/`；采集 1440px 桌面、390px 手机和登录页截图。
- [ ] 检查截图：侧栏/顶栏/底部播放器同时可见；抽屉未遮挡关键控件；手机没有横向滚动；文本没有重叠；封面缺失时图标占位正常。
- [ ] 运行前端构建命令并确认 `dist/bundle.js` 包含新组件代码。
- [ ] 按现有 Windows 打包脚本构建一个新目录，不覆盖现有发布包；检查 `WebInterface/bundle.js`、插件和 `ffmpeg.exe` 都存在。
- [ ] 不在当前脏工作树中执行泛化 `git add` 或提交；仅在用户要求后再创建可审查的提交。

## Risks and Rollback

- 固定播放器可能覆盖小屏内容：每个可滚动页面必须预留播放器与移动导航的底部空间。
- 将状态逻辑下沉到组件会导致重复轮询：所有轮询和 API 调用继续只放在 `ConsoleOverview.vue`。
- 队列抽屉可能暴露管理员操作：渲染层与后端 API 两侧都保持管理员限制。
- 回滚是还原本次前端组件与页面改动；不触及数据库、机器人配置或 TS3 权限文件。

## Retirement

- 旧顶栏 `Navbar.vue`：从活跃控制台路径移除；保留源文件直至确认未迁移旧页面没有引用，再另行删除。
- 旧三栏播放器/队列 UI：由音乐页重写替代，不保留并行界面或样式兼容分支。
- CeruMusic 外部参考目录：只在临时目录读取，不纳入本项目和发布包。

## Execution Readiness View

- Intent Lock：实现已确认的 CeruMusic 风格 UI，不增加音乐业务能力。
- Scope Fence：仅 `WebInterface`、视觉截图和新发布包；不修改 C# API、TS3 命令、权限或数据。
- Baseline Lock：以上规格和既有网页控制台计划。
- Owner / Contract Constraints：`ConsoleOverview.vue` 仍是音乐状态/API 所有者；`ConsoleApi.ts` 请求合约不变。
- Task Batches：壳层；播放器和主页面；队列；登录管理；截图与构建。
- Test Obligations：每批前端构建成功；普通/管理员权限、桌面/手机布局、现有 API 交互手测。
- Drift / Rewind Rules：若需要后端接口、新权限或 CeruMusic 资源，停止并回到设计；若播放器覆盖内容，先修布局再继续。
- Evidence Required Before Completion：构建退出码、截图、发布包文件清单、权限手测记录。
- Advisory Boundary：此视图仅为实施指引，不是完成授权。
