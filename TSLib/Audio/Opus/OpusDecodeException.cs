using System;

namespace TSLib.Audio.Opus
{
	public sealed class OpusDecodeException : Exception
	{
		public Errors Error { get; }

		public OpusDecodeException(Errors error)
			: base("Decoding failed - " + error)
		{
			Error = error;
		}
	}
}
