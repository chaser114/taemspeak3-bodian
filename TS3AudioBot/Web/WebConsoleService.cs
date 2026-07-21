using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TS3AudioBot.Audio;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Helper;
using TS3AudioBot.History;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories;

namespace TS3AudioBot.Web
{
	/// <summary>Cookie-authenticated web console adapter. It is deliberately the only owner that crosses into a bot scheduler.</summary>
	public sealed class WebConsoleService
	{
		private readonly BotManager botManager;
		private readonly ConfRoot rootConfig;
		private readonly WebAccountService webAccounts;

		public WebConsoleService(BotManager botManager, ConfRoot rootConfig, WebAccountService webAccounts)
		{
			this.botManager = botManager;
			this.rootConfig = rootConfig;
			this.webAccounts = webAccounts;
		}

		public async Task<object> GetState(string? botId = null)
		{
			var bot = GetBot(botId);
			if (bot is null) return new { configured = rootConfig.GetAllBots()?.Length > 0, connected = false, current = (object?)null, queue = Array.Empty<object>(), recent = Array.Empty<object>() };
			var connected = bot.GetInfo().Status == BotStatus.Connected;
			return await bot.Scheduler.InvokeAsync<object>(() =>
			{
				var playManager = bot.Injector.GetModuleOrThrow<PlayManager>();
				var player = bot.Injector.GetModuleOrThrow<Player>();
				var playlist = bot.Injector.GetModuleOrThrow<PlaylistManager>();
				var current = playManager.CurrentPlayData;
				var queue = playlist.CurrentList.Items.Select((item, index) => Track(item.AudioResource, index == playlist.Index)).ToArray();
				var history = bot.Injector.TryGet<HistoryManager>(out var historyManager)
					? historyManager.Search(new SeachQuery { MaxResults = 8 }).Select(x => Track(x.AudioResource, false)).ToArray()
					: Array.Empty<object>();
				return Task.FromResult<object>(new { configured = true, connected, current = current is null ? null : Track(current.ResourceData, true), paused = player.Paused, position = player.Position?.TotalSeconds ?? 0, length = player.Length?.TotalSeconds ?? 0, queue, recent = history });
			});
		}

		public Task<IList<AudioResource>> Search(string query, string? botId = null)
		{
			if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("请输入歌曲、歌手或专辑。");
			var bot = RequireBot(botId);
			return bot.Scheduler.InvokeAsync(() => bot.Injector.GetModuleOrThrow<ResolveContext>().Search("kuwo", query.Trim()));
		}

		public Task Play(string username, AudioResource resource, string? botId = null)
			=> OnBot(username, botId, (playManager, player, playlist) => playManager.Play(WebInvoker(username), resource));

		public Task Add(string username, AudioResource resource, string? botId = null)
			=> OnBot(username, botId, (playManager, player, playlist) => playManager.Enqueue(WebInvoker(username), resource));

		public Task Next(string username, string? botId = null)
			=> OnBot(username, botId, (playManager, player, playlist) => playManager.Next(WebInvoker(username)));

		public Task Previous(string username, string? botId = null)
			=> OnBot(username, botId, (playManager, player, playlist) => playManager.Previous(WebInvoker(username)));

		public Task TogglePause(string? botId = null)
			=> OnBot(string.Empty, botId, (playManager, player, playlist) =>
			{
				if (playManager.CurrentPlayData is null) return Task.CompletedTask;
				player.Paused = !player.Paused;
				return Task.CompletedTask;
			});

		public Task Clear(string? botId = null)
			=> OnBot(string.Empty, botId, (playManager, player, playlist) => { playManager.ClearQueue(); return Task.CompletedTask; });

		public async Task<object> GetLyrics(string? botId = null)
		{
			var bot = GetBot(botId);
			if (bot is null)
				return new { available = false, lrc = (string?)null, lines = Array.Empty<object>(), title = (string?)null };

			var resource = await bot.Scheduler.InvokeAsync(() =>
			{
				var playManager = bot.Injector.GetModuleOrThrow<PlayManager>();
				return Task.FromResult(playManager.CurrentPlayData?.ResourceData);
			});
			if (resource is null)
				return new { available = false, lrc = (string?)null, lines = Array.Empty<object>(), title = (string?)null };

			// Prefer LRC cached on the resource from the same detail API call (play_url + lrc).
			var lrc = resource.Get("lrc");
			if (string.IsNullOrWhiteSpace(lrc))
			{
				try { lrc = await FetchBdyyLyricsAsync(resource); }
				catch { lrc = null; }
			}

			var lines = ParseLrc(lrc);
			return new
			{
				available = lines.Count > 0,
				lrc,
				title = resource.ResourceTitle,
				resid = resource.ResourceId,
				lines = lines.Select(x => new { time = x.time, text = x.text }).ToArray(),
			};
		}

