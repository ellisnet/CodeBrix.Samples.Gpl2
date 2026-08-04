using Wolfenstein.Brix.Assets;
using SilverAssertions;
using Xunit;

namespace Wolfenstein.Brix.Assets.Tests;

public class AssetUrlClassifierTests
{
    [Theory]
    [InlineData("https://www.classicdosgames.com/files/games/apogee/1wolf14.zip")]
    [InlineData("http://www.classicdosgames.com/files/games/apogee/1WOLF14.ZIP")]
    [InlineData("https://example.com/mirror/1wolf14.zip?token=abc123")]
    [InlineData("http://example.com/1wolf14.zip#fragment")]
    [InlineData("https://example.com/a%20folder/1wolf14.zip")]
    public void IsAssetFileUrl_recognizes_the_asset_file(string url) =>
        AssetUrlClassifier.IsAssetFileUrl(url).Should().BeTrue();

    [Theory]
    [InlineData("https://www.classicdosgames.com/game/Wolfenstein_3D.html")]
    [InlineData("https://example.com/1wolf14.zip.torrent")]
    [InlineData("https://example.com/other-file.zip")]
    [InlineData("https://example.com/")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void IsAssetFileUrl_rejects_everything_else(string url) =>
        AssetUrlClassifier.IsAssetFileUrl(url).Should().BeFalse();

    [Theory]
    [InlineData("https://example.com/some-archive.zip")]
    [InlineData("https://example.com/setup.EXE")]
    [InlineData("https://example.com/files/game.7z?mirror=2")]
    [InlineData("https://example.com/data.tar")]
    [InlineData("https://example.com/image.iso")]
    public void IsDownloadLikeUrl_flags_file_downloads(string url) =>
        AssetUrlClassifier.IsDownloadLikeUrl(url).Should().BeTrue();

    [Theory]
    [InlineData("https://www.classicdosgames.com/game/Wolfenstein_3D.html")]
    [InlineData("https://example.com/page.html")]
    [InlineData("https://example.com/article.php?id=7")]
    [InlineData("https://example.com/")]
    [InlineData("not a url")]
    [InlineData(null)]
    public void IsDownloadLikeUrl_passes_page_navigation(string url) =>
        AssetUrlClassifier.IsDownloadLikeUrl(url).Should().BeFalse();

    [Fact]
    public void The_asset_file_is_also_download_like()
    {
        //Assert — the classifier order matters in the caller: asset check first.
        AssetUrlClassifier.IsDownloadLikeUrl("https://example.com/1wolf14.zip").Should().BeTrue();
    }

    [Fact]
    public void GetUrlFileName_ignores_query_and_fragment()
    {
        //Assert
        AssetUrlClassifier.GetUrlFileName("https://example.com/dir/file.zip?a=1#top").Should().Be("file.zip");
        AssetUrlClassifier.GetUrlFileName("https://example.com/dir/").Should().BeNull();
    }

    [Theory]
    [InlineData("https://example.com/mirror/1wolf14.zip", "1wolf14.zip")]
    [InlineData("https://example.com/mirror/1wolf14.zip", "1wolf14 (3).zip")]
    [InlineData("https://example.com/download.php?id=42", "1wolf14.zip")]
    [InlineData("https://example.com/download.php?id=42", "1WOLF14.ZIP")]
    [InlineData("https://example.com/mirror/1wolf14.zip", "unrelated-name.zip")]
    [InlineData("https://example.com/mirror/1wolf14.zip", null)]
    public void IsAssetDownload_accepts_by_suggested_name_or_url(string url, string suggestedFileName) =>
        AssetUrlClassifier.IsAssetDownload(url, suggestedFileName).Should().BeTrue();

    [Theory]
    [InlineData("https://example.com/other-file.zip", "other-file.zip")]
    [InlineData("https://example.com/download.php?id=42", "setup.exe")]
    [InlineData("https://example.com/download.php?id=42", null)]
    [InlineData(null, null)]
    public void IsAssetDownload_refuses_everything_else(string url, string suggestedFileName) =>
        AssetUrlClassifier.IsAssetDownload(url, suggestedFileName).Should().BeFalse();

    [Theory]
    [InlineData("1wolf14 (1).zip", "1wolf14.zip")]
    [InlineData("1wolf14 (27).zip", "1wolf14.zip")]
    [InlineData("1wolf14.zip", "1wolf14.zip")]
    [InlineData("1wolf14 (x).zip", "1wolf14 (x).zip")]
    [InlineData("1wolf14 ().zip", "1wolf14 ().zip")]
    [InlineData(" (1).zip", " (1).zip")]
    [InlineData("archive (1)", "archive")]
    [InlineData(null, null)]
    public void StripCollisionSuffix_removes_only_the_browser_suffix(string fileName, string expected) =>
        AssetUrlClassifier.StripCollisionSuffix(fileName).Should().Be(expected);
}
