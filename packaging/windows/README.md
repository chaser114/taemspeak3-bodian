# Windows 部署

## 首次安装

直接双击运行 `start-web-console.bat`。包内已包含 .NET runtime、`ffmpeg.exe`、
网页控制台和 `KuwoMusicPlugin.dll`。

启动后访问 `http://127.0.0.1:58913`，首次使用时在网页创建管理员账号并填写 TeamSpeak 服务器地址。

## 数据目录（重要）

所有用户数据都在 **`data\`** 中，与程序文件分开：

- `data\bots\`：每个机器人的连接、频道、昵称等
- `data\ts3audiobot.db`：网页账号、会话、播放历史
- `data\rights.toml`：权限配置
- `data\ts3audiobot.toml`：主配置

**升级时不要删除 `data\`。** 首次从旧版本启动时，若根目录还有旧的 `bots\`、
`ts3audiobot.db` 等，启动脚本会自动迁入 `data\`。

## 无损升级（推荐）

不要把新 zip 直接解压覆盖旧目录。请用升级脚本：

```bat
update-windows.bat ..\TS3AudioBot-KuwoPlugin-windows-x64-new
```

或：

```powershell
powershell -ExecutionPolicy Bypass -File .\run\update-windows.ps1 -Source ..\新版本目录或.zip
```

脚本会：

1. 把当前 `data\`（及旧版根目录配置）备份到 `backup\pre-update-时间戳\`
2. 只更新主程序、`plugins\`、`WebInterface\` 等程序文件
3. 保留机器人、网页账号和管理权限

升级后仍双击 `start-web-console.bat` 启动即可。
