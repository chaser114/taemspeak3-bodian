using System;
using NUnit.Framework;
using TS3AudioBot.Audio;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class VoiceCommandTimingTests
	{
		private static readonly DateTime Start = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);

		[Test]
		public void EndpointFinishesAfterShortGracePeriod()
		{
			var timing = StartTiming();

			timing.AcceptAudio(true, true, Start.AddSeconds(1));

			Assert.IsFalse(timing.ShouldFinish(Start.AddSeconds(1.29)));
			Assert.IsTrue(timing.ShouldFinish(Start.AddSeconds(1.3)));
		}

		[Test]
		public void NewAudioCancelsPendingEndpointFinish()
		{
			var timing = StartTiming();

			timing.AcceptAudio(true, true, Start.AddSeconds(1));
			timing.AcceptAudio(true, false, Start.AddSeconds(1.3));

			Assert.IsFalse(timing.ShouldFinish(Start.AddSeconds(1.59)));
		}

		[Test]
		public void SegmentEndStartsShortGracePeriod()
		{
			var timing = StartTiming();

			timing.AcceptAudio(true, false, Start.AddSeconds(1));
			timing.MarkSegmentEnd(Start.AddSeconds(1.2));

			Assert.IsFalse(timing.ShouldFinish(Start.AddSeconds(1.49)));
			Assert.IsTrue(timing.ShouldFinish(Start.AddSeconds(1.5)));
		}

		[Test]
		public void WakePhraseAudioDoesNotStartEndpointGracePeriod()
		{
			var timing = StartTiming();

			timing.AcceptAudio(false, true, Start.AddSeconds(1));

			Assert.IsFalse(timing.HasCommandAudio);
			Assert.IsFalse(timing.ShouldFinish(Start.AddSeconds(1.3)));
		}

		[Test]
		public void CommandWindowRemainsFallbackWhenNoEndpointIsDetected()
		{
			var timing = StartTiming();

			Assert.IsFalse(timing.ShouldFinish(Start.AddSeconds(3.99)));
			Assert.IsTrue(timing.ShouldFinish(Start.AddSeconds(4)));
		}

		[Test]
		public void CommandAudioUsesShortSilenceFallbackWithoutEndpoint()
		{
			var timing = StartTiming();

			timing.AcceptAudio(true, false, Start.AddSeconds(1));

			Assert.IsFalse(timing.ShouldFinish(Start.AddSeconds(1.69)));
			Assert.IsTrue(timing.ShouldFinish(Start.AddSeconds(1.7)));
		}

		[Test]
		public void ContinuedCommandAudioExtendsShortSilenceFallback()
		{
			var timing = StartTiming();

			timing.AcceptAudio(true, false, Start.AddSeconds(1));
			timing.AcceptAudio(true, false, Start.AddSeconds(1.8));

			Assert.IsFalse(timing.ShouldFinish(Start.AddSeconds(2.49)));
			Assert.IsTrue(timing.ShouldFinish(Start.AddSeconds(2.51)));
		}

		private static VoiceCommandTiming StartTiming()
		{
			var timing = new VoiceCommandTiming(TimeSpan.FromSeconds(4));
			timing.Begin(Start);
			return timing;
		}
	}
}
