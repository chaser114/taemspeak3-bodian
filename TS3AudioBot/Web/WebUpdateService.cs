using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TS3AudioBot.Environment;

namespace TS3AudioBot.Web
{
	/// <summary>
	/// One-click program update for the web console.
	/// Only replaces program files; data/ is never overwritten.
	/// </summary>
	public sealed class WebUpdateService
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly HttpClient Http = CreateHttpClient();
		private static int busy;

		// Public release channels. Domestic default is GitCode (Gitee release quota is limited).
		private const string GithubOwner = "chaser114";
		private const string GithubRepo = "taemspeak3-bodian";
		private const string GitcodeOwner = "chaser114";
		private const string GitcodeRepo = "taemspeak3-bodian";

		public string InstallRoot { get; }
		public string CurrentVersion { get; }

		public WebUpdateService()
		{
			InstallRoot = ResolveInstallRoot();
			CurrentVersion = ReadCurrentVersion(InstallRoot);
		}

		public object GetStatus()
		{
			return new
			{
				currentVersion = CurrentVersion,
				platform = DetectPlatform(),
				busy = Volatile.Read(ref busy) != 0,
				sources = new object[]
				{
					// Default: GitHub metadata + domestic proxy download. No third-party package upload.
					new { id = "github-cn", label = "国内加速（推荐）", defaultSource = true },
					new { id = "github", label = "GitHub 官方源", defaultSource = false },
				}
			};
		}

		public async Task<object> CheckAsync(string? preferredSource = null)
		{
			var platform = DetectPlatform();
			var errors = new List<string>();
			ReleaseInfo? github = null;

			try { github = await FetchLatestAsync("github", platform); }
			catch (Exception ex) { errors.Add("GitHub: " + ex.Message); Log.Warn(ex, "GitHub update check failed."); }

			// github-cn reuses GitHub release metadata, only rewrites download URL via proxy.
			var githubCn = github is null ? null : CloneWithProxiedUrl(github);

			var preferred = NormalizeSource(preferredSource);
			ReleaseInfo? selected = preferred == "github"
				? (github ?? githubCn)
				: (githubCn ?? github);
			var selectedSource = selected is null
				? preferred
				: (preferred == "github" && github != null && ReferenceEquals(selected, github) ? "github" : "github-cn");

			var hasUpdate = selected != null && IsNewer(selected.Tag, CurrentVersion);
			return new
			{
				currentVersion = CurrentVersion,
				latestVersion = selected?.Tag,
				hasUpdate,
				source = selected is null ? null : selectedSource,
				notes = selected?.Notes,
				downloadName = selected?.AssetName,
				downloadUrl = selected?.AssetUrl,
				publishedAt = selected?.PublishedAt,
				platform,
				sources = new object[]
				{
					new
					{
						id = "github-cn",
						label = "国内加速（推荐）",
						available = githubCn != null,
						latestVersion = githubCn?.Tag,
						hasUpdate = githubCn != null && IsNewer(githubCn.Tag, CurrentVersion),
					},
					new
					{
						id = "github",
						label = "GitHub 官方源",
						available = github != null,
						latestVersion = github?.Tag,
						hasUpdate = github != null && IsNewer(github.Tag, CurrentVersion),
					},
				},
				errors,
			};
		}

