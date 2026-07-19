using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TS3AudioBot.Environment;

namespace TS3AudioBot.Web
{
	/// <summary>
	/// Admin ops similar to Sub2API: view logs and stop/restart the host process from the web console.
	/// </summary>
	public sealed class WebProcessService
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly WebUpdateService webUpdate;

		public WebProcessService(WebUpdateService webUpdate)
		{
			this.webUpdate = webUpdate;
		}

		public object GetStatus()
		{
			var root = webUpdate.InstallRoot;
			var pidFile = Path.Combine(root, "ts3audiobot.pid");
			int? filePid = null;
			if (File.Exists(pidFile)
				&& int.TryParse(File.ReadAllText(pidFile).Trim().Split('\n', '\r')[0], out var parsed))
				filePid = parsed;

			var currentPid = Process.GetCurrentProcess().Id;
			return new
			{
				running = true,
				pid = currentPid,
				pidFile = filePid,
				version = webUpdate.CurrentVersion,
				platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux",
				installRoot = root,
				consoleLog = Rel(root, Path.Combine(root, "logs", "console.log")),
				nlogDir = Rel(root, Path.Combine(Directory.GetCurrentDirectory(), "logs")),
			};
		}

		public object GetLogs(int lines = 200, string? source = null)
		{
			if (lines < 20) lines = 20;
			if (lines > 2000) lines = 2000;

			var root = webUpdate.InstallRoot;
			var prefer = string.IsNullOrWhiteSpace(source) ? "auto" : source.Trim().ToLowerInvariant();
			var candidates = new List<(string id, string path)>();

			var consoleLog = Path.Combine(root, "logs", "console.log");
			if (File.Exists(consoleLog))
				candidates.Add(("console", consoleLog));

			// NLog files are written under current working directory (usually data/logs).
			var nlogDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
			if (Directory.Exists(nlogDir))
			{
				var latest = new DirectoryInfo(nlogDir).EnumerateFiles("*.log")
					.OrderByDescending(f => f.LastWriteTimeUtc)
					.FirstOrDefault();
				if (latest != null)
					candidates.Add(("nlog", latest.FullName));
			}

			// Fallback: installRoot/data/logs
			var dataLogs = Path.Combine(root, "data", "logs");
			if (Directory.Exists(dataLogs))
			{
				var latest = new DirectoryInfo(dataLogs).EnumerateFiles("*.log")
					.OrderByDescending(f => f.LastWriteTimeUtc)
					.FirstOrDefault();
				if (latest != null && candidates.All(c => !string.Equals(c.path, latest.FullName, StringComparison.OrdinalIgnoreCase)))
					candidates.Add(("nlog", latest.FullName));
			}

			string? chosenPath = null;
			string chosenId = "none";
			if (prefer != "auto")
			{
				var hit = candidates.FirstOrDefault(c => c.id == prefer);
				if (!string.IsNullOrEmpty(hit.path))
				{
					chosenPath = hit.path;
					chosenId = hit.id;
				}
			}
			if (chosenPath is null && candidates.Count > 0)
			{
				// Prefer nlog detailed bot logs, then console.
				var nlog = candidates.FirstOrDefault(c => c.id == "nlog");
				var pick = !string.IsNullOrEmpty(nlog.path) ? nlog : candidates[0];
				chosenPath = pick.path;
				chosenId = pick.id;
			}

			if (chosenPath is null || !File.Exists(chosenPath))
			{
				return new
				{
					source = chosenId,
					path = (string?)null,
					lines = Array.Empty<string>(),
					text = "暂无日志文件。启动后日志会出现在 data/logs/ 或 logs/console.log。",
					available = candidates.Select(c => new { c.id, path = Rel(root, c.path) }).ToArray(),
				};
			}

			var tail = ReadTailLines(chosenPath, lines);
			return new
			{
				source = chosenId,
				path = Rel(root, chosenPath),
				lines = tail,
				text = string.Join("\n", tail),
				available = candidates.Select(c => new { c.id, path = Rel(root, c.path) }).ToArray(),
			};
		}

		public object ScheduleStop()
		{
			Log.Info("Admin requested process stop from web console.");
			_ = Task.Run(async () =>
			{
				await Task.Delay(600);
				try
				{
					var pidFile = Path.Combine(webUpdate.InstallRoot, "ts3audiobot.pid");
					if (File.Exists(pidFile))
						File.Delete(pidFile);
				}
				catch { /* ignore */ }
				System.Environment.Exit(0);
			});
			return new
			{
				ok = true,
				message = "服务即将停止。需要再次访问时请在服务器上重新运行启动脚本。",
				action = "stop",
			};
		}

		public object ScheduleRestart()
		{
			var root = webUpdate.InstallRoot;
			var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
			var script = WriteRestartScript(root, platform);
			Log.Info("Admin requested process restart from web console: {0}", script);

