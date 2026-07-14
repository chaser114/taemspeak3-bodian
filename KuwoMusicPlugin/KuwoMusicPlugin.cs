using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TS3AudioBot;
using TS3AudioBot.Helper;
using TS3AudioBot.ResourceFactories;

namespace KuwoMusicPlugin
{
	// TS3AudioBot loads a single IResolver implementation from this DLL.
	public sealed class KuwoMusicPlugin : IResourceResolver, ISearchResolver, IThumbnailResolver
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private const string ApiUrl = "https://api.xingzhige.com/API/Kuwo_BD_new/";
		private const string AudioUrlKey = "audio_url";
		private const string CoverUrlKey = "cover_url";
		private const string QueryKey = "query";
		private const string SelectionKey = "selection";
		private const string ResolvedAtKey = "resolved_at";
		private static readonly TimeSpan AudioUrlLifetime = TimeSpan.FromHours(12);

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
			var audioUrl = resource.Get(AudioUrlKey);
			if (ShouldRefreshAudioUrl(resource, audioUrl))
			{
				var query = resource.Get(QueryKey);
				var selection = resource.Get(SelectionKey);
				if (string.IsNullOrWhiteSpace(query) || !int.TryParse(selection, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
					throw new InvalidOperationException("The Kuwo resource does not contain an audio URL.");

				var song = await RequestSong(query, number);
				if (song is null)
					throw new InvalidOperationException("No Kuwo search result was returned.");
				resource = ToResource(song, query, number);
				audioUrl = resource.Get(AudioUrlKey);
			}

			if (string.IsNullOrWhiteSpace(audioUrl))
				throw new InvalidOperationException("The Kuwo resource does not contain a playable song.");
			return new PlayResource(audioUrl!, resource);
		}

		public string RestoreLink(ResolveContext _, AudioResource resource) => resource.Get(AudioUrlKey) ?? resource.ResourceId;

		public async Task<IList<AudioResource>> Search(ResolveContext _, string keyword)
		{
			if (string.IsNullOrWhiteSpace(keyword))
				throw new InvalidOperationException("A search query is required.");

			var requests = Enumerable.Range(1, 10).Select(number => RequestResource(keyword, number));
			var resources = await Task.WhenAll(requests);
			return resources.Where(resource => resource != null)
				.Select(resource => resource!)
				.ToList();
		}

		public async Task GetThumbnail(ResolveContext _, PlayResource playResource, Func<Stream, Task> action)
		{
			var coverUrl = playResource.AudioResource.Get(CoverUrlKey);
			if (string.IsNullOrWhiteSpace(coverUrl))
				throw new InvalidOperationException("The Kuwo resource does not contain a cover URL.");
			await WebWrapper.Request(coverUrl).ToStream(action);
		}

		private static AudioResource ToResource(KuwoSong song, string query, int selection)
		{
			var audioUrl = song.raw?.audioHttpsUrl ?? song.src;
			if (song.id == 0 || string.IsNullOrWhiteSpace(song.songname) || string.IsNullOrWhiteSpace(audioUrl))
				throw new InvalidOperationException("The Kuwo API response does not contain a playable song.");

			var title = string.IsNullOrWhiteSpace(song.name) ? song.songname : $"{song.songname} - {song.name}";
			return new AudioResource(song.id.ToString(CultureInfo.InvariantCulture), title, "kuwo")
				.Add(AudioUrlKey, audioUrl)
				.Add(CoverUrlKey, song.cover ?? string.Empty)
				.Add(QueryKey, query)
				.Add(SelectionKey, selection.ToString(CultureInfo.InvariantCulture))
				.Add(ResolvedAtKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
		}

		private static async Task<KuwoSong?> RequestSong(string keyword, int number)
		{
			var url = $"{ApiUrl}?name={Uri.EscapeDataString(keyword)}&n={number.ToString(CultureInfo.InvariantCulture)}";
			var response = await WebWrapper.Request(url).AsJson<KuwoResponse>();
			return response.code == 0 ? response.data : null;
		}

		private static bool ShouldRefreshAudioUrl(AudioResource resource, string? audioUrl)
		{
			if (string.IsNullOrWhiteSpace(audioUrl))
				return true;
			if (!long.TryParse(resource.Get(ResolvedAtKey), NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
				return true;
			return DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(timestamp) >= AudioUrlLifetime;
		}

		private static async Task<AudioResource?> RequestResource(string keyword, int number)
		{
			try
			{
				var song = await RequestSong(keyword, number);
				return song is null ? null : ToResource(song, keyword, number);
			}
			catch (AudioBotException ex)
			{
				Log.Debug(ex, "Kuwo search result {0} could not be loaded.", number);
				return null;
			}
			catch (InvalidOperationException ex)
			{
				Log.Debug(ex, "Kuwo search result {0} was invalid.", number);
				return null;
			}
		}

		public void Dispose() { }

		private sealed class KuwoResponse
		{
			public int code { get; set; }
			public KuwoSong? data { get; set; }
		}

		private sealed class KuwoSong
		{
			public long id { get; set; }
			public string? songname { get; set; }
			public string? name { get; set; }
			public string? cover { get; set; }
			public string? src { get; set; }
			public KuwoRaw? raw { get; set; }
		}

		private sealed class KuwoRaw
		{
			public string? audioHttpsUrl { get; set; }
		}
	}
}
