using System;
using NUnit.Framework;
using TS3AudioBot.Audio;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class SpeechActivityDetectorTests
	{
		[Test]
		public void QuietFramesDoNotOpenSpeech()
		{
			using (var detector = CreateDetector(false))
			{
				var result = detector.Push(Frame(false));

				Assert.IsFalse(detector.IsActive);
				Assert.IsFalse(result.Started);
				Assert.IsEmpty(result.Frames);
			}
		}

		[Test]
		public void SpeechStartIncludesPreRoll()
		{
			using (var detector = CreateDetector(frame => frame[0] == 1))
			{
				detector.Push(Frame(false, 0));
				detector.Push(Frame(false, 0));
				var result = detector.Push(Frame(true, 1));

				Assert.IsTrue(result.Started);
				Assert.AreEqual(3, result.Frames.Count);
				Assert.AreEqual(1, result.Frames[2][0]);
			}
		}

		[Test]
		public void SpeechKeepsQuietHangoverFramesBeforeEnding()
		{
			using (var detector = CreateDetector(frame => frame[0] == 1))
			{
				detector.Push(Frame(true, 1));
				SpeechActivityResult result = SpeechActivityResult.Empty;
				for (var i = 0; i < 14; i++)
					result = detector.Push(Frame(false, 0));

				Assert.IsFalse(result.Ended);
				Assert.IsTrue(detector.IsActive);

				result = detector.Push(Frame(false, 0));
				Assert.IsTrue(result.Ended);
				Assert.IsFalse(detector.IsActive);
			}
		}

		[Test]
		public void ResetClearsActiveSegment()
		{
			using (var detector = CreateDetector(true))
			{
				detector.Push(Frame(true));
				detector.Reset();

				Assert.IsFalse(detector.IsActive);
			}
		}

		[Test]
		public void WakeModeCanKeepShortPausesInsideOneSegment()
		{
			using (var detector = CreateDetector(frame => frame[0] == 1))
			{
				detector.SetHangoverFrames(3);
				detector.Push(Frame(true, 1));

			Assert.IsFalse(detector.Push(Frame(false, 0)).Ended);
			Assert.IsFalse(detector.Push(Frame(false, 0)).Ended);
			Assert.IsTrue(detector.Push(Frame(false, 0)).Ended);
			}
		}

		private static SpeechActivityDetector CreateDetector(bool result)
			=> new SpeechActivityDetector(_ => result);

		private static SpeechActivityDetector CreateDetector(Func<byte[], bool> classifier)
			=> new SpeechActivityDetector(classifier);

		private static byte[] Frame(bool _, byte marker = 0)
		{
			var frame = new byte[SpeechActivityDetector.FrameBytes];
			frame[0] = marker;
			return frame;
		}
	}
}
