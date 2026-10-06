using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.GameEngine.Host.Rendering;
using CodeBrix.Platform.PlayTest;
using SilverAssertions;
using SkiaSharp;
using Wolfenstein.Brix.Assets;
using Wolfenstein.Brix.Settings;
using Wolfenstein.Brix.ViewModels;
using Xunit;

namespace Wolfenstein.Brix.PlayTests;

// Opt-in: these need the shareware game data in the git-ignored Downloaded/Wolfenstein.Brix_assets folder
// and skip cleanly without it. The game runs live (70 Hz loop, real audio device), so the tests
// assert mode, focus and non-blank pixels only, and never send the game a key that could choose Quit.
public sealed class GameModeTests(AppFixture fixture) : WolfensteinTest(fixture)
{
    private const string NoGameData = "Needs the verified .WL1 files in the repository's git-ignored Downloaded/Wolfenstein.Brix_assets folder.";
    private const string NoGameZip = "Needs 1wolf14.zip in the repository's git-ignored Downloaded/Wolfenstein.Brix_assets folder.";
    public static bool HasGameData => AppFixture.HasGameData;
    public static bool HasGameZip => HasGameData && File.Exists(Path.Combine(AppFixture.GameDataDirectory, WolfensteinAssetCatalog.AssetFileName));

    [Fact(Skip = NoGameData, SkipUnless = nameof(HasGameData))]
    public async Task Verified_folder_starts_in_game_mode()
    {
        await Fixture.ResetAsync(null, AppFixture.GameDataDirectory);
        await Expect(Canvas).ToBeVisibleAsync();
        await Expect(ChooseFolder).ToBeHiddenAsync();
        await Expect(Address).ToBeHiddenAsync();
        (await Page.EvaluateAsync(() => Model.IsGameMode)).Should().BeTrue();
        // Game Mode never starts the embedded browser.
        (await Page.EvaluateAsync(() => Model.NavigateToUrl == null)).Should().BeTrue();
    }

    [Fact(Skip = NoGameData, SkipUnless = nameof(HasGameData))]
    public async Task Picking_folder_with_verified_game_files_skips_browser()
    {
        Fixture.Application.FilePickers.EnqueueFolder(AppFixture.GameDataDirectory);
        await ChooseFolder.ClickAsync();
        await Expect(Canvas).ToBeVisibleAsync();
        await Expect(Address).ToBeHiddenAsync();
        SettingsService.Get<string>(MainViewModel.AssetsFolderKey).Should().Be(AppFixture.GameDataDirectory);
        (await Page.EvaluateAsync(() => Fixture.CompletedNavigations)).Should().Be(0);
        Fixture.Requests.Should().BeEmpty();
    }

    [Fact(Skip = NoGameData, SkipUnless = nameof(HasGameData))]
    public async Task Game_canvas_takes_keyboard_focus()
    {
        await Fixture.ResetAsync(null, AppFixture.GameDataDirectory);
        await Expect(Canvas).ToBeVisibleAsync();
        await WaitAsync(() => Fixture.FocusedElement() is GameSurfaceCanvas, focused => focused);
    }

    [Fact(Skip = NoGameData, SkipUnless = nameof(HasGameData))]
    public async Task Click_on_canvas_returns_focus_to_canvas()
    {
        await Fixture.ResetAsync(null, AppFixture.GameDataDirectory);
        await WaitAsync(() => Fixture.FocusedElement() is GameSurfaceCanvas, focused => focused);
        // Move focus off the canvas, as another element taking it would.
        await Page.EvaluateAsync(() =>
        {
            Fixture.View.IsTabStop = true;
            Fixture.View.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        });
        await WaitAsync(() => Fixture.FocusedElement() is GameSurfaceCanvas, focused => !focused);
        await Canvas.ClickAsync();
        await WaitAsync(() => Fixture.FocusedElement() is GameSurfaceCanvas, focused => focused);
    }

    [Fact(Skip = NoGameData, SkipUnless = nameof(HasGameData))]
    public async Task Game_canvas_renders_non_blank_frame()
    {
        await Fixture.ResetAsync(null, AppFixture.GameDataDirectory);
        await Expect(Canvas).ToBeVisibleAsync();
        var bounds = await Canvas.BoundingBoxAsync();
        // The live loop presents frames on its own clock; poll real screenshots until one shows
        // the title screen's many colors rather than the empty canvas.
        var elapsed = Stopwatch.StartNew();
        int colors;
        do
        {
            using var bitmap = SKBitmap.Decode(await Page.ScreenshotAsync());
            var seen = new HashSet<SKColor>();
            for (var y = (int)bounds.Y; y < bounds.Y + bounds.Height; y += 7)
                for (var x = (int)bounds.X; x < bounds.X + bounds.Width; x += 7)
                    seen.Add(bitmap.GetPixel(x, y));
            colors = seen.Count;
        }
        while (colors < 32 && elapsed.Elapsed < TimeSpan.FromSeconds(15));
        colors.Should().BeGreaterThanOrEqualTo(32);
    }

    [Fact(Skip = NoGameZip, SkipUnless = nameof(HasGameZip))]
    public async Task Known_good_zip_installs_and_enters_game_mode()
    {
        var folder = Path.Combine(Fixture.ScratchDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Fixture.Application.FilePickers.EnqueueFolder(folder);
        await ChooseFolder.ClickAsync();
        await Expect(FolderButton).ToHaveTextAsync(folder);
        await Fixture.WaitForStartPageAsync();
        var target = await Fixture.AcceptDownloadAsync();
        File.Copy(Path.Combine(AppFixture.GameDataDirectory, WolfensteinAssetCatalog.AssetFileName), target, overwrite: true);
        await Page.EvaluateAsync(() => Model.OnAssetDownloadCompleted(target));
        await Expect(Canvas).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Overlay).ToBeHiddenAsync();
        await Expect(Dialog).ToHaveCountAsync(0);
        WolfensteinAssetPipeline.VerifyInstalledAssets(folder).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(target)).Should().BeFalse();
    }
}
