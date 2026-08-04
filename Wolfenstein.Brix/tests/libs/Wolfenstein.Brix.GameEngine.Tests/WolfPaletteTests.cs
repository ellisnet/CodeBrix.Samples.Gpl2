using SilverAssertions;
using Wolfenstein.Brix.GameEngine.Assets;
using Xunit;

namespace Wolfenstein.Brix.GameEngine.Tests;

// Spot checks of the translated VGA palette against values computed
// by hand from the GPL source's 6-bit table.
public class WolfPaletteTests
{
    [Fact]
    public void Palette_has_256_opaque_entries()
    {
        WolfPalette.Rgba.Length.Should().Be(256);
        foreach (var entry in WolfPalette.Rgba)
        {
            (entry >> 24).Should().Be(0xFFu);
        }
    }

    [Fact]
    public void Entry_0_is_black() =>
        WolfPalette.Rgba[0].Should().Be(0xFF000000u);

    [Fact]
    public void Entry_15_is_white()
    {
        // 6-bit (63,63,63) converts to 8-bit 255 per channel.
        WolfPalette.Rgba[15].Should().Be(0xFFFFFFFFu);
    }

    [Fact]
    public void Entry_25_is_the_floor_gray() =>
        WolfPalette.Rgba[0x19].Should().Be(0xFF717171u);

    [Fact]
    public void Entry_29_is_the_ceiling_gray() =>
        WolfPalette.Rgba[0x1D].Should().Be(0xFF383838u);

    [Fact]
    public void Entry_255_is_the_sentinel_magenta() =>
        WolfPalette.Rgba[255].Should().Be(0xFF8A009Au);
}
