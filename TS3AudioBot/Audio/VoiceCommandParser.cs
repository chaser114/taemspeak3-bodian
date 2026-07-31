// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License version 3.0.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TS3AudioBot.Audio
{
	public enum VoiceCommandKind
	{
		Pause,
		Resume,
		Next,
		PlaySong,
	}

	public enum VoiceWakeWordMatchKind
	{
		None,
		Partial,
		Confirmed,
	}

	public sealed class VoiceCommand
	{
		public VoiceCommandKind Kind { get; }
		public string? Argument { get; }
		public IReadOnlyList<string> SearchQueries { get; }

		public VoiceCommand(VoiceCommandKind kind, string? argument = null, IEnumerable<string>? searchQueries = null)
		{
			Kind = kind;
			Argument = argument;
			var queries = new List<string>();
			if (!string.IsNullOrWhiteSpace(argument))
				queries.Add(argument);
			if (searchQueries != null)
			{
				foreach (var query in searchQueries)
					if (!string.IsNullOrWhiteSpace(query) && !queries.Contains(query, StringComparer.Ordinal))
						queries.Add(query);
			}
			SearchQueries = queries;
		}
	}

	public static class VoiceCommandParser
	{
		private static readonly string[] PausePrefixes = { "暂停", "停止播放" };
		private static readonly string[] ResumePrefixes = { "继续", "恢复播放" };
		private static readonly string[] NextPrefixes = { "下一首", "下一曲", "切歌" };
		private static readonly string[] PlayPrefixes = { "播放", "点歌", "来一首" };

		public static bool TryParse(string recognizedText, string wakeWord, bool wakeAlreadyMatched, out VoiceCommand command)
		{
			command = null!;
			var text = Normalize(recognizedText);
			var wake = Normalize(wakeWord);
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(wake))
				return false;

			if (!wakeAlreadyMatched)
			{
				if (!TryConsumeFuzzyPrefix(text, wake, out var wakeLength))
					return false;
				text = text.Substring(wakeLength);
			}
			else if (TryConsumeFuzzyPrefix(text, wake, out var wakeLength))
			{
				// Some recognizers include the wake phrase in the command result,
				// while others only return the words spoken after it.
				text = text.Substring(wakeLength);
			}

			if (text == "停" || TryConsumeAnyPrefix(text, PausePrefixes, out _))
			{
				command = new VoiceCommand(VoiceCommandKind.Pause);
				return true;
			}

			if (TryConsumeAnyPrefix(text, ResumePrefixes, out _))
			{
				command = new VoiceCommand(VoiceCommandKind.Resume);
				return true;
			}

			if (TryConsumeAnyPrefix(text, NextPrefixes, out _))
			{
				command = new VoiceCommand(VoiceCommandKind.Next);
				return true;
			}

			foreach (var prefix in PlayPrefixes)
			{
				if (!TryConsumeFuzzyPrefix(text, prefix, out var prefixLength))
					continue;

				var query = text.Substring(prefixLength).Trim();
				if (query.Length == 0)
					return false;

				command = new VoiceCommand(VoiceCommandKind.PlaySong, query);
				return true;
			}

			return false;
		}

		public static bool TryParseCandidates(IEnumerable<string> recognizedTexts, string wakeWord, bool wakeAlreadyMatched, out VoiceCommand command)
		{
			command = null!;
			var songQueries = new List<string>();
			foreach (var recognizedText in recognizedTexts ?? Array.Empty<string>())
			{
				if (!TryParse(recognizedText, wakeWord, wakeAlreadyMatched, out var candidate))
					continue;

				if (candidate.Kind != VoiceCommandKind.PlaySong)
				{
					command = candidate;
					return true;
				}

				foreach (var query in candidate.SearchQueries)
					if (!songQueries.Contains(query, StringComparer.Ordinal))
						songQueries.Add(query);
			}

			if (songQueries.Count == 0)
				return false;

			command = new VoiceCommand(VoiceCommandKind.PlaySong, songQueries[0], songQueries);
			return true;
		}

		public static VoiceWakeWordMatchKind MatchWakeWord(string recognizedText, string wakeWord)
		{
			var text = Normalize(recognizedText);
			var wake = Normalize(wakeWord);
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(wake))
				return VoiceWakeWordMatchKind.None;

			if (text.Contains(wake, StringComparison.Ordinal)
				|| ContainsNearMatch(text, wake, 1))
				return VoiceWakeWordMatchKind.Confirmed;

			// A partial match is deliberately limited to the beginning of the
			// configured wake word. This prevents common suffixes such as
			// "机器人" from arming voice control on their own.
			if (wake.Length >= 3)
			{
				var longestPrefix = Math.Min(4, wake.Length);
				for (var prefixLength = 3; prefixLength <= longestPrefix; prefixLength++)
				{
					var prefix = wake.Substring(0, prefixLength);
					if (ContainsNearMatch(text, prefix, 1, 3))
						return VoiceWakeWordMatchKind.Partial;
				}
			}

			return VoiceWakeWordMatchKind.None;
		}

		private static bool TryConsumeAnyPrefix(string text, string[] prefixes, out int consumedLength)
		{
			foreach (var prefix in prefixes)
			{
				if (TryConsumeFuzzyPrefix(text, prefix, out consumedLength))
					return true;
			}

			consumedLength = 0;
			return false;
		}

		private static bool TryConsumeFuzzyPrefix(string text, string pattern, out int consumedLength)
		{
			if (text.StartsWith(pattern, StringComparison.Ordinal))
			{
				consumedLength = pattern.Length;
				return true;
			}

			consumedLength = 0;
			if (pattern.Length < 2)
				return false;

			var candidateLengths = new[] { pattern.Length, pattern.Length - 1, pattern.Length + 1 };
			foreach (var candidateLength in candidateLengths)
			{
				if (candidateLength < 2 || candidateLength > text.Length)
					continue;

				if (IsWithinEditDistance(text.Substring(0, candidateLength), pattern, 1))
				{
					consumedLength = candidateLength;
					return true;
				}
			}

			return false;
		}

		private static bool ContainsNearMatch(string text, string pattern, int maxDistance, int minimumWindowLength = 1)
		{
			var minimumWindow = Math.Max(minimumWindowLength, pattern.Length - maxDistance);
			var maximumWindow = Math.Min(text.Length, pattern.Length + maxDistance);
			if (minimumWindow > maximumWindow)
				return false;

			for (var start = 0; start < text.Length; start++)
			{
				for (var length = minimumWindow; length <= maximumWindow && start + length <= text.Length; length++)
				{
					if (IsWithinEditDistance(text.Substring(start, length), pattern, maxDistance))
						return true;
				}
			}

			return false;
		}

		private static bool IsWithinEditDistance(string left, string right, int maxDistance)
		{
			if (Math.Abs(left.Length - right.Length) > maxDistance)
				return false;

			var previous = new int[right.Length + 1];
			for (var j = 0; j <= right.Length; j++)
				previous[j] = j;

			for (var i = 1; i <= left.Length; i++)
			{
				var current = new int[right.Length + 1];
				current[0] = i;
				for (var j = 1; j <= right.Length; j++)
				{
					var substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
					current[j] = Math.Min(
						Math.Min(current[j - 1] + 1, previous[j] + 1),
						previous[j - 1] + substitutionCost);
				}
				previous = current;
			}

			return previous[right.Length] <= maxDistance;
		}

		public static string Normalize(string value)
			=> new string((value ?? string.Empty)
				.Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c))
				.ToArray())
			.ToLowerInvariant();
	}

	internal sealed class VoiceCommandStability
	{
		private VoiceCommandKind? lastKind;
		private int stableCount;

		public bool TryCommitControl(string recognizedText, string wakeWord, out VoiceCommand command)
		{
			command = null!;
			if (!VoiceCommandParser.TryParse(recognizedText, wakeWord, true, out var candidate)
				|| candidate.Kind == VoiceCommandKind.PlaySong)
			{
				Reset();
				return false;
			}

			if (lastKind == candidate.Kind)
				stableCount++;
			else
			{
				lastKind = candidate.Kind;
				stableCount = 1;
			}

			if (stableCount < 2)
				return false;

			command = candidate;
			Reset();
			return true;
		}

		public void Reset()
		{
			lastKind = null;
			stableCount = 0;
		}
	}

	internal sealed class VoiceWakeStability
	{
		private int partialMatches;

		public bool Confirm(string recognizedText, string wakeWord)
		{
			var match = VoiceCommandParser.MatchWakeWord(recognizedText, wakeWord);
			if (match == VoiceWakeWordMatchKind.Confirmed)
			{
				Reset();
				return true;
			}

			if (match != VoiceWakeWordMatchKind.Partial)
			{
				Reset();
				return false;
			}

			partialMatches++;
			if (partialMatches < 2)
				return false;

			Reset();
			return true;
		}

		public void Reset() => partialMatches = 0;
	}
}