			if (platform == "windows")
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "powershell",
					Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"",
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = root,
				});
			}
			else
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "/bin/sh",
					Arguments = "\"" + script + "\"",
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = root,
				});
			}

			_ = Task.Run(async () =>
			{
				await Task.Delay(800);
				System.Environment.Exit(0);
			});

			return new
			{
				ok = true,
				message = "服务即将重启，约 10 秒后刷新网页。日志：logs/console.log。",
				action = "restart",
				logFile = "logs/console.log",
			};
		}

		private static string WriteRestartScript(string installRoot, string platform)
		{
			Directory.CreateDirectory(Path.Combine(installRoot, "logs"));
			if (platform == "windows")
			{
				var script = Path.Combine(installRoot, ".service-restart.ps1");
				var content = $@"
$ErrorActionPreference = 'Stop'
Start-Sleep -Seconds 2
$dst = '{installRoot.Replace("'", "''")}'
$logDir = Join-Path $dst 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$consoleLog = Join-Path $logDir 'console.log'
$pidFile = Join-Path $dst 'ts3audiobot.pid'
$data = Join-Path $dst 'data'
New-Item -ItemType Directory -Force -Path $data | Out-Null
$exe = Join-Path $dst 'TS3AudioBot.exe'
if (Test-Path -LiteralPath $pidFile) {{
  $oldPid = (Get-Content -LiteralPath $pidFile -ErrorAction SilentlyContinue | Select-Object -First 1)
  if ($oldPid) {{ Stop-Process -Id $oldPid -Force -ErrorAction SilentlyContinue }}
}}
Get-Process -Name 'TS3AudioBot' -ErrorAction SilentlyContinue | Where-Object {{ $_.Path -and $_.Path.StartsWith($dst, [StringComparison]::OrdinalIgnoreCase) }} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
$p = Start-Process -FilePath $exe -ArgumentList '--config','ts3audiobot.toml','--non-interactive' -WorkingDirectory $data -RedirectStandardOutput $consoleLog -RedirectStandardError $consoleLog -PassThru -WindowStyle Hidden
Set-Content -LiteralPath $pidFile -Value $p.Id -Encoding ASCII
";
				File.WriteAllText(script, content, Encoding.UTF8);
				return script;
			}
			else
			{
				var script = Path.Combine(installRoot, ".service-restart.sh");
				var content = $@"#!/usr/bin/env sh
set -eu
sleep 2
dst='{installRoot.Replace("'", "'\\''")}'
mkdir -p ""$dst/logs"" ""$dst/data""
console_log=""$dst/logs/console.log""
pid_file=""$dst/ts3audiobot.pid""
if [ -f ""$pid_file"" ]; then
  oldpid=$(cat ""$pid_file"" 2>/dev/null || true)
  if [ -n ""${{oldpid:-}}"" ]; then kill ""$oldpid"" 2>/dev/null || true; sleep 1; kill -9 ""$oldpid"" 2>/dev/null || true; fi
fi
if command -v pgrep >/dev/null 2>&1; then
  pids=$(pgrep -f ""$dst/TS3AudioBot"" 2>/dev/null || true)
  if [ -n ""$pids"" ]; then kill $pids 2>/dev/null || true; sleep 1; kill -9 $pids 2>/dev/null || true; fi
fi
if [ ! -x ""$dst/TS3AudioBot"" ]; then chmod +x ""$dst/TS3AudioBot"" 2>/dev/null || true; fi
cd ""$dst/data""
nohup ""$dst/TS3AudioBot"" --config ts3audiobot.toml --non-interactive >>""$console_log"" 2>&1 &
echo $! > ""$pid_file""
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
				catch { /* ignore */ }
				return script;
			}
		}

		private static string[] ReadTailLines(string path, int lines)
		{
			// Read from end without loading huge files entirely when possible.
			try
			{
				using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				if (fs.Length == 0) return Array.Empty<string>();
				const int chunk = 8192;
				var buffer = new List<byte>();
				var pos = fs.Length;
				while (pos > 0 && CountNewlines(buffer) <= lines)
				{
					var readSize = (int)Math.Min(chunk, pos);
					pos -= readSize;
					fs.Seek(pos, SeekOrigin.Begin);
					var tmp = new byte[readSize];
					var n = fs.Read(tmp, 0, readSize);
					if (n <= 0) break;
					buffer.InsertRange(0, tmp.Take(n));
					if (buffer.Count > 1024 * 1024) break; // cap 1MB
				}
				var text = Encoding.UTF8.GetString(buffer.ToArray());
				var all = text.Replace("\r\n", "\n").Split('\n');
				if (all.Length <= lines) return all;
				return all.Skip(all.Length - lines).ToArray();
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Failed reading log tail from {0}", path);
				return new[] { "读取日志失败：" + ex.Message };
			}
		}

		private static int CountNewlines(List<byte> bytes)
		{
			var n = 0;
			for (var i = 0; i < bytes.Count; i++)
				if (bytes[i] == (byte)'\n') n++;
			return n;
		}

		private static string Rel(string root, string path)
		{
			try
			{
				var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
					+ Path.DirectorySeparatorChar;
				var fullPath = Path.GetFullPath(path);
				if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
					return fullPath.Substring(fullRoot.Length).Replace('\\', '/');
			}
			catch { /* ignore */ }
			return path.Replace('\\', '/');
		}
	}
}
