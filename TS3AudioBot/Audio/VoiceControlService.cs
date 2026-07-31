// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License version 3.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TS3AudioBot.Config;
using TSLib;
using TSLib.Audio;
using TSLib.Audio.Opus;
using TSLib.Full;
using TSLib.Scheduler;
using TS3AudioBot.ResourceFactories;
using Vosk;

namespace TS3AudioBot.Audio
{
	/// <summary>
	/// Receives TeamSpeak voice packets without doing recognition on the packet or playback threads.
	/// The feature is one global switch: when enabled, wake-word and command recognition are both available.
	/// </summary>
	public sealed class VoiceControlService : IAudioPassiveConsumer, IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly TimeSpan CommandWindow = TimeSpan.FromSeconds(4);
		private static readonly TimeSpan SpeakerExpiry = TimeSpan.FromMinutes(2);
		private const int QueueCapacity = 256;
		private const int SampleRate = 16_000;
		private const int DecoderBufferSize = 8192;

		private readonly ConfVoice config;
		private readonly TsFullClient client;
		private readonly PlayManager playManager;
		private readonly ResolveContext resourceResolver;
		private readonly Player player;
		private readonly DedicatedTaskScheduler scheduler;
		private readonly AudioPacketReader packetReader;
		private readonly object lifecycleLock = new object();

		private BlockingCollection<VoiceFrame>? queue;
		private CancellationTokenSource? cancellation;
		private Thread? worker;
		private Model? model;
		private bool running;
		private bool disposed;

		public bool Active => running;

		public VoiceControlService(ConfVoice config, TsFullClient client, PlayManager playManager, ResolveContext resourceResolver, Player player, DedicatedTaskScheduler scheduler)
		{
			this.config = config;
			this.client = client;
			this.playManager = playManager;
			this.resourceResolver = resourceResolver;
			this.player = player;
			this.scheduler = scheduler;
			packetReader = new AudioPacketReader { OutStream = this };

			config.Enabled.Changed += OnEnabledChanged;
			config.WakeWord.Changed += OnWakeWordChanged;

			if (config.Enabled.Value)
				Start();
		}

		public void Write(Span<byte> data, Meta? meta)
		{
			if (!running || meta?.Codec != Codec.OpusVoice
				|| (data.IsEmpty && meta.Control != PipeControl.EmptyTick))
				return;

			var currentQueue = queue;
			if (currentQueue is null)
				return;

			var frame = new VoiceFrame(meta.In.Sender, meta.Codec.Value, meta.Control, data.ToArray());
			try
			{
				// Never let recognition back-pressure TeamSpeak's packet receiver.
				currentQueue.TryAdd(frame);
			}
			catch (InvalidOperationException)
			{
				// The worker is shutting down; dropping this frame is intentional.
			}
		}

		private void OnEnabledChanged(object? sender, ConfigChangedEventArgs<bool> args)
		{
			if (args.NewValue)
				Start();
			else
				Stop();
		}

		private void OnWakeWordChanged(object? sender, ConfigChangedEventArgs<string> args)
		{
			if (!string.IsNullOrWhiteSpace(args.NewValue))
				Log.Info("Voice wake word changed for bot to '{0}'.", args.NewValue.Trim());
		}

		private void Start()
		{
			lock (lifecycleLock)
			{
				if (disposed || running)
					return;

				var modelPath = ResolveModelPath();
				if (!Directory.Exists(modelPath))
				{
					Log.Error("Voice control is enabled but the local Vosk model was not found at '{0}'.", modelPath);
					return;
				}

				try
				{
					Vosk.Vosk.SetLogLevel(-1);
					model = new Model(modelPath);
					queue = new BlockingCollection<VoiceFrame>(QueueCapacity);
					cancellation = new CancellationTokenSource();
					running = true;
					client.OutStream = packetReader;
					var token = cancellation.Token;
					worker = new Thread(() => RunWorker(queue, token))
					{
						Name = "VoiceControl",
						IsBackground = true,
						Priority = ThreadPriority.BelowNormal,
					};
					worker.Start();
					Log.Info("Local voice control enabled with wake word '{0}'.", config.WakeWord.Value.Trim());
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Could not start local voice control.");
					running = false;
					client.OutStream = null;
					queue?.Dispose();
					queue = null;
					cancellation?.Dispose();
					cancellation = null;
					worker = null;
					model?.Dispose();
					model = null;
				}
			}
		}

