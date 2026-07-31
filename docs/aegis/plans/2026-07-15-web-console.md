# 网页音乐控制台实施计划

## Goal
在现有 WebInterface 中增加首次初始化、账号密码登录、管理员与点歌用户权限、TS3 连接配置和中文音乐控制台，同时保留 TS3 聊天命令。

## Architecture
`WebAccountService` 持久化网页账户和会话；`WebServer` 提供受 Cookie 保护的 `/console-api`；Vue 2 WebInterface 消费该 API。原 `/api` Basic Token 接口继续保留以保证既有界面兼容。

## Compatibility Boundary
- 不改变 `!search`、`!play`、`!add`、`!next` 的 TS3 普通成员可用策略。
- `!clear` 仅保留给管理员。
- 不修改用户维护的 `README.md`。

## Verification
- 串行编译主程序和 WebInterface。
- 检查首次初始化、登录、普通用户权限拦截和管理员接口授权。

## Tasks
1. 新增 LiteDB 网页账户、会话和首次初始化服务；密码使用 PBKDF2 哈希。
2. 在 Kestrel 管线增加 Cookie 会话 API、管理员账号管理和初始化入口。
3. 为网页 API 建立最小的管理员/点歌用户命令白名单，不暴露底层 Token。
4. 新增中文 Vue 页面：初始化、登录、音乐页、账号管理、TS3 设置。
5. 默认权限模板修改为所有 TS3 成员可点歌，`!clear` 仅管理员。
6. 构建并生成静态界面参考图。

## Risks
首次初始化入口必须在创建首个管理员后关闭；服务器密码不能返回给浏览器；公开部署必须建议 HTTPS。
