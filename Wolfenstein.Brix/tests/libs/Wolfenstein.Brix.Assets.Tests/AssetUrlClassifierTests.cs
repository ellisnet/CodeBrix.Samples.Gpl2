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
}
