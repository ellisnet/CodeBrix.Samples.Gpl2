using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.PlayTest;
using SilverAssertions;
using Wolfenstein.Brix.Assets;
using Wolfenstein.Brix.Settings;
using Wolfenstein.Brix.ViewModels;
using Xunit;

namespace Wolfenstein.Brix.PlayTests;

public sealed class ApplicationTests(AppFixture fixture) : WolfensteinTest(fixture)
{
    private const string Hint = "HINT: Download the “Wolfenstein 3D v1.4g Shareware Episode (856,401 bytes)” file from this page.";

    [Fact]
    public async Task First_run_shows_folder_setup_in_assets_mode()
    {
        await Expect(Page.GetByText("SELECT YOUR ASSETS FOLDER", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(ChooseFolder).ToHaveTextAsync("CHOOSE ASSETS FOLDER…");
        await Expect(FolderButton).ToHaveTextAsync("Choose assets folder…");
        await Expect(Address).ToBeHiddenAsync();
        await Expect(Canvas).ToBeHiddenAsync();
        await Expect(Overlay).ToBeHiddenAsync();
        (await Page.EvaluateAsync(() => Model.IsGameMode)).Should().BeFalse();
    }

    [Fact]
    public async Task Choosing_folder_shows_browser_area_and_hint()
    {
        var folder = await ChooseFolderAsync(ChooseFolder);
        await Expect(ChooseFolder).ToBeHiddenAsync();
        await Expect(Page.GetByText(Hint, new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Address).ToHaveValueAsync(Fixture.StartUrl);
        await Expect(Go).ToBeEnabledAsync();
        await Expect(Browser).ToBeVisibleAsync();
        SettingsService.Get<string>(MainViewModel.AssetsFolderKey).Should().Be(folder);
        Fixture.Application.FilePickers.FolderRequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Cancelled_folder_picker_keeps_folder_setup()
    {
        Fixture.Application.FilePickers.EnqueueFolder(null);
        await ChooseFolder.ClickAsync();
        await WaitAsync(() => Fixture.Application.FilePickers.FolderRequestCount, count => count == 1);
        await Expect(ChooseFolder).ToBeVisibleAsync();
        await Expect(FolderButton).ToHaveTextAsync("Choose assets folder…");
        await Expect(Address).ToBeHiddenAsync();
        SettingsService.HasValue(MainViewModel.AssetsFolderKey).Should().BeFalse();
    }

    [Fact]
    public async Task Chosen_folder_is_remembered_and_restored_on_new_page()
    {
        var folder = await ChooseFolderAsync(ChooseFolder);
        Fixture.Requests.Clear();
        await Fixture.LoadPageAsync();
        await Expect(FolderButton).ToHaveTextAsync(folder);
        await Expect(ChooseFolder).ToBeHiddenAsync();
        await Expect(Address).ToBeVisibleAsync();
        // The restored page opens its start page by itself, without a new folder choice.
        await WaitAsync(() => Fixture.Requests.Contains("/game/Wolfenstein_3D.html"), opened => opened);
        Fixture.Application.FilePickers.FolderRequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Remembered_folder_deleted_between_sessions_asks_again()
    {
        var folder = await ChooseFolderAsync(ChooseFolder);
        Directory.Delete(folder);
        await Fixture.LoadPageAsync();
        await Expect(ChooseFolder).ToBeVisibleAsync();
        await Expect(FolderButton).ToHaveTextAsync("Choose assets folder…");
        await Expect(Address).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Folder_bar_button_changes_the_folder()
    {
        await ChooseFolderAsync(ChooseFolder);
        var other = await ChooseFolderAsync(FolderButton, "other");
        SettingsService.Get<string>(MainViewModel.AssetsFolderKey).Should().Be(other);
        await Expect(Address).ToBeVisibleAsync();
        // The browser keeps its page; only the first folder choice opens the start page.
        (await Page.EvaluateAsync(() => Fixture.Navigations.Count)).Should().Be(0);
        Fixture.Requests.Count(request => request == "/game/Wolfenstein_3D.html").Should().Be(1);
    }

    [Fact]
    public async Task Folder_without_game_files_opens_the_classicdosgames_start_page()
    {
        new AssetsBrowserOptions().StartUrl.Should().Be(WolfensteinAssetCatalog.DefaultBrowseUrl);
        await ChooseFolderAsync(ChooseFolder);
        await WaitAsync(() => Fixture.Requests.Contains("/game/Wolfenstein_3D.html"), opened => opened);
        await Expect(Address).ToHaveValueAsync(Fixture.StartUrl);
        (await Page.EvaluateAsync(() => Model.IsGameMode)).Should().BeFalse();
    }

    [Fact]
    public async Task Go_navigates_to_https_normalized_address()
    {
        await ChooseFolderAsync(ChooseFolder);
        await Address.FillAsync("  example.com/wolf3d  ");
        await Go.ClickAsync();
        // Recorded only: the fixture lets the browser open loopback pages alone.
        await WaitAsync(() => Fixture.Navigations.Count, count => count == 1);
        (await Page.EvaluateAsync(() => Fixture.Navigations[0])).Should().Be("https://example.com/wolf3d");
    }

    [Fact]
    public async Task Enter_in_address_bar_navigates_like_go()
    {
        await ChooseFolderAsync(ChooseFolder);
        await Address.FillAsync("http://example.com/mirrors");
        await Address.PressAsync("Enter");
        await WaitAsync(() => Fixture.Navigations.Count, count => count == 1);
        (await Page.EvaluateAsync(() => Fixture.Navigations[0])).Should().Be("http://example.com/mirrors");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("ftp://example.com/1wolf14.zip")]
    [InlineData("file:///1wolf14.zip")]
    public async Task Invalid_or_blank_address_does_not_navigate(string address)
    {
        await ChooseFolderAsync(ChooseFolder);
        await Address.FillAsync(address);
        await Go.ClickAsync();
        await Address.PressAsync("Enter");
        (await Page.EvaluateAsync(() => Fixture.Navigations.Count)).Should().Be(0);
        await Expect(Address).ToHaveValueAsync(address);
    }

    [Fact]
    public async Task Unrecognized_download_is_cancelled_with_dialog()
    {
        await ChooseFolderAsync(ChooseFolder);
        var url = new Uri(Fixture.Origin, "files/setup.exe").AbsoluteUri;
        (await Page.EvaluateAsync(() => Model.HandleDownloadStarting(url, "setup.exe", out _))).Should().BeFalse();
        await Expect(Dialog).ToContainTextAsync("Download Canceled");
        await Expect(Dialog).ToContainTextAsync("please download a file named '1wolf14.zip'");
        await Expect(Overlay).ToBeHiddenAsync();
        await CloseDialogAsync();
        await Expect(Address).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Accepted_download_shows_overlay_stage_and_percent()
    {
        await ChooseFolderAsync(ChooseFolder);
        var target = await Fixture.AcceptDownloadAsync();
        Path.GetFileName(target).Should().Be(WolfensteinAssetCatalog.AssetFileName);
        Directory.Exists(Path.GetDirectoryName(target)).Should().BeTrue();
        await Expect(Overlay).ToBeVisibleAsync();
        await Expect(Overlay).ToContainTextAsync("GET PSYCHED!");
        await Expect(Page.GetByTestId("DownloadStage")).ToHaveTextAsync("Downloading 1wolf14.zip…");
        await Expect(Page.GetByTestId("DownloadPercent")).ToHaveTextAsync("0%");
        await Page.EvaluateAsync(() => Model.OnAssetDownloadProgress(WolfensteinAssetCatalog.AssetZipSize / 2));
        await Expect(Page.GetByTestId("DownloadPercent")).ToHaveTextAsync("50%");
        // The authentic archive has an odd byte count, so half of it is just under 50%.
        (await Page.EvaluateAsync(() => Model.DownloadProgress)).Should().BeApproximately(50d, 0.01);
        await Expect(Dialog).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Second_download_while_downloading_is_refused()
    {
        await ChooseFolderAsync(ChooseFolder);
        await Fixture.AcceptDownloadAsync();
        await Page.EvaluateAsync(() => Model.OnAssetDownloadProgress(WolfensteinAssetCatalog.AssetZipSize / 4));
        (await Fixture.AcceptDownloadAsync()).Should().BeNull();
        await Expect(Page.GetByTestId("DownloadPercent")).ToHaveTextAsync("25%");
        await Expect(Overlay).ToBeVisibleAsync();
        await Expect(Dialog).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Corrupt_1wolf14_zip_fails_verification_with_dialog_and_stays_in_assets_mode()
    {
        var folder = await ChooseFolderAsync(ChooseFolder);
        var target = await Fixture.AcceptDownloadAsync();
        await File.WriteAllTextAsync(target, "This is not the Wolfenstein 3D shareware archive.", TestContext.Current.CancellationToken);
        await Page.EvaluateAsync(() => Model.OnAssetDownloadCompleted(target));
        await Expect(Dialog).ToContainTextAsync("Assets Setup Failed");
        await Expect(Dialog).ToContainTextAsync("The verification of the game assets from the selected file failed");
        // The overlay stays behind the dialog until it is dismissed.
        await CloseDialogAsync();
        await Expect(Overlay).ToBeHiddenAsync();
        await Expect(Address).ToBeVisibleAsync();
        (await Page.EvaluateAsync(() => Model.IsGameMode)).Should().BeFalse();
        Directory.EnumerateFileSystemEntries(folder).Should().BeEmpty();
        // The pipeline removes its temporary download folder, success or failure.
        await WaitAsync(() => Directory.Exists(Path.GetDirectoryName(target)), exists => !exists);
    }

    [Fact]
    public async Task Interrupted_download_reports_download_failure()
    {
        await ChooseFolderAsync(ChooseFolder);
        await Fixture.AcceptDownloadAsync();
        await Expect(Overlay).ToBeVisibleAsync();
        await Page.EvaluateAsync(() => Model.OnAssetDownloadFailed());
        await Expect(Dialog).ToContainTextAsync("The download of the game assets from the selected file failed");
        await Expect(Overlay).ToBeHiddenAsync();
        await CloseDialogAsync();
        await Expect(Address).ToBeVisibleAsync();
    }
}
