using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot;
using TS3AudioBot.Helper;
using TS3AudioBot.ResourceFactories;

namespace KuwoMusicPlugin
{
	// TS3AudioBot loads a single IResolver implementation from this DLL.
	// Search / play / lyrics all go through the same 波点 API (二合一).
	public sealed class KuwoMusicPlugin : IResourceResolver, ISearchResolver, IThumbnailResolver
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		// Single music API: list search without n; detail (audio+lrc) with n.
		private const string ApiUrl = "https://api.xcvts.cn/api/music/bdyy";
		private const string AudioUrlKey = "audio_url";
		private const string CoverUrlKey = "cover_url";
		private const string QueryKey = "query";
		private const string SelectionKey = "selection";
		private const string LyricsKey = "lrc";
		private const string ResolvedAtKey = "resolved_at";
		private static readonly TimeSpan AudioUrlLifetime = TimeSpan.FromHours(12);
		private static readonly SemaphoreSlim ApiConcurrency = new SemaphoreSlim(2, 2);
		private static readonly Regex DetailIdRegex = new Regex(@"play_detail/(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public string ResolverFor => "kuwo";

		public MatchCertainty MatchResource(ResolveContext _, string __) => MatchCertainty.Never;

		public async Task<PlayResource> GetResource(ResolveContext ctx, string query)
		{
			var results = await Search(ctx, query);
			if (results.Count == 0)
				throw new InvalidOperationException("No Kuwo search result was returned.");
			return await GetResourceById(ctx, results[0]);
		}

		public async Task<PlayResource> GetResourceById(ResolveContext _, AudioResource resource)
		{
			if (!string.Equals(resource.AudioType, ResolverFor, StringComparison.OrdinalIgnoreCase)
				|| !long.TryParse(resource.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var resourceId)
				|| resourceId <= 0)
				throw new InvalidOperationException("The Kuwo resource is invalid.");

			// Never trust audio_url / lrc from the client. Re-resolve through API for playback.
			var query = resource.Get(QueryKey);
			var selection = resource.Get(SelectionKey);
			if (string.IsNullOrWhiteSpace(query)
				|| !int.TryParse(selection, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
				|| number < 1
				|| number > 20)
				throw new InvalidOperationException("The Kuwo resource is missing server-side search information.");

			var song = await RequestSongDetail(query, number);
			if (song is null)
				throw new InvalidOperationException("The Kuwo search result is no longer available.");

			var resolvedId = ExtractSongId(song.detail_page);
			if (resolvedId != 0 && resolvedId != resourceId)
				throw new InvalidOperationException("The Kuwo search result is no longer available.");

			var resolved = ToResource(song, query, number, resourceId);
			var audioUrl = resolved.Get(AudioUrlKey);
			if (string.IsNullOrWhiteSpace(audioUrl))
				throw new InvalidOperationException("The Kuwo resource does not contain a playable song.");
			return new PlayResource(audioUrl!, resolved);
		}

		public string RestoreLink(ResolveContext _, AudioResource resource) => resource.Get(AudioUrlKey) ?? resource.ResourceId;

		public async Task<IList<AudioResource>> Search(ResolveContext _, string keyword)
		{
			if (string.IsNullOrWhiteSpace(keyword))
				throw new InvalidOperationException("A search query is required.");

			var q = keyword.Trim();
			// List call only (no n). Playback resolves play_url + lrc with n later.
			var list = await RequestSearchList(q, 10);
			var resources = new List<AudioResource>();
			for (var i = 0; i < list.Count; i++)
			{
				var item = list[i];
				var id = ExtractSongId(item.detail_page);
				if (id <= 0 || string.IsNullOrWhiteSpace(item.name)) continue;
				var number = i + 1;
				var title = string.IsNullOrWhiteSpace(item.artist) ? item.name! : $"{item.name} - {item.artist}";
				resources.Add(new AudioResource(id.ToString(CultureInfo.InvariantCulture), title, "kuwo")
					.Add(CoverUrlKey, item.pic ?? string.Empty)
					.Add(QueryKey, q)
					.Add(SelectionKey, number.ToString(CultureInfo.InvariantCulture)));
			}
			return resources;
		}

		public async Task GetThumbnail(ResolveContext _, PlayResource playResource, Func<Stream, Task> action)
		{
			var coverUrl = playResource.AudioResource.Get(CoverUrlKey);
			if (string.IsNullOrWhiteSpace(coverUrl))
				throw new InvalidOperationException("The Kuwo resource does not contain a cover URL.");
			await WebWrapper.Request(coverUrl).ToStream(action);
		}

		/// <summary>Return cached LRC from the last detail resolve, if present.</summary>
		public static string? GetCachedLyrics(AudioResource resource)
			=> resource?.Get(LyricsKey);

		private static AudioResource ToResource(BdyyDetail song, string query, int selection, long fallbackId = 0)
		{
			var audioUrl = song.play_url;
			var id = ExtractSongId(song.detail_page);
			if (id <= 0) id = fallbackId;
			if (id <= 0 || string.IsNullOrWhiteSpace(song.name) || string.IsNullOrWhiteSpace(audioUrl))
				throw new InvalidOperationException("The Kuwo API response does not contain a playable song.");

			var title = string.IsNullOrWhiteSpace(song.artist) ? song.name : $"{song.name} - {song.artist}";
			var resource = new AudioResource(id.ToString(CultureInfo.InvariantCulture), title, "kuwo")
				.Add(AudioUrlKey, audioUrl)
				.Add(CoverUrlKey, song.cover ?? string.Empty)
				.Add(QueryKey, query)
				.Add(SelectionKey, selection.ToString(CultureInfo.InvariantCulture))
				.Add(ResolvedAtKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
			if (!string.IsNullOrWhiteSpace(song.lrc))
				resource.Add(LyricsKey, song.lrc);
			return resource;
		}

		private static long ExtractSongId(string? detailPage)
		{
			if (string.IsNullOrWhiteSpace(detailPage)) return 0;
			var match = DetailIdRegex.Match(detailPage);
			if (!match.Success) return 0;
			return long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0;
		}

		private static async Task<IList<BdyyListItem>> RequestSearchList(string keyword, int count)
		{
			await ApiConcurrency.WaitAsync();
			try
			{
				// Without n: returns a list. sc controls count.
				var url = $"{ApiUrl}?msg={Uri.EscapeDataString(keyword)}&sc={count.ToString(CultureInfo.InvariantCulture)}&type=json";
				var response = await WebWrapper.Request(url).AsJson<BdyyListResponse>();
				if (response is null || response.code != 200 || response.data is null)
					return Array.Empty<BdyyListItem>();
				return response.data.Where(x => x != null && !string.IsNullOrWhiteSpace(x!.name)).Select(x => x!).ToList();
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Kuwo list search failed for {0}", keyword);
				return Array.Empty<BdyyListItem>();
			}
			finally
			{
				ApiConcurrency.Release();
			}
		}

		private static async Task<BdyyDetail?> RequestSongDetail(string keyword, int number)
		{
			await ApiConcurrency.WaitAsync();
			try
			{
				// With n: returns play_url + lrc together (二合一).
				var url = $"{ApiUrl}?msg={Uri.EscapeDataString(keyword)}&n={number.ToString(CultureInfo.InvariantCulture)}&type=json";
				var response = await WebWrapper.Request(url).AsJson<BdyyDetailResponse>();
				if (response is null || response.code != 200)
					return null;
				return response.data;
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Kuwo detail request failed for {0} n={1}", keyword, number);
				return null;
			}
			finally
			{
				ApiConcurrency.Release();
			}
		}

		public void Dispose() { }

		private sealed class BdyyListResponse
		{
			public int code { get; set; }
			public List<BdyyListItem?>? data { get; set; }
		}

		private sealed class BdyyListItem
		{
			public string? name { get; set; }
			public string? artist { get; set; }
			public string? detail_page { get; set; }
			public string? pic { get; set; }
		}

		private sealed class BdyyDetailResponse
		{
			public int code { get; set; }
			public BdyyDetail? data { get; set; }
		}

		private sealed class BdyyDetail
		{
			public string? name { get; set; }
			public string? artist { get; set; }
			public string? cover { get; set; }
			public string? detail_page { get; set; }
			public string? play_url { get; set; }
			public string? lrc { get; set; }
		}
	}
}