		private void Stop()
		{
			Thread? thread;
			BlockingCollection<VoiceFrame>? currentQueue;
			CancellationTokenSource? currentCancellation;

			lock (lifecycleLock)
			{
				if (!running)
				{
					client.OutStream = null;
					return;
				}

				running = false;
				client.OutStream = null;
				thread = worker;
				currentQueue = queue;
				currentCancellation = cancellation;
				currentCancellation?.Cancel();
				currentQueue?.CompleteAdding();
			}

			if (thread != null && thread != Thread.CurrentThread)
				thread.Join(TimeSpan.FromSeconds(2));

			lock (lifecycleLock)
			{
				currentQueue?.Dispose();
				currentCancellation?.Dispose();
				if (ReferenceEquals(queue, currentQueue)) queue = null;
				if (ReferenceEquals(cancellation, currentCancellation)) cancellation = null;
				worker = null;
				model?.Dispose();
				model = null;
			}
		}

		private void RunWorker(BlockingCollection<VoiceFrame> currentQueue, CancellationToken token)
		{
			var speakers = new Dictionary<ClientId, SpeakerState>();
			try
			{
				while (!token.IsCancellationRequested)
				{
					if (currentQueue.TryTake(out var frame, 100, token))
						ProcessFrame(frame, speakers);
					ExpireCommandWindows(speakers);
					ExpireSpeakers(speakers);
				}
			}
			catch (OperationCanceledException) { }
			catch (InvalidOperationException) when (currentQueue.IsAddingCompleted) { }
			catch (Exception ex) { Log.Error(ex, "Voice control worker stopped unexpectedly."); }
			finally
			{
				foreach (var speaker in speakers.Values)
					speaker.Dispose();
			}
		}

		private void ProcessFrame(VoiceFrame frame, Dictionary<ClientId, SpeakerState> speakers)
		{
			if (frame.Codec != Codec.OpusVoice)
				return;

			var currentModel = model;
			if (currentModel is null)
				return;

			var wakeWord = config.WakeWord.Value.Trim();
			if (!speakers.TryGetValue(frame.Sender, out var speaker))
			{
				speaker = new SpeakerState(currentModel, wakeWord, SampleRate);
				speakers.Add(frame.Sender, speaker);
			}
			else if (!string.Equals(speaker.WakeWord, wakeWord, StringComparison.Ordinal))
			{
				speaker.UpdateWakeWord(wakeWord, SampleRate);
			}

			if (frame.Control == PipeControl.EmptyTick)
			{
				speaker.LastSeenUtc = DateTime.UtcNow;
				speaker.EndSpeechSegment();
				HandleSegmentEnd(frame.Sender, speaker);
				return;
			}

			Span<byte> decoded;
			try
			{
				decoded = speaker.Decoder.Decode(frame.Data.AsSpan(), speaker.DecodeBuffer);
			}
			catch (OpusDecodeException ex) when (ex.Error == Errors.InvalidPacket)
			{
				Log.Debug("Dropped invalid Opus voice packet from client {0} (length {1}).", frame.Sender, frame.Data.Length);
				return;
			}
			if (decoded.IsEmpty)
				return;

			speaker.LastSeenUtc = DateTime.UtcNow;
			var pcm = decoded.ToArray();
			var activity = speaker.SpeechDetector.Push(pcm);
			foreach (var speechFrame in activity.Frames)
				ProcessSpeechFrame(frame.Sender, speaker, speechFrame);
			if (activity.Ended)
			{
				speaker.EndSpeechSegment();
				HandleSegmentEnd(frame.Sender, speaker);
			}
		}

