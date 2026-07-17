using System;
using System.Linq;
using ManagedDoom;
using ManagedDoom.Audio;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.GameEngine.Tests;

// Format detection for the MUS/MIDI decoders extracted out of upstream's
// Silk music backend. (Synthesizer render smoke tests arrive with the
// soundfont in the music phase — MeltySynth cannot be constructed
// without an .sf2 file.)
public class MusicDecoderFactoryTests
{
    [Fact]
    public void D_E1M1_is_recognized_as_mus()
    {
        using var wad = new Wad(TestWad.Doom1SharewarePath);
        var decoder = MusicDecoderFactory.Create(wad.ReadLump("D_E1M1"), loop: false);
        decoder.Should().BeOfType<MusDecoder>();
    }

    [Fact]
    public void Every_music_lump_in_the_shareware_wad_decodes_as_mus()
    {
        using var wad = new Wad(TestWad.Doom1SharewarePath);
        var musicLumps = wad.LumpInfos
            .Where(lump => lump.Name.StartsWith("D_"))
            .Select(lump => lump.Name)
            .ToArray();

        musicLumps.Length.Should().NotBe(0);
        foreach (var lump in musicLumps)
        {
            MusicDecoderFactory.Create(wad.ReadLump(lump), loop: false).Should().BeOfType<MusDecoder>();
        }
    }

    [Fact]
    public void Garbage_data_is_rejected()
    {
        Action act = () => MusicDecoderFactory.Create(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, loop: false);
        act.Should().Throw<Exception>();
    }
}
