using System;
using NUnit.Framework;
using TSLib;
using TSLib.Audio;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class AudioPacketReaderTests
	{
		[Test]
		public void SignalsSingleByteEndOfStreamPacketWithoutForwardingOpusData()
		{
			var sink = new CapturingConsumer();
			var reader = new AudioPacketReader { OutStream = sink };
			var packet = new byte[] { 0, 1, 0, 2, (byte)Codec.OpusVoice, 0 };

			reader.Write(packet, new Meta());

			Assert.IsNotNull(sink.Data);
			Assert.IsEmpty(sink.Data);
			Assert.IsNotNull(sink.Meta);
			Assert.AreEqual(PipeControl.EmptyTick, sink.Meta.Control);
		}

		[Test]
		public void ForwardsOpusPayloadAsData()
		{
			var sink = new CapturingConsumer();
			var reader = new AudioPacketReader { OutStream = sink };
			var packet = new byte[] { 0, 1, 0, 2, (byte)Codec.OpusVoice, 0x11, 0x22 };

			reader.Write(packet, new Meta());

			Assert.AreEqual(new byte[] { 0x11, 0x22 }, sink.Data);
			Assert.IsNotNull(sink.Meta);
			Assert.AreEqual(PipeControl.Data, sink.Meta.Control);
		}

		private sealed class CapturingConsumer : IAudioPassiveConsumer
		{
			public bool Active => true;
			public byte[] Data { get; private set; }
			public Meta Meta { get; private set; }

			public void Write(Span<byte> data, Meta? meta)
			{
				Data = data.ToArray();
				Meta = meta;
			}
		}
	}
}
