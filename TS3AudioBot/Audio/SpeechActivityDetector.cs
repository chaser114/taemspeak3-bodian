using System;
using System.Collections.Generic;
using WebRtcVadSharp;

namespace TS3AudioBot.Audio
{
	internal sealed class SpeechActivityDetector : IDisposable
	{
		public const int SampleRate = 16_000;
		public const int FrameDurationMs = 20;
		public const int FrameBytes = SampleRate * FrameDurationMs / 1000 * 2;
		public const int WakeHangoverFrames = 40;
		public const int CommandHangoverFrames = 15;

		private const int PreRollFrames = 10;

		private readonly Func<byte[], bool> classifier;
		private readonly WebRtcVad? webRtcVad;
		private readonly Queue<byte[]> preRoll = new Queue<byte[]>(PreRollFrames);
		private readonly List<byte> pendingPcm = new List<byte>(FrameBytes);
		private bool active;
		private int silentFrames;
		private int hangoverFrames = CommandHangoverFrames;

		public SpeechActivityDetector()
		{
			webRtcVad = new WebRtcVad
			{
				SampleRate = WebRtcVadSharp.SampleRate.Is16kHz,
				FrameLength = WebRtcVadSharp.FrameLength.Is20ms,
				OperatingMode = OperatingMode.LowBitrate,
			};
			classifier = webRtcVad.HasSpeech;
		}

		internal SpeechActivityDetector(Func<byte[], bool> classifier)
		{
			this.classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
		}

		public bool IsActive => active;

		public void SetHangoverFrames(int frames)
		{
			if (frames <= 0)
				throw new ArgumentOutOfRangeException(nameof(frames));
			hangoverFrames = frames;
		}

		public SpeechActivityResult Push(byte[] pcm)
		{
			if (pcm is null)
				throw new ArgumentNullException(nameof(pcm));

			pendingPcm.AddRange(pcm);
			var frames = new List<byte[]>();
			var started = false;
			var ended = false;
			while (pendingPcm.Count >= FrameBytes)
			{
				var frame = pendingPcm.GetRange(0, FrameBytes).ToArray();
				pendingPcm.RemoveRange(0, FrameBytes);
				var frameResult = PushFrame(frame);
				if (frameResult.Frames.Count > 0)
					frames.AddRange(frameResult.Frames);
				started |= frameResult.Started;
				ended |= frameResult.Ended;
			}

			return frames.Count == 0 && !started && !ended
				? SpeechActivityResult.Empty
				: new SpeechActivityResult(frames, started, ended);
		}

		public SpeechActivityResult EndSegment()
		{
			pendingPcm.Clear();
			if (!active)
			{
				preRoll.Clear();
				return SpeechActivityResult.Empty;
			}

			active = false;
			silentFrames = 0;
			preRoll.Clear();
			return new SpeechActivityResult(Array.Empty<byte[]>(), started: false, ended: true);
		}

		private SpeechActivityResult PushFrame(byte[] frame)
		{
			var hasSpeech = classifier(frame);
			if (!active)
			{
				RememberPreRoll(frame);
				if (!hasSpeech)
					return SpeechActivityResult.Empty;

				active = true;
				silentFrames = 0;
				var frames = new List<byte[]>(preRoll);
				preRoll.Clear();
				return new SpeechActivityResult(frames, started: true, ended: false);
			}

			if (hasSpeech)
				silentFrames = 0;
			else
				silentFrames++;

			var ended = silentFrames >= hangoverFrames;
			if (ended)
				active = false;

			return new SpeechActivityResult(
				new[] { frame },
				started: false,
				ended: ended);
		}

		public void Reset()
		{
			active = false;
			silentFrames = 0;
			preRoll.Clear();
			pendingPcm.Clear();
		}

		public void Dispose()
		{
			Reset();
			webRtcVad?.Dispose();
		}

		private void RememberPreRoll(byte[] frame)
		{
			if (preRoll.Count == PreRollFrames)
				preRoll.Dequeue();
			preRoll.Enqueue(frame);
		}

	}

	internal sealed class SpeechActivityResult
	{
		public static SpeechActivityResult Empty { get; } = new SpeechActivityResult(Array.Empty<byte[]>(), false, false);

		public IReadOnlyList<byte[]> Frames { get; }
		public bool Started { get; }
		public bool Ended { get; }

		public SpeechActivityResult(IReadOnlyList<byte[]> frames, bool started, bool ended)
		{
			Frames = frames;
			Started = started;
			Ended = ended;
		}
	}
}