		private void ProcessSpeechFrame(ClientId sender, SpeakerState speaker, byte[] pcm)
		{
			pcm = SpeechAudioPreprocessor.Prepare(pcm);
			if (!speaker.CommandMode)
			{
				speaker.AcceptWake();
				speaker.WakeRecognizer.AcceptWaveform(pcm, pcm.Length);
				var partial = ExtractText(speaker.WakeRecognizer.PartialResult(), "partial");
				if (speaker.ConfirmWake(partial))
				{
					speaker.BeginCommand();
					// Keep this audio in the command recognizer so commands spoken
					// without a pause still work, but do not count the wake phrase
					// itself as command audio.
					speaker.AcceptCommand(pcm, false);
				}
			}
			else
			{
				var partial = speaker.AcceptCommand(pcm, true);
				if (speaker.TryCommitStableControl(partial, out var command))
				{
					speaker.MarkCommandCommitted();
					QueueCommand(sender, command);
				}
			}
		}

		private void HandleSegmentEnd(ClientId sender, SpeakerState speaker)
		{
			if (!speaker.CommandMode)
			{
				var finalWake = ExtractText(speaker.FlushWake(), "text");
				if (VoiceCommandParser.TryParse(finalWake, speaker.WakeWord, false, out var command))
				{
					QueueCommand(sender, command);
				}
				else if (VoiceCommandParser.MatchWakeWord(finalWake, speaker.WakeWord) != VoiceWakeWordMatchKind.None)
				{
					speaker.BeginCommand();
				}
				return;
			}

			speaker.MarkCommandSegmentEnd(DateTime.UtcNow);
		}

		private void ExpireCommandWindows(Dictionary<ClientId, SpeakerState> speakers)
		{
			var now = DateTime.UtcNow;
			foreach (var pair in speakers.Where(x => x.Value.CommandMode && x.Value.ShouldFinishCommand(now)).ToArray())
				FinishCommand(pair.Key, pair.Value);
		}

		private void ExpireSpeakers(Dictionary<ClientId, SpeakerState> speakers)
		{
			var now = DateTime.UtcNow;
			foreach (var pair in speakers.Where(x => now - x.Value.LastSeenUtc > SpeakerExpiry).ToArray())
			{
				pair.Value.Dispose();
				speakers.Remove(pair.Key);
			}
		}

		private void FinishCommand(ClientId sender, SpeakerState speaker)
		{
			if (speaker.CommandCommitted || !speaker.CommandHasAudio)
			{
				speaker.ResetCommand();
				return;
			}

			var candidates = ExtractFinalCandidates(speaker.CommandRecognizer!.FinalResult());
			var wake = speaker.WakeWord;
			speaker.ResetCommand();

			if (!VoiceCommandParser.TryParseCandidates(candidates, wake, true, out var command))
				return;

			QueueCommand(sender, command);
		}

		private void QueueCommand(ClientId sender, VoiceCommand command)
		{
			_ = scheduler.InvokeAsync(() => ExecuteCommand(sender, command));
		}

		private async Task ExecuteCommand(ClientId sender, VoiceCommand command)
		{
			var invoker = new InvokerData(ResolveUid(sender));
			try
			{
				switch (command.Kind)
				{
				case VoiceCommandKind.Pause:
					player.Paused = true;
					break;
				case VoiceCommandKind.Resume:
					player.Paused = false;
					break;
				case VoiceCommandKind.Next:
					await playManager.Next(invoker);
					break;
				case VoiceCommandKind.PlaySong:
					await PlaySong(invoker, command);
					break;
				}
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Voice command '{0}' failed.", command.Kind);
			}
		}

