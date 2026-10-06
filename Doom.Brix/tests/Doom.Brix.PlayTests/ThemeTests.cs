using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.PlayTest;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace Doom.Brix.PlayTests;

public sealed class ThemeTests(AppFixture fixture) : DoomTest(fixture)
{
    // Doom.Brix paints its own black-metal palette, whatever the simulated OS theme is.
    [Fact]
    public async Task Assets_mode_keeps_its_own_palette_in_either_theme()
    {
        var theme = Fixture.Application.SystemTheme;
        await ChooseFolderAsync(ChooseFolder);
        await Address.ClickAsync();
        await Expect(Address).ToBeVisibleAsync();
        var png = await Page.ScreenshotAsync(new()
        {
            Path = Path.Combine(Fixture.DataDirectory, $"Doom.Brix-assets-{theme}.png"),
        });
        using var bitmap = SKBitmap.Decode(png);
        // The header band, just inside its top-left corner.
        bitmap.GetPixel(5, 5).Should().Be(new SKColor(0x1E, 0x17, 0x12));
        var folderBar = await FolderButton.BoundingBoxAsync();
        bitmap.GetPixel(5, (int)(folderBar.Y + folderBar.Height / 2)).Should().Be(new SKColor(0x24, 0x1D, 0x16));
        // The page background, in the margin left of the browser area.
        var browser = await Browser.BoundingBoxAsync();
        bitmap.GetPixel(5, (int)(browser.Y + browser.Height / 2)).Should().Be(new SKColor(0x12, 0x0F, 0x0C));
    }
}
