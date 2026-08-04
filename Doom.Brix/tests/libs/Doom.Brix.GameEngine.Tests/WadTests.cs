using ManagedDoom;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.GameEngine.Tests;

// WAD-parsing checks against the real shareware DOOM1.WAD (v1.9).
// The expected values were read directly from the canonical file
// (md5 f0cefca49926d00903cf57551d901abe).
public class WadTests
{
    [Fact]
    public void Shareware_wad_is_detected_as_shareware()
    {
        using var wad = new Wad(TestWad.Doom1SharewarePath);
        wad.GameMode.Should().Be(GameMode.Shareware);
    }

    [Fact]
    public void Lump_directory_matches_the_v19_shareware_layout()
    {
        using var wad = new Wad(TestWad.Doom1SharewarePath);

        wad.LumpInfos.Count.Should().Be(1264);
        wad.GetLumpNumber("PLAYPAL").Should().Be(0);
        wad.GetLumpNumber("COLORMAP").Should().Be(1);
        wad.GetLumpNumber("DEMO1").Should().Be(3);
        wad.GetLumpNumber("DEMO2").Should().Be(4);
        wad.GetLumpNumber("DEMO3").Should().Be(5);
        wad.GetLumpNumber("E1M1").Should().Be(6);
        wad.GetLumpNumber("TEXTURE1").Should().Be(105);
        wad.GetLumpNumber("F_START").Should().Be(1206);
        wad.GetLumpNumber("F_END").Should().Be(1263);
    }

    [Fact]
    public void All_nine_episode_one_maps_are_present()
    {
        using var wad = new Wad(TestWad.Doom1SharewarePath);
        for (var map = 1; map <= 9; map++)
        {
            wad.GetLumpNumber("E1M" + map).Should().NotBe(-1);
        }
    }

    [Fact]
    public void Texture2_is_absent_in_shareware()
    {
        using var wad = new Wad(TestWad.Doom1SharewarePath);
        wad.GetLumpNumber("TEXTURE2").Should().Be(-1);
    }

    [Fact]
    public void Flats_are_all_4096_bytes_or_empty()
    {
        using var wad = new Wad(TestWad.Doom1SharewarePath);
        var start = wad.GetLumpNumber("F_START") + 1;
        var end = wad.GetLumpNumber("F_END");
        for (var lump = start; lump < end; lump++)
        {
            var size = wad.GetLumpSize(lump);
            (size == 0 || size == 4096).Should().BeTrue();
        }
    }

    [Fact]
    public void Music_and_sound_effect_lumps_are_present()
    {
        using var wad = new Wad(TestWad.Doom1SharewarePath);
        wad.GetLumpNumber("D_E1M1").Should().Be(219);
        wad.GetLumpNumber("DSPISTOL").Should().NotBe(-1);
    }
}