		private async Task PlaySong(InvokerData invoker, VoiceCommand command)
		{
			var queries = command.SearchQueries.Take(3).ToArray();
			if (queries.Length == 0)
				throw new InvalidOperationException("Voice playback did not contain a song query.");

			AudioResource? selected = null;
			var selectedScore = int.MinValue;
			for (var queryIndex = 0; queryIndex < queries.Length; queryIndex++)
			{
				IList<AudioResource> results;
				try
				{
					results = await resourceResolver.Search("kuwo", queries[queryIndex]);
				}
				catch (Exception ex)
				{
					Log.Debug(ex, "Voice song search failed for candidate {0}.", queries[queryIndex]);
					continue;
				}

				for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
				{
					var score = ScoreSongCandidate(queries[queryIndex], results[resultIndex]);
					// Vosk returns alternatives from most to least likely. Keep that
					// ordering as the deterministic tie-breaker after title matching.
					score = score * 10 - queryIndex * 2 - resultIndex;
					if (score <= selectedScore)
						continue;

					selected = results[resultIndex];
					selectedScore = score;
				}
			}

			if (selected is null)
				throw new InvalidOperationException("No Kuwo search result was returned.");

			await playManager.Play(invoker, selected);
		}

		private static int ScoreSongCandidate(string query, AudioResource resource)
		{
			var normalizedQuery = NormalizeSongText(query);
			var normalizedTitle = NormalizeSongText(resource.ResourceTitle ?? string.Empty);
			if (normalizedQuery.Length == 0 || normalizedTitle.Length == 0)
				return 0;

			if (normalizedTitle.Contains(normalizedQuery, StringComparison.Ordinal))
				return 1000 + normalizedQuery.Length;
			if (normalizedQuery.Contains(normalizedTitle, StringComparison.Ordinal))
				return 900 + normalizedTitle.Length;

			var commonLength = LongestCommonSubsequenceLength(normalizedQuery, normalizedTitle);
			return commonLength * 1000 / Math.Max(normalizedQuery.Length, normalizedTitle.Length);
		}

		private static string NormalizeSongText(string value)
			=> VoiceCommandParser.Normalize(value).Replace("的", string.Empty, StringComparison.Ordinal);

		private static int LongestCommonSubsequenceLength(string left, string right)
		{
			var previous = new int[right.Length + 1];
			for (var i = 1; i <= left.Length; i++)
			{
				var current = new int[right.Length + 1];
				for (var j = 1; j <= right.Length; j++)
					current[j] = left[i - 1] == right[j - 1]
						? previous[j - 1] + 1
						: Math.Max(previous[j], current[j - 1]);
				previous = current;
			}
			return previous[right.Length];
		}

		private Uid ResolveUid(ClientId sender)
		{
			if (client.Book.Clients.TryGetValue(sender, out var clientInfo) && clientInfo.Uid.HasValue)
				return clientInfo.Uid.Value;
			return Uid.Anonymous;
		}

		private static string ExtractText(string json, string property)
		{
			try { return JObject.Parse(json).Value<string>(property) ?? string.Empty; }
			catch { return string.Empty; }
		}

		private static string ExtractPartialText(string json) => ExtractText(json, "partial");

		private static IReadOnlyList<string> ExtractFinalCandidates(string json)
		{
			try
			{
				var parsed = JObject.Parse(json);
				var candidates = (parsed["alternatives"] as JArray)?
					.OfType<JObject>()
					.Select(x => x.Value<string>("text"))
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.Distinct(StringComparer.Ordinal)
					.Take(3)
					.ToArray();
				if (candidates != null && candidates.Length > 0)
					return candidates;

				var text = parsed.Value<string>("text");
				return string.IsNullOrWhiteSpace(text) ? Array.Empty<string>() : new[] { text };
			}
			catch
			{
				return Array.Empty<string>();
			}
		}

