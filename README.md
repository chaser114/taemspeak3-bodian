# TS3AudioBot 波点音乐插件

基于 [Splamy/TS3AudioBot](https://github.com/Splamy/TS3AudioBot) 的自用定制版。机器人通过独立的 `KuwoMusicPlugin.dll` 调用酷我音乐 API 搜索和播放歌曲。

## 功能

- `!search 歌曲名`：搜索波点音乐的内容
- `!play 歌曲名`：搜索并直接播放第一首
- `!play 序号`：播放最近一次搜索结果中的对应歌曲
- 播放时将机器人 TeamSpeak 简介更新为歌曲名；空闲时显示“休眠中”
- 插件独立于主程序，文件位于 `plugins/KuwoMusicPlugin.dll`


## 下载

在 GitHub 的 [Releases](../../releases) 页面下载带版本号的构建包：

- Windows：`TS3AudioBot-KuwoPlugin-windows-x64.zip`
- Linux：`TS3AudioBot-KuwoPlugin-linux-x64.tar.gz`

每次推送到 `main`、`master` 或手动运行 Actions 时，也可以在 Actions 对应任务的 Artifacts 中下载测试包。

## 部署

### Linux（Ubuntu 22.04 x64）

```bash
sudo apt update
sudo apt install -y ffmpeg libopus-dev
tar -xzf TS3AudioBot-KuwoPlugin-linux-x64.tar.gz
cd TS3AudioBot-KuwoPlugin-linux-x64
./start.sh
```

`ffmpeg` 负责音频转码；`libopus-dev` 提供 TeamSpeak 音频编码所需的 `libopus.so`。首次启动会生成配置文件，按控制台提示填写 TeamSpeak 地址和账号后再启动即可。

### Windows x64

解压 Windows 包后运行 `start.cmd`。包内已包含 `.NET runtime` 和 `ffmpeg.exe`。

## 更换 API

当前 API 地址只在 [KuwoMusicPlugin.cs](KuwoMusicPlugin/KuwoMusicPlugin.cs) 中定义一次：

```csharp
private const string ApiUrl = "https://api.xingzhige.com/API/Kuwo_BD_new/";
```

接口失效时，先把这行替换成新 API 的基础地址。新接口必须支持关键词和结果序号请求；当前请求格式为：

```text
{ApiUrl}?name=歌曲名&n=序号
```

若新接口的 JSON 字段不同，在同一文件底部的 `KuwoResponse`、`KuwoSong` 和 `KuwoRaw` 中调整字段，并在 `ToResource` 内映射以下数据：歌曲 ID、歌名、歌手、封面链接和可播放音频链接。不要修改 `MainCommands.cs`，`!search` 和 `!play` 已固定调用该插件。

修改完成后提交并推送；GitHub Actions 会自动构建新的安装包。也可以在本地执行：

```bash
dotnet build -c Release TS3AudioBot/TS3AudioBot.csproj
dotnet build -c Release KuwoMusicPlugin/KuwoMusicPlugin.csproj
```

## 开源说明

本仓库基于上游 TS3AudioBot，保留原有 [OSL-3.0](LICENSE) 许可证和版权文件。发布前请确认使用的第三方音乐 API 允许你的使用方式。

## 声明

- 本插件由chatgpt开发，插件只是用来自用更新可能较慢，如需增添功能可以发issues，或者自己fork仓库，让AI给你改
- 使用的api[星之阁API](https://api.xingzhige.com/)
