using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.Platform.PlayTest;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace Wolfenstein.Brix.PlayTests;

public sealed class BrowserTests(AppFixture fixture) : WolfensteinTest(fixture)
{
    [Fact]
    public async Task Navigation_completed_updates_address_bar()
    {
        await ChooseFolderAsync(ChooseFolder);
        var mirrors = new Uri(Fixture.Origin, "game/downloads.html").AbsoluteUri;
        await Address.FillAsync(mirrors);
        await Go.ClickAsync();
        await WaitAsync(() => Fixture.Requests.Contains("/game/downloads.html"), opened => opened);
        await WaitAsync(() => Fixture.CompletedNavigations, count => count == 2);
        await Expect(Address).ToHaveValueAsync(mirrors);
    }

    [Fact]
    public async Task Clicking_link_to_1wolf14_zip_triggers_download_policy()
    {
        await ChooseFolderAsync(ChooseFolder);
        await ClickLinkAsync("asset");
        await WaitAsync(() => Fixture.Requests.Contains("/files/1wolf14.zip"), requested => requested);
        // The fixture serves a file that is not the authentic archive, so verification rejects it.
        await Expect(Dialog).ToContainTextAsync("The verification of the game assets from the selected file failed");
        await CloseDialogAsync();
        await Expect(Overlay).ToBeHiddenAsync();
        (await Page.EvaluateAsync(() => Model.IsGameMode)).Should().BeFalse();
    }

    [Fact]
    public async Task Clicking_non_asset_download_is_cancelled()
    {
        await ChooseFolderAsync(ChooseFolder);
        await ClickLinkAsync("other");
        await Expect(Dialog).ToContainTextAsync("Download Canceled");
        await Expect(Overlay).ToBeHiddenAsync();
        await CloseDialogAsync();
        await Expect(Address).ToBeVisibleAsync();
    }

    // Use the real WebView API only to measure the link. The click travels through
    // PlayTest's pointer input and the control's native browser input bridge.
    private async Task ClickLinkAsync(string id)
    {
        var operation = await Page.EvaluateAsync(() =>
            ((WebView2)Fixture.View.FindName("Browser")).ExecuteScriptAsync(
                "(() => { const r = document.getElementById('" + id + "').getBoundingClientRect(); return {x:r.x+r.width/2,y:r.y+r.height/2}; })()"));
        using var position = JsonDocument.Parse(await operation);
        var bounds = await Browser.BoundingBoxAsync();
        await Page.Mouse.ClickAsync(bounds.X + (float)position.RootElement.GetProperty("x").GetDouble(),
            bounds.Y + (float)position.RootElement.GetProperty("y").GetDouble());
    }
}