		private static string ResolveModelPath()
		{
			var overridePath = System.Environment.GetEnvironmentVariable("TS3ABOT_VOICE_MODEL");
			return string.IsNullOrWhiteSpace(overridePath)
				? Path.Combine(AppContext.BaseDirectory, "voice-models", "vosk-model-small-cn-0.22")
				: Path.GetFullPath(overridePath);
		}

		public void Dispose()
		{
			lock (lifecycleLock)
			{
				if (disposed)
					return;
				disposed = true;
			}
			config.Enabled.Changed -= OnEnabledChanged;
			config.WakeWord.Changed -= OnWakeWordChanged;
			Stop();
		}

		private readonly struct VoiceFrame
		{
			public ClientId Sender { get; }
			public Codec Codec { get; }
			public PipeControl Control { get; }
			public byte[] Data { get; }

			public VoiceFrame(ClientId sender, Codec codec, PipeControl control, byte[] data)
			{
				Sender = sender;
				Codec = codec;
				Control = control;
				Data = data;
			}
		}

		private sealed class SpeakerState : IDisposable
		{
			private readonly Model model;
			private readonly VoiceCommandTiming commandTiming;
			private readonly VoiceWakeStability wakeStability = new VoiceWakeStability();
			private readonly VoiceCommandStability commandStability = new VoiceCommandStability();
			public string WakeWord { get; private set; }
			public OpusDecoder Decoder { get; }
			public SpeechActivityDetector SpeechDetector { get; }
			public byte[] DecodeBuffer { get; } = new byte[DecoderBufferSize];
			public VoskRecognizer WakeRecognizer { get; private set; }
			public VoskRecognizer? CommandRecognizer { get; private set; }
			public bool CommandMode => CommandRecognizer != null;
			public DateTime CommandDeadlineUtc => commandTiming.CommandDeadlineUtc;
			public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
			private bool wakeHasAudio;
			public bool CommandHasAudio => commandTiming.HasCommandAudio;
			public bool CommandCommitted { get; private set; }

			public SpeakerState(Model model, string wakeWord, int sampleRate)
			{
				this.model = model;
				commandTiming = new VoiceCommandTiming(CommandWindow);
				WakeWord = wakeWord;
				Decoder = OpusDecoder.Create(sampleRate, 1);
				SpeechDetector = new SpeechActivityDetector();
				SpeechDetector.SetHangoverFrames(SpeechActivityDetector.WakeHangoverFrames);
				WakeRecognizer = CreateWakeRecognizer(sampleRate);
			}

			private VoskRecognizer CreateWakeRecognizer(int sampleRate)
			{
				// Do not pass the custom wake word as a single Vosk grammar token.
				// Chinese phrases and user-defined names are often not literal entries
				// in the model vocabulary; Vosk would discard them before recognition.
				// The recognized text is filtered by ContainsWakeWord instead.
				var recognizer = new VoskRecognizer(model, sampleRate);
				recognizer.SetMaxAlternatives(0);
				return recognizer;
			}

			public void UpdateWakeWord(string wakeWord, int sampleRate)
			{
				if (string.Equals(WakeWord, wakeWord, StringComparison.Ordinal))
					return;

				ResetCommand();
				WakeRecognizer.Dispose();
				WakeWord = wakeWord;
				wakeStability.Reset();
				WakeRecognizer = CreateWakeRecognizer(sampleRate);
			}

			public bool ConfirmWake(string partial) => wakeStability.Confirm(partial, WakeWord);

			public void AcceptWake()
			{
				wakeHasAudio = true;
			}

			public string FlushWake()
			{
				if (!wakeHasAudio)
					return string.Empty;

				var result = WakeRecognizer.FinalResult();
				WakeRecognizer.Reset();
				wakeHasAudio = false;
				return result;
			}

			public void BeginCommand()
			{
				if (CommandRecognizer != null)
					return;
				CommandRecognizer = new VoskRecognizer(GetModel(), SampleRate);
				CommandRecognizer.SetMaxAlternatives(3);
				SpeechDetector.SetHangoverFrames(SpeechActivityDetector.CommandHangoverFrames);
				commandTiming.Begin(DateTime.UtcNow);
			}

