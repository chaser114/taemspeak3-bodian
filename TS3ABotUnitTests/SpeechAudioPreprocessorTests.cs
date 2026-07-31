using NUnit.Framework;
using TS3AudioBot.Audio;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class SpeechAudioPreprocessorTests
	{
		[Test]
		public void QuietAudioIsNotAmplified()
		{
			var pcm = Samples(20);

			Assert.AreSame(pcm, SpeechAudioPreprocessor.Prepare(pcm));
		}

		[Test]
		public void LowLevelSpeechIsAmplified()
		{
			var pcm = Samples(500);
			var prepared = SpeechAudioPreprocessor.Prepare(pcm);

			Assert.AreNotSame(pcm, prepared);
			Assert.Greater(System.Math.Abs(Read(prepared, 0)), System.Math.Abs(Read(pcm, 0)));
		}

		[Test]
		public void LoudSpeechDoesNotGetAmplifiedOrClipped()
		{
			var pcm = Samples(12000);
			var prepared = SpeechAudioPreprocessor.Prepare(pcm);

			Assert.AreSame(pcm, prepared);
			Assert.AreEqual(12000, Read(prepared, 0));
		}

		private static byte[] Samples(short value)
		{
			var pcm = new byte[320];
			for (var i = 0; i < pcm.Length; i += 2)
			{
				pcm[i] = (byte)(value & 0xff);
				pcm[i + 1] = (byte)((value >> 8) & 0xff);
			}
			return pcm;
		}

		private static short Read(byte[] pcm, int offset)
			=> (short)(pcm[offset] | pcm[offset + 1] << 8);
	}
}
