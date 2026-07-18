using System.IO;
using CodeBrix.Audio.Engine.Metadata.SoundFont;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Synth.Tests;

// Cross-checks Doom.Brix.Synth's preset parsing against CodeBrix.Audio's SF2 parser,
// the modern replacement for the upstream MeltySynth-vs-NAudio comparison.
//
//was previously: MeltySynthTest.PresetTest_NAudio (compared against NAudio.SoundFont)
public class PresetNAudioTests
{
    [Theory]
    [MemberData(nameof(TestSettings.SoundFontNames), MemberType = typeof(TestSettings))]
    public void presets_match_the_codebrix_audio_reference_reader(string soundFontName)
    {
        var soundFont = TestSettings.LoadSoundFont(soundFontName);

        using var stream = File.OpenRead(TestSettings.FindSoundFontPath(soundFontName));
        var expected = SoundFontParser.Parse(stream).Presets!.Presets;

        // CodeBrix.Audio keeps the mandatory terminal (EOP) record; Doom.Brix.Synth drops it.
        (expected.Length - 1).Should().Be(soundFont.Presets.Count);

        for (var i = 0; i < soundFont.Presets.Count; i++)
        {
            Sf2Name.Of(expected[i].Name).Should().Be(soundFont.Presets[i].Name);
            ((int)expected[i].Preset).Should().Be(soundFont.Presets[i].PatchNumber);
            ((int)expected[i].Bank).Should().Be(soundFont.Presets[i].BankNumber);
        }
    }
}