			public string AcceptCommand(byte[] pcm, bool countAsCommandAudio)
			{
				var endpointDetected = CommandRecognizer!.AcceptWaveform(pcm, pcm.Length);
				commandTiming.AcceptAudio(countAsCommandAudio, endpointDetected, DateTime.UtcNow);
				return ExtractPartialText(CommandRecognizer.PartialResult());
			}

			public bool TryCommitStableControl(string partial, out VoiceCommand command)
				=> commandStability.TryCommitControl(partial, WakeWord, out command);

			public void MarkCommandCommitted() => CommandCommitted = true;

			public bool ShouldFinishCommand(DateTime now) => commandTiming.ShouldFinish(now);

			public void MarkCommandSegmentEnd(DateTime now) => commandTiming.MarkSegmentEnd(now);

			private Model GetModel()
			{
				return model;
			}

			public void ResetCommand()
			{
				CommandRecognizer?.Dispose();
				CommandRecognizer = null;
				CommandCommitted = false;
				commandTiming.Reset();
				wakeStability.Reset();
				commandStability.Reset();
				SpeechDetector.SetHangoverFrames(SpeechActivityDetector.WakeHangoverFrames);
				WakeRecognizer.Reset();
				wakeHasAudio = false;
			}

			public void EndSpeechSegment() => SpeechDetector.EndSegment();

			public void Dispose()
			{
				CommandRecognizer?.Dispose();
				WakeRecognizer.Dispose();
				SpeechDetector.Dispose();
				Decoder.Dispose();
			}
		}
	}

	internal sealed class VoiceCommandTiming
	{
		private static readonly TimeSpan EndpointGrace = TimeSpan.FromMilliseconds(300);
		private static readonly TimeSpan CommandSilenceFallback = TimeSpan.FromMilliseconds(700);
		private readonly TimeSpan commandWindow;

		public bool HasCommandAudio { get; private set; }
		public DateTime CommandDeadlineUtc { get; private set; }
		public DateTime? EndpointDeadlineUtc { get; private set; }
		public DateTime? CommandSilenceDeadlineUtc { get; private set; }

		public VoiceCommandTiming(TimeSpan commandWindow)
		{
			this.commandWindow = commandWindow;
			Reset();
		}

		public void Begin(DateTime now)
		{
			HasCommandAudio = false;
			EndpointDeadlineUtc = null;
			CommandSilenceDeadlineUtc = null;
			CommandDeadlineUtc = now + commandWindow;
		}

		public void AcceptAudio(bool isCommandAudio, bool endpointDetected, DateTime now)
		{
			// Audio containing only the wake phrase must not start the endpoint
			// grace period, otherwise a pause before the command could cancel it.
			if (!isCommandAudio)
				return;

			HasCommandAudio = true;
			CommandSilenceDeadlineUtc = now + CommandSilenceFallback;
			EndpointDeadlineUtc = null;
			if (endpointDetected)
				EndpointDeadlineUtc = now + EndpointGrace;
		}

		public void MarkSegmentEnd(DateTime now)
		{
			if (HasCommandAudio)
				EndpointDeadlineUtc = now + EndpointGrace;
		}

		public bool ShouldFinish(DateTime now)
			=> (!HasCommandAudio && now >= CommandDeadlineUtc)
				|| (EndpointDeadlineUtc.HasValue && now >= EndpointDeadlineUtc.Value)
				|| (CommandSilenceDeadlineUtc.HasValue && now >= CommandSilenceDeadlineUtc.Value);

		public void Reset()
		{
			HasCommandAudio = false;
			EndpointDeadlineUtc = null;
			CommandSilenceDeadlineUtc = null;
			CommandDeadlineUtc = DateTime.MinValue;
		}
	}
}
