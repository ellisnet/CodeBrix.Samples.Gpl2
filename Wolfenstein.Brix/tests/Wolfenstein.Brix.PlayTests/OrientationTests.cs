using System.Threading.Tasks;
using CodeBrix.Platform.PlayTest;
using SilverAssertions;
using Xunit;

namespace Wolfenstein.Brix.PlayTests;

public sealed class OrientationTests(AppFixture fixture) : WolfensteinTest(fixture)
{
    [Fact]
    [PlayTestOrientation(ScreenOrientation.Portrait)]
    public async Task Assets_mode_fits_portrait_screen()
    {
        Fixture.Application.Orientation.Should().Be(ScreenOrientation.Portrait);
        await Expect(ChooseFolder).ToBeVisibleAsync();
        var setup = await ChooseFolder.BoundingBoxAsync();
        (setup.X + setup.Width).Should().BeLessThanOrEqualTo(Fixture.Application.Width);
        await ChooseFolderAsync(ChooseFolder);
        foreach (var locator in new[] { FolderButton, Address, Go, Browser })
        {
            await Expect(locator).ToBeVisibleAsync();
            var box = await locator.BoundingBoxAsync();
            box.X.Should().BeGreaterThanOrEqualTo(0);
            (box.X + box.Width).Should().BeLessThanOrEqualTo(Fixture.Application.Width);
            (box.Y + box.Height).Should().BeLessThanOrEqualTo(Fixture.Application.Height);
        }
        // The browser takes the remaining height of the tall screen.
        (await Browser.BoundingBoxAsync()).Height.Should().BeGreaterThan(1200);
        await Address.FillAsync("example.com/portrait");
        await Go.ClickAsync();
        await WaitAsync(() => Fixture.Navigations.Count, count => count == 1);
    }
}
