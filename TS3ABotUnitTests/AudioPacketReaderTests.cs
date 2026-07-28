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
		public void DropsSingleByteEndOfStreamPacket()
		{
			var sink = new CapturingConsumer();
			var reader = new AudioPacketReader { OutStream = sink };
			var packet = new byte[] { 0, 1, 0, 2, (byte)Codec.OpusVoice, 0 };

			reader.Write(packet, new Meta());

			Assert.IsNull(sink.Data);
			Assert.IsNull(sink.Meta);
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
