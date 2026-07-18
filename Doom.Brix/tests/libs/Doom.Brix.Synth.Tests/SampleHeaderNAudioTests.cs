using System.IO;
using CodeBrix.Audio.Engine.Metadata.SoundFont;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Synth.Tests;

// Cross-checks Doom.Brix.Synth's sample-header parsing against CodeBrix.Audio's SF2 parser.
//
//was previously: MeltySynthTest.SampleHeaderTest_NAudio (compared against NAudio.SoundFont)
public class SampleHeaderNAudioTests
{
    [Theory]
    [MemberData(nameof(TestSettings.SoundFontNames), MemberType = typeof(TestSettings))]
    public void sample_headers_match_the_codebrix_audio_reference_reader(string soundFontName)
    {
        var soundFont = TestSettings.LoadSoundFont(soundFontName);

        using var stream = File.OpenRead(TestSettings.FindSoundFontPath(soundFontName));
        var expected = SoundFontParser.Parse(stream).SampleHeaders!.SampleHeaders;

        // CodeBrix.Audio keeps the mandatory terminal (EOS) record; Doom.Brix.Synth drops it.
        (expected.Length - 1).Should().Be(soundFont.SampleHeaders.Count);

        for (var i = 0; i < soundFont.SampleHeaders.Count; i++)
        {
            var e = expected[i];
            var a = soundFont.SampleHeaders[i];

            Sf2Name.Of(e.Name).Should().Be(a.Name);
            ((int)e.Start).Should().Be(a.Start);
            ((int)e.End).Should().Be(a.End);
            ((int)e.StartLoop).Should().Be(a.StartLoop);
            ((int)e.EndLoop).Should().Be(a.EndLoop);
            ((int)e.SampleRate).Should().Be(a.SampleRate);
            e.OriginalKey.Should().Be(a.OriginalPitch);
            e.Correction.Should().Be(a.PitchCorrection);
        }
    }
}
