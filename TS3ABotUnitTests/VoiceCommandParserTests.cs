using NUnit.Framework;
using TS3AudioBot.Audio;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class VoiceCommandParserTests
	{
		[TestCase("音乐机器人暂停", VoiceCommandKind.Pause)]
		[TestCase("音乐机器人，暂停播放", VoiceCommandKind.Pause)]
		[TestCase("音乐机器人继续", VoiceCommandKind.Resume)]
		[TestCase("音乐机器人下一首", VoiceCommandKind.Next)]
		public void RequiresWakeWordForPlaybackCommands(string text, VoiceCommandKind expected)
		{
			Assert.IsTrue(VoiceCommandParser.TryParse(text, "音乐机器人", false, out var command));
			Assert.AreEqual(expected, command.Kind);
		}

		[Test]
		public void IgnoresCommandWithoutWakeWord()
		{
			Assert.IsFalse(VoiceCommandParser.TryParse("暂停", "音乐机器人", false, out _));
		}

		[Test]
		public void ParsesSongQueryAfterWakeWord()
		{
			Assert.IsTrue(VoiceCommandParser.TryParse("音乐机器人，播放周杰伦七里香", "音乐机器人", false, out var command));
			Assert.AreEqual(VoiceCommandKind.PlaySong, command.Kind);
			Assert.AreEqual("周杰伦七里香", command.Argument);
		}

		[Test]
		public void AcceptsRecognizerResultWithWakeWordAfterWakeWasMatched()
		{
			Assert.IsTrue(VoiceCommandParser.TryParse("音乐机器人播放周杰伦七里香", "音乐机器人", true, out var command));
			Assert.AreEqual(VoiceCommandKind.PlaySong, command.Kind);
			Assert.AreEqual("周杰伦七里香", command.Argument);
		}
	}
}
