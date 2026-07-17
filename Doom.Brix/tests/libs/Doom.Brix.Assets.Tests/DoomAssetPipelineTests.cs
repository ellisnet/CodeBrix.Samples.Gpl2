using System;
using System.IO;
using Doom.Brix.Assets;
using Doom.Brix.Assets.Models;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Assets.Tests;

public class DoomAssetPipelineTests : IDisposable
{
    readonly string root;

    public DoomAssetPipelineTests()
    {
        root = Path.Combine(Path.GetTempPath(), "doom-brix-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
    }

    // The known-good doom19s.zip lives in the repo's Downloaded folder (not
    // committed - the assets are not freely distributable), so the tests that
    // need the real file skip cleanly when it is not present.
    static string FindKnownGoodZip()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, "Downloaded", "doom_assets", "doom19s.zip");
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
            "The known-good doom19s.zip is not present under the repo's Downloaded folder.");
        return zipPath;
    }

    [Fact]
    public void VerifyDownloadedZip_accepts_the_known_good_file()
    {
        //Arrange
        var zipPath = RequireKnownGoodZip();

        //Act + Assert (no exception)
        DoomAssetPipeline.VerifyDownloadedZip(zipPath, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void VerifyDownloadedZip_rejects_a_wrong_file_as_a_verification_failure()
    {
        //Arrange
        var wrongFile = Path.Combine(root, "doom19s.zip");
        File.WriteAllText(wrongFile, "definitely not the real shareware distribution");

        //Act
        Action act = () => DoomAssetPipeline.VerifyDownloadedZip(wrongFile, TestContext.Current.CancellationToken);

        //Assert
        act.Should().Throw<AssetPipelineException>()
            .Which.Stage.Should().Be(AssetStage.Verifying);
    }

    [Fact]
    public void ExtractAssets_produces_a_wad_that_passes_installed_verification()
    {
        //Arrange
        var zipPath = RequireKnownGoodZip();
        var assetsFolder = Path.Combine(root, "assets");

        //Act
        DoomAssetPipeline.ExtractAssets(zipPath, assetsFolder, TestContext.Current.CancellationToken);

        //Assert
        File.Exists(Path.Combine(assetsFolder, DoomAssetCatalog.WadFileName)).Should().BeTrue();
        DoomAssetPipeline.VerifyInstalledAssets(assetsFolder).Should().BeTrue();
        // No leftovers beside the WAD: extraction is a single-file, no-temp affair.
        Directory.GetFiles(assetsFolder).Length.Should().Be(1);
    }

    [Fact]
    public void VerifyInstalledAssets_is_false_for_a_missing_or_tampered_wad()
    {
        //Arrange — empty folder: nothing to verify.
        var assetsFolder = Path.Combine(root, "assets");
        Directory.CreateDirectory(assetsFolder);

        //Assert
        DoomAssetPipeline.VerifyInstalledAssets(assetsFolder).Should().BeFalse();
        DoomAssetPipeline.VerifyInstalledAssets(null).Should().BeFalse();
        DoomAssetPipeline.VerifyInstalledAssets(Path.Combine(root, "no-such-folder")).Should().BeFalse();

        //Arrange — a wad-named file with the wrong content.
        File.WriteAllText(Path.Combine(assetsFolder, DoomAssetCatalog.WadFileName), "IWAD but not really");

        //Assert
        DoomAssetPipeline.VerifyInstalledAssets(assetsFolder).Should().BeFalse();
    }

    [Fact]
    public void VerifyInstalledAssets_is_false_after_a_single_byte_flip()
    {
        //Arrange
        var zipPath = RequireKnownGoodZip();
        var assetsFolder = Path.Combine(root, "assets");
        DoomAssetPipeline.ExtractAssets(zipPath, assetsFolder, TestContext.Current.CancellationToken);
        var wadPath = Path.Combine(assetsFolder, DoomAssetCatalog.WadFileName);

        //Act — flip one byte in the middle of the file.
        using (var stream = new FileStream(wadPath, FileMode.Open, FileAccess.ReadWrite))
        {
            stream.Position = stream.Length / 2;
            var original = stream.ReadByte();
            stream.Position = stream.Length / 2;
            stream.WriteByte((byte) (original ^ 0xFF));
        }

        //Assert
        DoomAssetPipeline.VerifyInstalledAssets(assetsFolder).Should().BeFalse();
    }
}