		// Same 波点 API used by the plugin; detail call returns lrc with play_url.
		private const string LyricsApiUrl = "https://api.xcvts.cn/api/music/bdyy";

		private static async Task<string?> FetchBdyyLyricsAsync(AudioResource resource)
		{
			var query = resource.Get("query");
			var selectionText = resource.Get("selection");
			if (string.IsNullOrWhiteSpace(query))
			{
				query = resource.ResourceTitle;
				if (string.IsNullOrWhiteSpace(query)) return null;
				var dash = query.IndexOf(" - ", StringComparison.Ordinal);
				if (dash > 0) query = query.Substring(0, dash);
			}
			if (!int.TryParse(selectionText, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
				number = 1;

			var url = $"{LyricsApiUrl}?msg={Uri.EscapeDataString(query.Trim())}&n={number.ToString(CultureInfo.InvariantCulture)}&type=json";
			var response = await WebWrapper.Request(url).AsJson<BdyyLyricsResponse>();
			if (response is null || response.code != 200 || response.data is null)
				return null;
			return string.IsNullOrWhiteSpace(response.data.lrc) ? null : response.data.lrc;
		}

		private static List<(double time, string text)> ParseLrc(string? lrc)
		{
			var result = new List<(double time, string text)>();
			if (string.IsNullOrWhiteSpace(lrc)) return result;
			var normalized = lrc.Replace("\r\n", "\n").Replace('\r', '\n');
			foreach (var raw in normalized.Split('\n'))
			{
				var line = raw.Trim();
				if (line.Length == 0) continue;
				var matches = System.Text.RegularExpressions.Regex.Matches(line, @"\[(\d{1,2}):(\d{1,2})(?:[.:](\d{1,3}))?\]");
				if (matches.Count == 0) continue;
				var textStart = matches[matches.Count - 1].Index + matches[matches.Count - 1].Length;
				var text = textStart < line.Length ? line.Substring(textStart).Trim() : string.Empty;
				if (string.IsNullOrWhiteSpace(text)) continue;
				foreach (System.Text.RegularExpressions.Match m in matches)
				{
					if (!int.TryParse(m.Groups[1].Value, out var min)) continue;
					if (!int.TryParse(m.Groups[2].Value, out var sec)) continue;
					var frac = 0.0;
					if (m.Groups[3].Success)
					{
						var ms = m.Groups[3].Value;
						if (ms.Length == 1) ms += "00";
						else if (ms.Length == 2) ms += "0";
						if (ms.Length > 3) ms = ms.Substring(0, 3);
						if (int.TryParse(ms, out var milli)) frac = milli / 1000.0;
					}
					result.Add((min * 60 + sec + frac, text));
				}
			}
			return result.OrderBy(x => x.time).ToList();
		}

		private sealed class BdyyLyricsResponse
		{
			public int code { get; set; }
			public BdyyLyricsData? data { get; set; }
		}

		private sealed class BdyyLyricsData
		{
			public string? lrc { get; set; }
		}

		/// <summary>
		/// Admin-only health check: whether bots can write their TeamSpeak description.
		/// Respects "暂不理会" dismiss flag stored in web settings.
		/// </summary>
		public async Task<object> GetDescriptionPermissionStatus(bool forceProbe = false)
		{
			if (webAccounts.IsDescriptionPermissionDismissed)
			{
				return new
				{
					needsAttention = false,
					dismissed = true,
					bots = Array.Empty<object>(),
					message = (string?)null,
				};
			}

			var infos = botManager.GetBotInfolist().Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToArray();
			var report = new List<object>();
			var missing = new List<string>();

			foreach (var info in infos)
			{
				if (!(info.Id is int id)) continue;
				var bot = botManager.GetBotLock(id);
				if (bot is null) continue;

				// Prefer human labels: TS nickname + server address, not internal config id.
				var displayName = ResolveBotDisplayName(bot.Name, info.Name);
				var server = ResolveBotServer(bot.Name, info.Server);
				var label = string.IsNullOrWhiteSpace(server) ? displayName : $"{displayName}（{server}）";

				if (info.Status != BotStatus.Connected)
				{
					report.Add(new
					{
						id = bot.Name,
						name = displayName,
						server,
						label,
						connected = false,
						canSetDescription = (bool?)null,
					});
					continue;
				}

				bool ok;
				if (forceProbe || bot.DescriptionPermissionOk is null)
					ok = await bot.ProbeDescriptionPermissionAsync();
				else
					ok = bot.DescriptionPermissionOk.Value;

				report.Add(new
				{
					id = bot.Name,
					name = displayName,
					server,
					label,
					connected = true,
					canSetDescription = ok,
				});
				if (!ok) missing.Add(label);
			}

			var needsAttention = missing.Count > 0;
			string? message = null;
			if (needsAttention)
			{
				var names = string.Join("、", missing);
				message = $"以下机器人没有修改简介权限：{names}。请在 TeamSpeak「权限 → 服务器组」给机器人所在组勾选“修改自己的简介 / 修改客户端简介”，或加入管理员组，否则播放时简介无法显示歌名。";
			}

			return new
			{
				needsAttention,
				dismissed = false,
				bots = report,
				message,
			};
		}

		public object DismissDescriptionPermissionNotice()
		{
			webAccounts.SetDescriptionPermissionDismissed(true);
			return new { ok = true, dismissed = true };
		}

		public object ResetDescriptionPermissionNotice()
		{
			webAccounts.SetDescriptionPermissionDismissed(false);
			return new { ok = true, dismissed = false };
		}

		private Task OnBot(string username, string? botId, Func<PlayManager, Player, PlaylistManager, Task> operation)
		{
			var bot = RequireBot(botId);
			return bot.Scheduler.InvokeAsync(() => operation(bot.Injector.GetModuleOrThrow<PlayManager>(), bot.Injector.GetModuleOrThrow<Player>(), bot.Injector.GetModuleOrThrow<PlaylistManager>()));
		}

		private Bot RequireBot(string? botId) => GetBot(botId) ?? throw new InvalidOperationException("还没有可用的机器人，请先由管理员完成 TeamSpeak 连接配置。");
		private Bot? GetBot(string? botId)
		{
			var infos = botManager.GetBotInfolist();
			var info = string.IsNullOrWhiteSpace(botId)
				? infos.FirstOrDefault()
				: infos.FirstOrDefault(x => string.Equals(x.Name, botId, StringComparison.Ordinal));
			return info?.Id is int id ? botManager.GetBotLock(id) : null;
		}

		private string ResolveBotDisplayName(string? configId, string? liveName)
		{
			if (!string.IsNullOrWhiteSpace(liveName)) return liveName.Trim();
			if (!string.IsNullOrWhiteSpace(configId))
			{
				var conf = rootConfig.GetBotConfig(configId);
				if (conf.Ok)
				{
					var nick = conf.Value.Connect.Name.Value;
					if (!string.IsNullOrWhiteSpace(nick)) return nick.Trim();
				}
				return configId!;
			}
			return "未命名机器人";
		}

		private string? ResolveBotServer(string? configId, string? liveServer)
		{
			if (!string.IsNullOrWhiteSpace(liveServer)) return liveServer.Trim();
			if (string.IsNullOrWhiteSpace(configId)) return null;
			var conf = rootConfig.GetBotConfig(configId);
			if (!conf.Ok) return null;
			var address = conf.Value.Connect.Address.Value;
			return string.IsNullOrWhiteSpace(address) ? null : address.Trim();
		}
		private static InvokerData WebInvoker(string username)
			=> string.IsNullOrWhiteSpace(username) ? InvokerData.Anonymous : new InvokerData(TSLib.Uid.To("web:" + username));
		private static object Track(AudioResource resource, bool active) => new
		{
			resource,
			title = resource.ResourceTitle ?? "未命名歌曲",
			type = resource.AudioType,
			coverUrl = resource.Get("cover_url"),
			active,
		};
	}
}