		public async Task<object> ApplyAsync(string sourceId, string? passwordVerifiedMarker)
		{
			if (passwordVerifiedMarker != "ok")
				throw new InvalidOperationException("请先验证管理员密码。");

			if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
				throw new InvalidOperationException("已有更新任务在进行中，请稍后再试。");

			try
			{
				EnsureWritableInstallRoot();
				var platform = DetectPlatform();
				var source = NormalizeSource(sourceId);
				var release = await FetchLatestAsync(source, platform);
				if (!IsNewer(release.Tag, CurrentVersion))
					throw new InvalidOperationException("当前已是最新版本。");

				var staging = Path.Combine(InstallRoot, ".update-staging");
				if (Directory.Exists(staging))
					Directory.Delete(staging, true);
				Directory.CreateDirectory(staging);

				var packagePath = Path.Combine(staging, release.AssetName);
				Log.Info("Downloading update {0} from {1}: {2}", release.Tag, source, release.AssetUrl);
				await DownloadFileAsync(release.AssetUrl, packagePath);

				var extractDir = Path.Combine(staging, "extract");
				Directory.CreateDirectory(extractDir);
				ExtractPackage(packagePath, extractDir);
				var packageRoot = FindPackageRoot(extractDir, platform);

				// Backup user data first (never delete data/).
				var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
				var backupDir = Path.Combine(InstallRoot, "backup", "pre-update-" + stamp);
				Directory.CreateDirectory(backupDir);
				foreach (var name in new[] { "data", "bots", "ts3audiobot.toml", "ts3audiobot.db", "rights.toml" })
				{
					var src = Path.Combine(InstallRoot, name);
					if (Directory.Exists(src) || File.Exists(src))
						CopyAny(src, Path.Combine(backupDir, name));
				}

				// Write version marker into package root so it is copied over.
				File.WriteAllText(Path.Combine(packageRoot, "VERSION"), release.Tag.Trim() + System.Environment.NewLine, Encoding.UTF8);

				var applyScript = WriteApplyScript(packageRoot, InstallRoot, platform);
				Log.Info("Starting update apply script: {0}", applyScript);
				StartApplyAndExit(applyScript, platform);

				return new
				{
					ok = true,
					message = "更新已开始，程序会自动重启。约 10～20 秒后刷新网页即可。日志：logs/console.log 与 data/logs/；停止：stop.bat 或 ./run/stop-linux.sh。",
					from = CurrentVersion,
					to = release.Tag,
					source,
					backup = backupDir,
					restart = "auto",
					logFile = "logs/console.log",
				};
			}
			finally
			{
				// If we failed before restart, clear busy. On success the process exits.
				Interlocked.Exchange(ref busy, 0);
			}
		}

		private static async Task<ReleaseInfo> FetchLatestAsync(string source, string platform)
		{
			// github-cn: same metadata as GitHub, package download via CN proxy.
			var apiSource = source == "gitcode" ? "gitcode" : "github";
			string apiUrl = apiSource == "gitcode"
				? $"https://gitcode.com/api/v5/repos/{GitcodeOwner}/{GitcodeRepo}/releases/latest"
				: $"https://api.github.com/repos/{GithubOwner}/{GithubRepo}/releases/latest";

			using var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
			req.Headers.TryAddWithoutValidation("Accept", "application/json");
			req.Headers.TryAddWithoutValidation("User-Agent", "taemspeak3-bodian-updater");

			using var resp = await Http.SendAsync(req);
			var body = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode)
				throw new InvalidOperationException($"无法获取 {source} 最新版本（HTTP {(int)resp.StatusCode}）。");

			var json = JObject.Parse(body);
			var tag = (json.Value<string>("tag_name") ?? json.Value<string>("name") ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(tag))
				throw new InvalidOperationException($"{source} 返回的版本号为空。");

			var notes = json.Value<string>("body") ?? string.Empty;
			var published = json.Value<string>("created_at") ?? json.Value<string>("published_at");
			var assets = json["assets"] as JArray ?? new JArray();
			var asset = PickAsset(assets, platform, apiSource);
			if (asset is null)
				throw new InvalidOperationException($"{source} 最新版本中没有适用于 {platform} 的安装包。");

			var url = asset.Value.url;
			if (source == "github-cn")
				url = ToGithubCnProxyUrl(url);

