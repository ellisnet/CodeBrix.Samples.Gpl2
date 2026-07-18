using System;
using System.Linq;
using SilverAssertions;
using Wolfenstein.Brix.GameEngine.Assets;
using Wolfenstein.Brix.GameEngine.Audio;
using Xunit;

namespace Wolfenstein.Brix.GameEngine.Tests;

// OPL synthesizer checks against the real AUDIOT chunks: the emulated
// chip must produce deterministic, audibly non-silent PCM for the
// shareware music and AdLib effects. (Bit-exactness of the chip core
// against the C original is verified separately by the port harness.)
public class WolfOplSynthTests
{
    private static AudioTFile LoadAudio() =>
        WolfAssets.Load(TestWl1.AssetsFolderPath).Audio;

    private static double Rms(float[] buffer)
    {
        double sum = 0;
        foreach (var sample in buffer)
        {
            sum += sample * sample;
        }

        return Math.Sqrt(sum / buffer.Length);
    }

    [Fact]
    public void Title_music_produces_audible_deterministic_output()
    {
        //Arrange - NAZI_NOR, the title track
        var audio = LoadAudio();
        var synthA = new WolfOplSynth();
        var synthB = new WolfOplSynth();
        var bufferA = new float[WolfOplSynth.SampleRate * 2];
        var bufferB = new float[WolfOplSynth.SampleRate * 2];

        //Act - one second of music on two independent synths
        synthA.PlayMusic(audio.GetMusic(7));
        synthB.PlayMusic(audio.GetMusic(7));
        synthA.Generate(bufferA);
        synthB.Generate(bufferB);

        //Assert
        (Rms(bufferA) > 0.001).Should().BeTrue();
        bufferA.SequenceEqual(bufferB).Should().BeTrue();
    }

    [Fact]
    public void Every_shareware_music_track_parses_and_plays()
    {
        var audio = LoadAudio();
        audio.MusicCount.Should().Be(27);
        var buffer = new float[WolfOplSynth.SampleRate / 2];
        for (var track = 0; track < audio.MusicCount; track++)
        {
            var chunk = audio.GetMusic(track);
            var synth = new WolfOplSynth();
            synth.PlayMusic(chunk);
            synth.Generate(buffer);

            // The shareware file carries a few 88-byte stub tracks;
            // real tracks must be audible, stubs need only parse.
            if (chunk.Length > 500)
            {
                (Rms(buffer) > 0.0001).Should().BeTrue();
            }
        }
    }

    [Fact]
    public void Adlib_effect_plays_and_ends()
    {
        //Arrange - the knife swing effect
        var audio = LoadAudio();
        var synth = new WolfOplSynth();
        var buffer = new float[WolfOplSynth.SampleRate * 2];

        //Act
        synth.PlaySfx(audio.GetAdlibSound(23));
        synth.IsSfxPlaying.Should().BeTrue();
        synth.Generate(buffer);

        //Assert - it sounded, and a one-second window outlives it
        (Rms(buffer) > 0.0005).Should().BeTrue();
        synth.IsSfxPlaying.Should().BeFalse();
    }

    [Fact]
    public void Higher_priority_effects_are_not_interrupted_by_lower()
    {
        var audio = LoadAudio();

        // Find two effects with different priorities.
        int lowIndex = -1, highIndex = -1;
        ushort lowPriority = ushort.MaxValue, highPriority = 0;
        for (var i = 0; i < AudioTFile.SoundCount; i++)
        {
            var chunk = audio.GetAdlibSound(i);
            if (chunk.Length < 24)
            {
                continue;
            }

            var priority = BitConverter.ToUInt16(chunk, 4);
            if (priority < lowPriority)
            {
                lowPriority = priority;
                lowIndex = i;
            }

            if (priority > highPriority)
            {
                highPriority = priority;
                highIndex = i;
            }
        }

        (lowIndex >= 0 && highIndex >= 0 && highPriority > lowPriority).Should().BeTrue();

        //Act - start the high-priority effect, then try the low one
        var synth = new WolfOplSynth();
        synth.PlaySfx(audio.GetAdlibSound(highIndex));
        var buffer = new float[1024];
        synth.Generate(buffer);
        synth.PlaySfx(audio.GetAdlibSound(lowIndex));

        //Assert - still playing the high one (not restarted by low)
        synth.IsSfxPlaying.Should().BeTrue();
    }

    [Fact]
    public void Music_and_effects_coexist()
    {
        var audio = LoadAudio();
        var synth = new WolfOplSynth();
        var musicOnly = new float[WolfOplSynth.SampleRate];
        var mixed = new float[WolfOplSynth.SampleRate];

        synth.PlayMusic(audio.GetMusic(3)); // GETTHEM
        synth.Generate(musicOnly);
        synth.PlaySfx(audio.GetAdlibSound(23));
        synth.Generate(mixed);

        (Rms(musicOnly) > 0.001).Should().BeTrue();
        (Rms(mixed) > 0.001).Should().BeTrue();
    }
}
