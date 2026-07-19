using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TS3AudioBot.Audio;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
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

		public WebConsoleService(BotManager botManager, ConfRoot rootConfig)
		{
			this.botManager = botManager;
			this.rootConfig = rootConfig;
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