			return new ReleaseInfo
			{
				Tag = tag,
				Notes = Truncate(notes, 2000),
				PublishedAt = published,
				AssetName = asset.Value.name,
				AssetUrl = url,
			};
		}

		private static (string name, string url)? PickAsset(JArray assets, string platform, string source)
		{
			// Prefer canonical package names from CI.
			// Important: GitCode auto-attaches source archives (type=source, e.g. build-44.zip).
			// Never treat those as install packages.
			string[] prefer = platform == "windows"
				? new[] { "TS3AudioBot-KuwoPlugin-windows-x64.zip", "windows-x64.zip", "windows-x64" }
				: new[] { "TS3AudioBot-KuwoPlugin-linux-x64.tar.gz", "linux-x64.tar.gz", "linux-x64" };

			var list = assets
				.OfType<JObject>()
				.Select(a =>
				{
					var name = a.Value<string>("name") ?? string.Empty;
					var assetType = a.Value<string>("type") ?? string.Empty;
					// GitHub: browser_download_url; GitCode/Gitee-style may use browser_download_url / download_url / url.
					var url = a.Value<string>("browser_download_url")
						?? a.Value<string>("download_url")
						?? a.Value<string>("url");
					if (!string.IsNullOrWhiteSpace(url) && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
						url = null;
					return new { name, url, assetType };
				})
				.Where(x => !string.IsNullOrWhiteSpace(x.name) && !string.IsNullOrWhiteSpace(x.url))
				.Where(x => !string.Equals(x.assetType, "source", StringComparison.OrdinalIgnoreCase))
				.Where(x => !IsSourceArchiveName(x.name))
				.ToList();

			foreach (var key in prefer)
			{
				var hit = list.FirstOrDefault(x =>
					x.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0
					&& (platform != "windows" || x.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
					&& (platform != "linux" || x.name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || x.name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)));
				if (hit != null) return (hit.name, hit.url!);
			}

			// Fallback: any matching extension for platform among non-source assets.
			if (platform == "windows")
			{
				var hit = list.FirstOrDefault(x =>
					x.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
					&& x.name.IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0);
				if (hit != null) return (hit.name, hit.url!);
			}
			else
			{
				var hit = list.FirstOrDefault(x =>
					(x.name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || x.name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
					&& x.name.IndexOf("linux", StringComparison.OrdinalIgnoreCase) >= 0);
				if (hit != null) return (hit.name, hit.url!);
			}
			return null;
		}

		private static bool IsSourceArchiveName(string name)
		{
			// GitCode auto source names: build-44.zip / build-44.tar.gz / ...
			if (string.IsNullOrWhiteSpace(name)) return false;
			var n = name.Trim();
			if (n.StartsWith("Source", StringComparison.OrdinalIgnoreCase)) return true;
			if (System.Text.RegularExpressions.Regex.IsMatch(n, @"^build-\d+(\.[a-z0-9]+)*\.(zip|tar\.gz|tgz|tar\.bz2|tar)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
				return true;
			return false;
		}

		private static async Task DownloadFileAsync(string url, string path)
		{
			var candidates = BuildDownloadCandidates(url);
			Exception? last = null;
			foreach (var candidate in candidates)
			{
				try
				{
					using var req = new HttpRequestMessage(HttpMethod.Get, candidate);
					req.Headers.TryAddWithoutValidation("User-Agent", "taemspeak3-bodian-updater");
					using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
					if (!resp.IsSuccessStatusCode)
					{
						last = new InvalidOperationException($"HTTP {(int)resp.StatusCode} from {candidate}");
						Log.Warn("Download candidate failed: {0}", last.Message);
						continue;
					}
					await using var input = await resp.Content.ReadAsStreamAsync();
					await using var output = File.Create(path);
					await input.CopyToAsync(output);
					if (candidate != url)
						Log.Info("Downloaded update package via proxy: {0}", candidate);
					return;
				}
				catch (Exception ex)
				{
					last = ex;
					Log.Warn(ex, "Download candidate failed: {0}", candidate);
				}
			}
			throw new InvalidOperationException("下载更新包失败：" + (last?.Message ?? "所有下载地址均不可用。"), last);
		}

		private static IEnumerable<string> BuildDownloadCandidates(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				yield break;

			// If already a proxy URL, try it first then strip to direct.
			if (url.StartsWith("https://ghproxy.net/", StringComparison.OrdinalIgnoreCase)
				|| url.StartsWith("https://mirror.ghproxy.com/", StringComparison.OrdinalIgnoreCase)
				|| url.StartsWith("https://gh.ddlc.top/", StringComparison.OrdinalIgnoreCase)
				|| url.StartsWith("https://gitclone.com/", StringComparison.OrdinalIgnoreCase))
			{
				yield return url;
				var direct = url
					.Replace("https://ghproxy.net/", "", StringComparison.OrdinalIgnoreCase)
					.Replace("https://mirror.ghproxy.com/", "", StringComparison.OrdinalIgnoreCase)
					.Replace("https://gh.ddlc.top/", "", StringComparison.OrdinalIgnoreCase);
				if (direct.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
					yield return direct;
				yield break;
			}

			// Proxied first (domestic), then direct GitHub.
			if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
				|| url.IndexOf("githubusercontent.com/", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				yield return "https://ghproxy.net/" + url;
				yield return "https://mirror.ghproxy.com/" + url;
				yield return "https://gh.ddlc.top/" + url;
			}
			yield return url;
		}

		private static void ExtractPackage(string packagePath, string extractDir)
		{
			if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
			{
				ZipFile.ExtractToDirectory(packagePath, extractDir, true);
				return;
			}
			if (packagePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || packagePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
			{
				// Use system tar (available on Linux packages and modern Windows 10+).
				var psi = new ProcessStartInfo
				{
					FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tar.exe" : "tar",
					Arguments = $"-xzf \"{packagePath}\" -C \"{extractDir}\"",
					UseShellExecute = false,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					CreateNoWindow = true,
				};
				using var proc = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 tar 解压更新包。");
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					throw new InvalidOperationException("解压更新包失败: " + proc.StandardError.ReadToEnd());
				return;
			}
			throw new InvalidOperationException("不支持的更新包格式。");
		}

		private static string FindPackageRoot(string extractDir, string platform)
		{
			var exeName = platform == "windows" ? "TS3AudioBot.exe" : "TS3AudioBot";
			var direct = Path.Combine(extractDir, exeName);
			if (File.Exists(direct)) return extractDir;

			var match = Directory.GetFiles(extractDir, exeName, SearchOption.AllDirectories).FirstOrDefault();
			if (match is null)
				throw new InvalidOperationException("更新包中未找到主程序文件。");
			return Path.GetDirectoryName(match)!;
		}

		private string WriteApplyScript(string packageRoot, string installRoot, string platform)
		{
			// Auto-restart so mobile web updates can reconnect without SSH.
			// Console output goes to logs/console.log; PID to ts3audiobot.pid for stop scripts.
			if (platform == "windows")
			{
				var script = Path.Combine(installRoot, ".update-apply.ps1");
				var content = $@"
$ErrorActionPreference = 'Stop'
Start-Sleep -Seconds 2
$src = '{EscapePs(packageRoot)}'
$dst = '{EscapePs(installRoot)}'
$logDir = Join-Path $dst 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir 'update-apply.log'
$consoleLog = Join-Path $logDir 'console.log'
$pidFile = Join-Path $dst 'ts3audiobot.pid'
function Write-Log([string]$msg) {{
  $line = ('[' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '] ' + $msg)
  Add-Content -LiteralPath $log -Value $line
}}
Write-Log 'Applying package files...'
$protected = @('data','bots','ts3audiobot.toml','ts3audiobot.db','rights.toml','NLog.config','logs','backup','.update-staging','.update-apply.ps1','.update-apply.sh','.update-done','ts3audiobot.pid')
Get-ChildItem -LiteralPath $src -Force | ForEach-Object {{
  if ($protected -contains $_.Name) {{ return }}
  $target = Join-Path $dst $_.Name
  if ($_.PSIsContainer) {{
    if (Test-Path -LiteralPath $target) {{ Remove-Item -LiteralPath $target -Recurse -Force }}
    Copy-Item -LiteralPath $_.FullName -Destination $target -Recurse -Force
  }} else {{
    Copy-Item -LiteralPath $_.FullName -Destination $target -Force
  }}
}}
if (Test-Path -LiteralPath (Join-Path $src 'VERSION')) {{
  Copy-Item -LiteralPath (Join-Path $src 'VERSION') -Destination (Join-Path $dst 'VERSION') -Force
}}
$prepare = Join-Path $dst 'packaging\common\prepare-data.ps1'
if (Test-Path -LiteralPath $prepare) {{ & $prepare -Root $dst | Out-Null }}
$data = Join-Path $dst 'data'
New-Item -ItemType Directory -Force -Path $data | Out-Null
$exe = Join-Path $dst 'TS3AudioBot.exe'
if (-not (Test-Path -LiteralPath $exe)) {{ throw 'TS3AudioBot.exe missing after update' }}
# Stop any leftover instance from this install before restart.
if (Test-Path -LiteralPath $pidFile) {{
  $oldPid = (Get-Content -LiteralPath $pidFile -ErrorAction SilentlyContinue | Select-Object -First 1)
  if ($oldPid) {{ Stop-Process -Id $oldPid -Force -ErrorAction SilentlyContinue }}
}}
Get-Process -Name 'TS3AudioBot' -ErrorAction SilentlyContinue | Where-Object {{ $_.Path -and $_.Path.StartsWith($dst, [StringComparison]::OrdinalIgnoreCase) }} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Write-Log 'Starting bot (console output -> logs/console.log)...'
$p = Start-Process -FilePath $exe -ArgumentList '--config','ts3audiobot.toml','--non-interactive' -WorkingDirectory $data -RedirectStandardOutput $consoleLog -RedirectStandardError $consoleLog -PassThru -WindowStyle Hidden
Set-Content -LiteralPath $pidFile -Value $p.Id -Encoding ASCII
Set-Content -LiteralPath (Join-Path $dst '.update-done') -Value ((Get-Date -Format 'o') + [Environment]::NewLine + 'pid=' + $p.Id + [Environment]::NewLine + 'log=logs/console.log') -Encoding UTF8
Write-Log ('Bot restarted with PID ' + $p.Id)
Remove-Item -LiteralPath (Join-Path $dst '.update-staging') -Recurse -Force -ErrorAction SilentlyContinue
";
				File.WriteAllText(script, content, Encoding.UTF8);
				return script;
			}
			else
			{
				var script = Path.Combine(installRoot, ".update-apply.sh");
				var content = $@"#!/usr/bin/env sh
set -eu
sleep 2
src='{EscapeSh(packageRoot)}'
dst='{EscapeSh(installRoot)}'
mkdir -p ""$dst/logs"" ""$dst/data""
log=""$dst/logs/update-apply.log""
console_log=""$dst/logs/console.log""
pid_file=""$dst/ts3audiobot.pid""
logline() {{ printf '[%s] %s\n' ""$(date '+%Y-%m-%d %H:%M:%S')"" ""$1"" >> ""$log""; }}
logline 'Applying package files...'
for entry in ""$src""/* ""$src""/.[!.]* ""$src""/..?*; do
  [ -e ""$entry"" ] || continue
  name=$(basename -- ""$entry"")
  case ""$name"" in
    .|..) continue ;;
    data|bots|ts3audiobot.toml|ts3audiobot.db|rights.toml|NLog.config|logs|backup|.update-staging|.update-apply.ps1|.update-apply.sh|.update-done|ts3audiobot.pid) continue ;;
  esac
  rm -rf ""$dst/$name""
  cp -a ""$entry"" ""$dst/$name""
done
if [ -f ""$src/VERSION"" ]; then cp -a ""$src/VERSION"" ""$dst/VERSION""; fi
if [ -f ""$dst/packaging/common/prepare-data.sh"" ]; then sh ""$dst/packaging/common/prepare-data.sh"" ""$dst"" >/dev/null; fi
if [ -f ""$pid_file"" ]; then
  oldpid=$(cat ""$pid_file"" 2>/dev/null || true)
  if [ -n ""${{oldpid:-}}"" ]; then kill ""$oldpid"" 2>/dev/null || true; sleep 1; kill -9 ""$oldpid"" 2>/dev/null || true; fi
fi
if command -v pgrep >/dev/null 2>&1; then
  pids=$(pgrep -f ""$dst/TS3AudioBot"" 2>/dev/null || true)
  if [ -n ""$pids"" ]; then kill $pids 2>/dev/null || true; sleep 1; kill -9 $pids 2>/dev/null || true; fi
fi
if [ ! -x ""$dst/TS3AudioBot"" ]; then chmod +x ""$dst/TS3AudioBot"" 2>/dev/null || true; fi
logline 'Starting bot (console output -> logs/console.log)...'
cd ""$dst/data""
nohup ""$dst/TS3AudioBot"" --config ts3audiobot.toml --non-interactive >>""$console_log"" 2>&1 &
echo $! > ""$pid_file""
printf '%s\npid=%s\nlog=logs/console.log\n' ""$(date -Iseconds 2>/dev/null || date)"" ""$(cat ""$pid_file"")"" > ""$dst/.update-done""
logline ""Bot restarted with PID $(cat ""$pid_file"")""
rm -rf ""$dst/.update-staging""
";
				File.WriteAllText(script, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "chmod",
						Arguments = "+x \"" + script + "\"",
						UseShellExecute = false,
						CreateNoWindow = true,
					})?.WaitForExit();
				}
				catch { /* best effort */ }
				return script;
			}
		}

		private static void StartApplyAndExit(string scriptPath, string platform)
		{
			if (platform == "windows")
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "powershell",
					Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
				});
			}
			else
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "/bin/sh",
					Arguments = "\"" + scriptPath + "\"",
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
				});
			}

			// Give the HTTP response a moment to flush, then exit this process.
			_ = Task.Run(async () =>
			{
				await Task.Delay(800);
				try { Environment.SystemData.AssemblyData.ToString(); } catch { /* ignore */ }
				System.Environment.Exit(0);
			});
		}

		private static string ResolveInstallRoot()
		{
			// Prefer the directory that contains the published binary / package layout.
			var asmDir = Path.GetDirectoryName(typeof(Core).Assembly.Location);
			if (!string.IsNullOrWhiteSpace(asmDir))
			{
				if (File.Exists(Path.Combine(asmDir, "TS3AudioBot.exe"))
					|| File.Exists(Path.Combine(asmDir, "TS3AudioBot"))
					|| Directory.Exists(Path.Combine(asmDir, "plugins"))
					|| Directory.Exists(Path.Combine(asmDir, "WebInterface")))
					return Path.GetFullPath(asmDir);
			}

			// When started with CWD=data, parent is the package root.
			var cwd = Directory.GetCurrentDirectory();
			var parent = Directory.GetParent(cwd)?.FullName;
			if (!string.IsNullOrWhiteSpace(parent)
				&& (File.Exists(Path.Combine(parent, "TS3AudioBot.exe")) || File.Exists(Path.Combine(parent, "TS3AudioBot"))))
				return parent!;

			return Path.GetFullPath(cwd);
		}

		private void EnsureWritableInstallRoot()
		{
			try
			{
				var probe = Path.Combine(InstallRoot, ".update-write-test");
				File.WriteAllText(probe, "ok");
				File.Delete(probe);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException(
					"当前安装目录不可写，网页升级无法替换程序文件。Docker 部署请使用 docker compose up -d --build；Windows/Linux 解压包请确保对安装目录有写权限。",
					ex);
			}
		}

		private static string ReadCurrentVersion(string installRoot)
		{
			try
			{
				var versionFile = Path.Combine(installRoot, "VERSION");
				if (File.Exists(versionFile))
				{
					var text = File.ReadAllText(versionFile).Trim();
					if (!string.IsNullOrWhiteSpace(text)) return text;
				}
			}
			catch { /* ignore */ }

			var build = SystemData.AssemblyData.Version;
			if (!string.IsNullOrWhiteSpace(build) && build != "<?>") return build.Trim();
			return "unknown";
		}

		private static string DetectPlatform()
			=> RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";

		private static string NormalizeSource(string? source)
		{
			if (string.IsNullOrWhiteSpace(source)) return "github-cn";
			var s = source.Trim().ToLowerInvariant();
			if (s == "github") return "github";
			// gitcode/gitee and other domestic labels → GitHub + CN proxy.
			return "github-cn";
		}

		private static ReleaseInfo CloneWithProxiedUrl(ReleaseInfo src)
		{
			return new ReleaseInfo
			{
				Tag = src.Tag,
				Notes = src.Notes,
				PublishedAt = src.PublishedAt,
				AssetName = src.AssetName,
				AssetUrl = ToGithubCnProxyUrl(src.AssetUrl),
			};
		}

		/// <summary>
		/// Rewrite github.com / githubusercontent download URLs through a public CN proxy.
		/// Metadata still comes from GitHub API; only the package blob download is proxied.
		/// </summary>
		private static string ToGithubCnProxyUrl(string url)
		{
			if (string.IsNullOrWhiteSpace(url)) return url;
			// Already proxied
			if (url.StartsWith("https://ghproxy.net/", StringComparison.OrdinalIgnoreCase)
				|| url.StartsWith("https://mirror.ghproxy.com/", StringComparison.OrdinalIgnoreCase)
				|| url.StartsWith("https://gh.ddlc.top/", StringComparison.OrdinalIgnoreCase))
				return url;

			// Prefer ghproxy.net for release assets.
			if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
				|| url.StartsWith("https://objects.githubusercontent.com/", StringComparison.OrdinalIgnoreCase)
				|| url.StartsWith("https://release-assets.githubusercontent.com/", StringComparison.OrdinalIgnoreCase)
				|| url.IndexOf("githubusercontent.com/", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "https://ghproxy.net/" + url;
			}
			return url;
		}

		internal static bool IsNewer(string remoteTag, string current)
		{
			if (string.IsNullOrWhiteSpace(remoteTag)) return false;
			if (string.IsNullOrWhiteSpace(current) || current == "unknown" || current == "<?>") return true;
			if (string.Equals(remoteTag.Trim(), current.Trim(), StringComparison.OrdinalIgnoreCase)) return false;

			// Prefer build-N style from Actions (build-22).
			var remoteBuild = ParseBuildNumber(remoteTag);
			var currentBuild = ParseBuildNumber(current);
			if (remoteBuild.HasValue && currentBuild.HasValue)
				return remoteBuild.Value > currentBuild.Value;

			// Fallback: semantic-ish version compare on digit groups.
			var r = ParseVersionParts(remoteTag);
			var c = ParseVersionParts(current);
			if (r.Length > 0 && c.Length > 0)
			{
				var len = Math.Max(r.Length, c.Length);
				for (int i = 0; i < len; i++)
				{
					var rv = i < r.Length ? r[i] : 0;
					var cv = i < c.Length ? c[i] : 0;
					if (rv != cv) return rv > cv;
				}
				return false;
			}

			// Unknown formats: treat different remote tag as update available.
			return true;
		}

		private static int? ParseBuildNumber(string value)
		{
			var m = Regex.Match(value, @"build[-_]?(\d+)", RegexOptions.IgnoreCase);
			if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) return n;
			if (int.TryParse(value.Trim().TrimStart('v', 'V'), out var plain)) return plain;
			return null;
		}

		private static int[] ParseVersionParts(string value)
		{
			return Regex.Matches(value, @"\d+")
				.Cast<Match>()
				.Select(m => int.TryParse(m.Value, out var n) ? n : 0)
				.ToArray();
		}

		private static void CopyAny(string src, string dst)
		{
			if (Directory.Exists(src))
			{
				CopyDirectory(src, dst);
				return;
			}
			var dir = Path.GetDirectoryName(dst);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			File.Copy(src, dst, true);
		}

		private static void CopyDirectory(string src, string dst)
		{
			Directory.CreateDirectory(dst);
			foreach (var file in Directory.GetFiles(src))
				File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
			foreach (var dir in Directory.GetDirectories(src))
				CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
		}

		private static string Truncate(string value, int max)
			=> value.Length <= max ? value : value.Substring(0, max) + "…";

		private static string EscapePs(string value) => value.Replace("'", "''");
		private static string EscapeSh(string value) => value.Replace("'", "'\\''");

		private static HttpClient CreateHttpClient()
		{
			var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
			client.DefaultRequestHeaders.UserAgent.ParseAdd("taemspeak3-bodian-updater");
			return client;
		}

		private sealed class ReleaseInfo
		{
			public string Tag { get; set; } = string.Empty;
			public string Notes { get; set; } = string.Empty;
			public string? PublishedAt { get; set; }
			public string AssetName { get; set; } = string.Empty;
			public string AssetUrl { get; set; } = string.Empty;
		}
	}
}
