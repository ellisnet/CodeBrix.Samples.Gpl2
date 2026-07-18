using System.IO;
using CodeBrix.Audio.Engine.Metadata.SoundFont;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Synth.Tests;

// Cross-checks Doom.Brix.Synth's instrument parsing against CodeBrix.Audio's SF2 parser.
//
//was previously: MeltySynthTest.InstrumentTest_NAudio (compared against NAudio.SoundFont)
public class InstrumentNAudioTests
{
    [Theory]
    [MemberData(nameof(TestSettings.SoundFontNames), MemberType = typeof(TestSettings))]
    public void instruments_match_the_codebrix_audio_reference_reader(string soundFontName)
    {
        var soundFont = TestSettings.LoadSoundFont(soundFontName);

        using var stream = File.OpenRead(TestSettings.FindSoundFontPath(soundFontName));
        var expected = SoundFontParser.Parse(stream).Instruments!.Instruments;

        // CodeBrix.Audio keeps the mandatory terminal (EOI) record; Doom.Brix.Synth drops it.
        (expected.Length - 1).Should().Be(soundFont.Instruments.Count);

        for (var i = 0; i < soundFont.Instruments.Count; i++)
        {
            Sf2Name.Of(expected[i].Name).Should().Be(soundFont.Instruments[i].Name);
        }
    }
}
