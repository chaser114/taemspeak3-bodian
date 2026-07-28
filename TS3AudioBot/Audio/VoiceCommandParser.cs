// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License version 3.0.

using System;
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

	public sealed class VoiceCommand
	{
		public VoiceCommandKind Kind { get; }
		public string? Argument { get; }

		public VoiceCommand(VoiceCommandKind kind, string? argument = null)
		{
			Kind = kind;
			Argument = argument;
		}
	}

	public static class VoiceCommandParser
	{
		public static bool TryParse(string recognizedText, string wakeWord, bool wakeAlreadyMatched, out VoiceCommand command)
		{
			command = null!;
			var text = Normalize(recognizedText);
			var wake = Normalize(wakeWord);
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(wake))
				return false;

			if (!wakeAlreadyMatched)
			{
				if (!text.StartsWith(wake, StringComparison.Ordinal))
					return false;
				text = text.Substring(wake.Length);
			}
			else if (text.StartsWith(wake, StringComparison.Ordinal))
			{
				// Some recognizers include the wake phrase in the command result,
				// while others only return the words spoken after it.
				text = text.Substring(wake.Length);
			}

			if (text.StartsWith("暂停", StringComparison.Ordinal)
				|| text.StartsWith("停止播放", StringComparison.Ordinal)
				|| text == "停")
			{
				command = new VoiceCommand(VoiceCommandKind.Pause);
				return true;
			}

			if (text.StartsWith("继续", StringComparison.Ordinal)
				|| text.StartsWith("恢复播放", StringComparison.Ordinal))
			{
				command = new VoiceCommand(VoiceCommandKind.Resume);
				return true;
			}

			if (text.StartsWith("下一首", StringComparison.Ordinal)
				|| text.StartsWith("下一曲", StringComparison.Ordinal)
				|| text.StartsWith("切歌", StringComparison.Ordinal))
			{
				command = new VoiceCommand(VoiceCommandKind.Next);
				return true;
			}

			foreach (var prefix in new[] { "播放", "点歌", "来一首" })
			{
				if (!text.StartsWith(prefix, StringComparison.Ordinal))
					continue;

				var query = text.Substring(prefix.Length).Trim();
				if (query.Length == 0)
					return false;

				command = new VoiceCommand(VoiceCommandKind.PlaySong, query);
				return true;
			}

			return false;
		}

		public static string Normalize(string value)
			=> new string((value ?? string.Empty)
				.Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c))
				.ToArray())
			.ToLowerInvariant();
	}
}
