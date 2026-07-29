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
		public void PreservesNaturalSingerSongQuery()
		{
			Assert.IsTrue(VoiceCommandParser.TryParse("音乐机器人，播放刘德华的十七岁", "音乐机器人", false, out var command));
			Assert.AreEqual(VoiceCommandKind.PlaySong, command.Kind);
			Assert.AreEqual("刘德华的十七岁", command.Argument);
		}

		[Test]
		public void AcceptsRecognizerResultWithWakeWordAfterWakeWasMatched()
		{
			Assert.IsTrue(VoiceCommandParser.TryParse("音乐机器人播放周杰伦七里香", "音乐机器人", true, out var command));
			Assert.AreEqual(VoiceCommandKind.PlaySong, command.Kind);
			Assert.AreEqual("周杰伦七里香", command.Argument);
		}

		[Test]
		public void AcceptsOneCharacterWakeWordError()
		{
			Assert.IsTrue(VoiceCommandParser.TryParse("音乐器人暂停", "音乐机器人", false, out var command));
			Assert.AreEqual(VoiceCommandKind.Pause, command.Kind);
		}

		[Test]
		public void ClassifiesThreeCharacterWakePrefixAsPartial()
		{
			Assert.AreEqual(
				VoiceWakeWordMatchKind.Partial,
				VoiceCommandParser.MatchWakeWord("音乐机", "音乐机器人"));
			Assert.AreEqual(
				VoiceWakeWordMatchKind.Confirmed,
				VoiceCommandParser.MatchWakeWord("音乐器人", "音乐机器人"));
		}

		[Test]
		public void DoesNotArmForCommonWakeWordSuffix()
		{
			Assert.AreEqual(
				VoiceWakeWordMatchKind.None,
				VoiceCommandParser.MatchWakeWord("机器人", "音乐机器人"));
			Assert.IsFalse(VoiceCommandParser.TryParse("机器人暂停", "音乐机器人", false, out _));
		}

		[Test]
		public void PartialWakeNeedsCommandAfterWakeWasMatched()
		{
			Assert.IsFalse(VoiceCommandParser.TryParse("音乐机暂停", "音乐机器人", false, out _));
			Assert.IsTrue(VoiceCommandParser.TryParse("暂停", "音乐机器人", true, out var command));
			Assert.AreEqual(VoiceCommandKind.Pause, command.Kind);
		}

		[TestCase("音乐机器人暂亭播放", VoiceCommandKind.Pause)]
		[TestCase("音乐机器人下一酋", VoiceCommandKind.Next)]
		[TestCase("音乐机器人恢复播放", VoiceCommandKind.Resume)]
		public void AcceptsSmallFixedCommandErrors(string text, VoiceCommandKind expected)
		{
			Assert.IsTrue(VoiceCommandParser.TryParse(text, "音乐机器人", false, out var command));
			Assert.AreEqual(expected, command.Kind);
		}

		[Test]
		public void DoesNotFuzzyReplaceSongName()
		{
			Assert.IsTrue(VoiceCommandParser.TryParse("音乐机器人播方周杰纶七里香", "音乐机器人", false, out var command));
			Assert.AreEqual(VoiceCommandKind.PlaySong, command.Kind);
			Assert.AreEqual("周杰纶七里香", command.Argument);
		}
	}
}
