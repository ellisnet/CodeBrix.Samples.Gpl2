using System;
using System.IO;
using System.Linq;
using Wolfenstein.Brix.Assets;
using Wolfenstein.Brix.Assets.Models;
using SilverAssertions;
using Xunit;

namespace Wolfenstein.Brix.Assets.Tests;

public class WolfensteinAssetPipelineTests : IDisposable
{
    readonly string root;

    public WolfensteinAssetPipelineTests()
    {
        root = Path.Combine(Path.GetTempPath(), "wolfenstein-brix-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
    }

    // The known-good 1wolf14.zip lives in the repo's Downloaded folder (not
    // committed - the assets are not freely distributable), so the tests that
    // need the real file skip cleanly when it is not present.
    static string FindKnownGoodZip()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, "Downloaded", "wolfenstein_assets", "1wolf14.zip");
            if (File.Exists(candidate))
                return candidate;
            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }

    static string RequireKnownGoodZip()
    {
        var zipPath = FindKnownGoodZip();
        Assert.SkipWhen(zipPath == null,
            "The known-good 1wolf14.zip is not present under the repo's Downloaded folder.");
        return zipPath;
    }

    [Fact]
    public void VerifyDownloadedZip_accepts_the_known_good_file()
    {
        //Arrange
        var zipPath = RequireKnownGoodZip();

        //Act + Assert (no exception)
        WolfensteinAssetPipeline.VerifyDownloadedZip(zipPath, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void VerifyDownloadedZip_rejects_a_wrong_file_as_a_verification_failure()
    {
        //Arrange
        var wrongFile = Path.Combine(root, "1wolf14.zip");
        File.WriteAllText(wrongFile, "definitely not the real shareware distribution");

        //Act
        Action act = () => WolfensteinAssetPipeline.VerifyDownloadedZip(wrongFile, TestContext.Current.CancellationToken);

        //Assert
        act.Should().Throw<AssetPipelineException>()
            .Which.Stage.Should().Be(AssetStage.Verifying);
    }

    [Fact]
    public void ExtractAssets_produces_only_the_wanted_verified_files()
    {
        //Arrange
        var zipPath = RequireKnownGoodZip();
        var assetsFolder = Path.Combine(root, "assets");

        //Act
        WolfensteinAssetPipeline.ExtractAssets(zipPath, assetsFolder, TestContext.Current.CancellationToken);

        //Assert — exactly the eight verified .WL1 files plus VENDOR.DOC; the
        // DOS executables were skipped without ever touching disk.
        var files = Directory.GetFiles(assetsFolder).Select(Path.GetFileName).OrderBy(name => name).ToArray();
        files.Length.Should().Be(9);
        files.Count(name => name.EndsWith(".WL1", StringComparison.OrdinalIgnoreCase)).Should().Be(8);
        files.Should().Contain(WolfensteinAssetCatalog.VendorDocFileName);
        files.Any(name => name.EndsWith(".EXE", StringComparison.OrdinalIgnoreCase)).Should().BeFalse();
        new FileInfo(Path.Combine(assetsFolder, WolfensteinAssetCatalog.VendorDocFileName)).Length
            .Should().BeGreaterThan(0);
        WolfensteinAssetPipeline.VerifyInstalledAssets(assetsFolder).Should().BeTrue();
    }

    [Fact]
    public void Vendor_doc_is_stored_but_never_gates_verification()
    {
        //Arrange
        var zipPath = RequireKnownGoodZip();
        var assetsFolder = Path.Combine(root, "assets");
        WolfensteinAssetPipeline.ExtractAssets(zipPath, assetsFolder, TestContext.Current.CancellationToken);

        //Act — a deleted VENDOR.DOC must not block Game Mode.
        File.Delete(Path.Combine(assetsFolder, WolfensteinAssetCatalog.VendorDocFileName));

        //Assert
        WolfensteinAssetPipeline.VerifyInstalledAssets(assetsFolder).Should().BeTrue();
    }

    [Fact]
    public void VerifyInstalledAssets_is_false_for_missing_or_junk_files()
    {
        //Arrange — empty folder: nothing to verify.
        var assetsFolder = Path.Combine(root, "assets");
        Directory.CreateDirectory(assetsFolder);

        //Assert
        WolfensteinAssetPipeline.VerifyInstalledAssets(assetsFolder).Should().BeFalse();
        WolfensteinAssetPipeline.VerifyInstalledAssets(null).Should().BeFalse();
        WolfensteinAssetPipeline.VerifyInstalledAssets(Path.Combine(root, "no-such-folder")).Should().BeFalse();

        //Arrange — files named right but with the wrong content.
        foreach (var name in WolfensteinAssetCatalog.KnownWl1Files.Keys)
        {
            File.WriteAllText(Path.Combine(assetsFolder, name), "not game data");
        }

        //Assert
        WolfensteinAssetPipeline.VerifyInstalledAssets(assetsFolder).Should().BeFalse();
    }

    [Fact]
    public void VerifyInstalledAssets_is_false_after_a_single_byte_flip()
    {
        //Arrange
        var zipPath = RequireKnownGoodZip();
        var assetsFolder = Path.Combine(root, "assets");
        WolfensteinAssetPipeline.ExtractAssets(zipPath, assetsFolder, TestContext.Current.CancellationToken);
        var vswapPath = Path.Combine(assetsFolder, "VSWAP.WL1");

        //Act — flip one byte in the middle of the biggest game file.
        using (var stream = new FileStream(vswapPath, FileMode.Open, FileAccess.ReadWrite))
        {
            stream.Position = stream.Length / 2;
            var original = stream.ReadByte();
            stream.Position = stream.Length / 2;
            stream.WriteByte((byte) (original ^ 0xFF));
        }

        //Assert
        WolfensteinAssetPipeline.VerifyInstalledAssets(assetsFolder).Should().BeFalse();
    }
}
