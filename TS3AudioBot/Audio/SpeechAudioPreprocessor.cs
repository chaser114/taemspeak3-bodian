using System;

namespace TS3AudioBot.Audio
{
	internal static class SpeechAudioPreprocessor
	{
		private const double TargetRms = 3000;
		private const double MaximumGain = 6;
		private const double MinimumRms = 120;

		public static byte[] Prepare(byte[] pcm)
		{
			if (pcm is null)
				throw new ArgumentNullException(nameof(pcm));
			if (pcm.Length < 2)
				return pcm;

			var sum = 0.0;
			var sampleCount = pcm.Length / 2;
			for (var i = 0; i < sampleCount; i++)
			{
				var sample = ReadSample(pcm, i * 2);
				sum += (double)sample * sample;
			}

			var rms = Math.Sqrt(sum / sampleCount);
			if (rms < MinimumRms)
				return pcm;

			var gain = Math.Min(MaximumGain, TargetRms / rms);
			if (gain <= 1.05)
				return pcm;

			var prepared = new byte[pcm.Length];
			for (var i = 0; i < sampleCount; i++)
			{
				var sample = (int)Math.Round(ReadSample(pcm, i * 2) * gain);
				sample = Math.Max(short.MinValue, Math.Min(short.MaxValue, sample));
				prepared[i * 2] = (byte)(sample & 0xff);
				prepared[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
			}

			if ((pcm.Length & 1) != 0)
				prepared[prepared.Length - 1] = pcm[pcm.Length - 1];
			return prepared;
		}

		private static short ReadSample(byte[] pcm, int offset)
			=> (short)(pcm[offset] | pcm[offset + 1] << 8);
	}
}
