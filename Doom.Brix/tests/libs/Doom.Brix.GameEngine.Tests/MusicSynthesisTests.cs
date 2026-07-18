using System;
using System.IO;
using System.Linq;
using ManagedDoom;
using ManagedDoom.Audio;
using Doom.Brix.Synth;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.GameEngine.Tests;

// Headless music synthesis: a WAD music lump decoded by the extracted MUS
// decoder and rendered through MeltySynth with the repo's shipped TimGM6mb
// SoundFont must produce audible (non-silent) output. This is the whole
// music pipeline minus the audio device.
public class MusicSynthesisTests
{
    [Fact]
    public void D_E1M1_renders_non_silent_audio_through_the_shipped_soundfont()
    {
        using var wad = new Wad(TestWad.Doom1SharewarePath);
        var decoder = MusicDecoderFactory.Create(wad.ReadLump("D_E1M1"), loop: false);

        var settings = new SynthesizerSettings(MusDecoder.SampleRate);
        settings.BlockSize = MusDecoder.BlockLength;
        var synthesizer = new Synthesizer(FindShippedSoundFont(), settings);

        // Render two seconds; the At Doom's Gate riff starts immediately.
        var left = new float[2 * MusDecoder.SampleRate];
        var right = new float[2 * MusDecoder.SampleRate];
        decoder.RenderWaveform(synthesizer, left, right);

        var peak = left.Concat(right).Max(sample => Math.Abs(sample));
        (peak > 0.01F).Should().BeTrue();
    }

    // The SoundFont is committed to the repo (unlike DOOM1.WAD), so it is
    // located relative to the repo root the same way TestWad walks up.
    private static string FindShippedSoundFont()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                directory.FullName, "Doom.Brix", "src", "libs", "Doom.Brix.Game",
                "ThirdPartyAssets", "TimGM6mb.sf2");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "The shipped TimGM6mb.sf2 was not found; it lives (committed) at "
            + "Doom.Brix/src/libs/Doom.Brix.Game/ThirdPartyAssets/TimGM6mb.sf2.");
    }
}
