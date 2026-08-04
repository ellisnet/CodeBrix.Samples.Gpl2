using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Compression.Checksum;
using CodeBrix.Compression.Zip;
using Doom.Brix.Assets.Internal;
using Doom.Brix.Assets.Models;

namespace Doom.Brix.Assets;

/// <summary>
/// The whole "get the game assets installed" pipeline for Doom.Brix:
/// download doom19s.zip to a temp folder, verify its authenticity (size,
/// CRC-32, MD5), extract the shareware v1.9 DOOM1.WAD from the split
/// self-extracting archive inside it (rejoined entirely in memory — no
/// intermediate files ever touch disk), verify the WAD against the known
/// historical values and its IWAD structure, and clean the temp folder up.
/// </summary>
public static class DoomAssetPipeline
{
    private const int CopyBufferSize = 81920;

    private static readonly AssetDownloader Downloader = new AssetDownloader();

    /// <summary>
    /// Runs the full pipeline: download → verify → extract into the user's
    /// assets folder. The temp download is deleted afterwards, success or
    /// failure. Failures throw <see cref="AssetPipelineException"/> carrying
    /// the stage that failed.
    /// </summary>
    public static async Task InstallFromUrlAsync(string url, string referrerUrl, string assetsFolderPath,
        IProgress<AssetProgress> progress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assetsFolderPath))
        {
            throw new ArgumentException("An assets folder path is required", nameof(assetsFolderPath));
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "Doom.Brix", Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        var zipPath = Path.Combine(tempDirectory, DoomAssetCatalog.AssetFileName);
        try
        {
            progress?.Report(new AssetProgress(AssetStage.Downloading, 0d));
            try
            {
                await Downloader.DownloadFileAsync(url, referrerUrl, zipPath, DoomAssetCatalog.AssetZipSize,
                    new StageProgress(progress, AssetStage.Downloading), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new AssetPipelineException(AssetStage.Downloading,
                    $"The game asset file could not be downloaded from '{url}'.", ex);
            }

            await VerifyAndExtractAsync(zipPath, assetsFolderPath, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try { Directory.Delete(tempDirectory, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// A fresh temp target path (ending in <see cref="DoomAssetCatalog.AssetFileName"/>,
    /// directory created) for a download the browser performs itself; hand the
    /// downloaded file to <see cref="InstallDownloadedZipAsync"/>, which cleans
    /// the directory up again.
    /// </summary>
    public static string CreateTempDownloadPath()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "Doom.Brix", Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return Path.Combine(tempDirectory, DoomAssetCatalog.AssetFileName);
    }

    /// <summary>
    /// Runs the pipeline's verify → extract stages over an already-downloaded
    /// copy of the asset zip (the browser downloaded it to a
    /// <see cref="CreateTempDownloadPath"/> target). The zip's containing
    /// directory is deleted afterwards, success or failure. Failures throw
    /// <see cref="AssetPipelineException"/> carrying the stage that failed.
    /// </summary>
    public static async Task InstallDownloadedZipAsync(string zipPath, string assetsFolderPath,
        IProgress<AssetProgress> progress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assetsFolderPath))
        {
            throw new ArgumentException("An assets folder path is required", nameof(assetsFolderPath));
        }

        try
        {
            await VerifyAndExtractAsync(zipPath, assetsFolderPath, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(zipPath), recursive: true); } catch { /* best effort */ }
        }
    }

    // Hashing and inflating are pure CPU/disk work; keep them off the caller's (UI) thread.
    private static Task VerifyAndExtractAsync(string zipPath, string assetsFolderPath,
        IProgress<AssetProgress> progress, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            progress?.Report(new AssetProgress(AssetStage.Verifying, 0d));
            VerifyDownloadedZip(zipPath, cancellationToken);
            progress?.Report(new AssetProgress(AssetStage.Verifying, 1d));

            progress?.Report(new AssetProgress(AssetStage.Extracting, 0d));
            ExtractAssets(zipPath, assetsFolderPath, cancellationToken);
            progress?.Report(new AssetProgress(AssetStage.Extracting, 1d));
        }, cancellationToken);

    /// <summary>
    /// Verifies the downloaded zip is the authentic doom19s.zip (size,
    /// CRC-32 and MD5 all match the known-good values); throws
    /// <see cref="AssetPipelineException"/> when it is not.
    /// </summary>
    public static void VerifyDownloadedZip(string zipPath, CancellationToken cancellationToken = default)
    {
        (long Size, long Crc32, string Md5Hex) checks;
        try
        {
            checks = ChecksumHelper.ComputeFileChecksums(zipPath, new byte[CopyBufferSize], cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AssetPipelineException(AssetStage.Verifying,
                "The downloaded file could not be read for verification.", ex);
        }

        if (checks.Size != DoomAssetCatalog.AssetZipSize
            || checks.Crc32 != DoomAssetCatalog.AssetZipCrc32
            || checks.Md5Hex != DoomAssetCatalog.AssetZipMd5)
        {
            throw new AssetPipelineException(AssetStage.Verifying,
                $"The downloaded file is not the authentic {DoomAssetCatalog.AssetFileName} "
                + $"(size {checks.Size:N0}, CRC-32 {checks.Crc32:x8}, MD5 {checks.Md5Hex}).");
        }
    }

    /// <summary>
    /// Extracts the verified DOOM1.WAD from doom19s.zip into the assets
    /// folder: the two DOOMS_19 parts are streamed and rejoined in memory,
    /// the WAD entry inside the rebuilt self-extracting archive streams to
    /// disk in one pass computing CRC-32 and MD5 as it goes, and the result
    /// is checked against the archive's declared values, the known
    /// historical values, and the IWAD structure.
    /// </summary>
    public static void ExtractAssets(string zipPath, string assetsFolderPath, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(assetsFolderPath);
            var buffer = new byte[CopyBufferSize];
            var wadPath = Path.Combine(assetsFolderPath, DoomAssetCatalog.WadFileName);

            using (var archiveStream = ConcatenateParts(zipPath, buffer, cancellationToken))
            {
                ExtractWad(archiveStream, wadPath, buffer, cancellationToken);
            }

            if (!HasConsistentIwadStructure(wadPath))
            {
                throw new AssetPipelineException(AssetStage.Extracting,
                    $"'{wadPath}' does not have a consistent IWAD structure.");
            }
        }
        catch (AssetPipelineException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                "The game assets could not be extracted from the downloaded file.", ex);
        }
    }

    /// <summary>
    /// Whether the assets folder holds a fully verified set of game assets:
    /// a DOOM1.WAD matching the known shareware v1.9 size, CRC-32 and MD5,
    /// with a consistent IWAD structure. Never throws.
    /// </summary>
    public static bool VerifyInstalledAssets(string assetsFolderPath)
    {
        if (string.IsNullOrWhiteSpace(assetsFolderPath))
        {
            return false;
        }

        try
        {
            var wadPath = Path.Combine(assetsFolderPath, DoomAssetCatalog.WadFileName);
            if (!File.Exists(wadPath))
            {
                return false;
            }

            var checks = ChecksumHelper.ComputeFileChecksums(wadPath, new byte[CopyBufferSize]);
            return checks.Size == DoomAssetCatalog.WadSize
                && checks.Crc32 == DoomAssetCatalog.WadCrc32
                && checks.Md5Hex == DoomAssetCatalog.WadMd5
                && HasConsistentIwadStructure(wadPath);
        }
        catch
        {
            return false;
        }
    }

    // Streams the two DOOMS_19 part entries, in order, into one presized
    // MemoryStream — the "extract" and the "concatenate" in one motion, with
    // each part's CRC-32 verified against the zip's central directory during
    // the same copy pass. The result is the complete DOOMS_19.EXE image; the
    // PKSFX stub in front is harmless because ZipFile locates the central
    // directory from the END of the stream.
    private static MemoryStream ConcatenateParts(string zipPath, byte[] buffer, CancellationToken cancellationToken)
    {
        using var sourceZip = new ZipFile(zipPath);

        var parts = new ZipEntry[DoomAssetCatalog.ArchivePartNames.Length];
        long totalSize = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            int index = sourceZip.FindEntry(DoomAssetCatalog.ArchivePartNames[i], ignoreCase: true);
            if (index < 0)
            {
                throw new AssetPipelineException(AssetStage.Extracting,
                    $"Entry '{DoomAssetCatalog.ArchivePartNames[i]}' not found in '{zipPath}'.");
            }
            parts[i] = sourceZip[index];
            totalSize += parts[i].Size;
        }

        var archiveStream = new MemoryStream(checked((int) totalSize));

        foreach (ZipEntry part in parts)
        {
            var crc = new Crc32();
            using Stream partStream = sourceZip.GetInputStream(part);

            int bytesRead;
            while ((bytesRead = partStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                crc.Update(new ArraySegment<byte>(buffer, 0, bytesRead));
                archiveStream.Write(buffer, 0, bytesRead);
            }

            if (crc.Value != part.Crc)
            {
                throw new AssetPipelineException(AssetStage.Extracting,
                    $"CRC-32 mismatch for '{part.Name}': computed {crc.Value:x8}, declared {part.Crc:x8}.");
            }
        }

        archiveStream.Position = 0; // rewind - the next step reads it as a zip
        return archiveStream;
    }

    // Streams the DOOM1.WAD entry to disk in a single pass, folding the
    // CRC-32, the MD5 and the byte count into the one write loop, then checks
    // the result against the archive's own declared values AND the known
    // historical shareware v1.9 values — two genuinely independent checks.
    private static void ExtractWad(MemoryStream archiveStream, string wadPath, byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var archiveZip = new ZipFile(archiveStream);

        int index = archiveZip.FindEntry(DoomAssetCatalog.WadFileName, ignoreCase: true);
        if (index < 0)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                $"Entry '{DoomAssetCatalog.WadFileName}' not found in the rebuilt archive.");
        }

        ZipEntry entry = archiveZip[index];
        var crc = new Crc32();
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        long totalBytes = 0;

        using (Stream entryStream = archiveZip.GetInputStream(entry))
        using (FileStream outputStream = File.Create(wadPath))
        {
            int bytesRead;
            while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                crc.Update(new ArraySegment<byte>(buffer, 0, bytesRead));
                md5.AppendData(buffer, 0, bytesRead);
                outputStream.Write(buffer, 0, bytesRead);
                totalBytes += bytesRead;
            }
        }

        string md5Hex = Convert.ToHexStringLower(md5.GetHashAndReset());

        // Layer 1: does the output match what the archive itself declared?
        if (totalBytes != entry.Size || crc.Value != entry.Crc)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                "Extracted data does not match the archive's declared size/CRC-32.");
        }

        // Layer 2: does it match the historical record for shareware v1.9?
        if (totalBytes != DoomAssetCatalog.WadSize
            || crc.Value != DoomAssetCatalog.WadCrc32
            || md5Hex != DoomAssetCatalog.WadMd5)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                $"Extracted {DoomAssetCatalog.WadFileName} is not the known shareware v1.9 file "
                + $"(size {totalBytes:N0}, CRC-32 {crc.Value:x8}, MD5 {md5Hex}).");
        }
    }

    // A DOOM WAD begins with a 12-byte header: the 'IWAD' magic, the lump
    // count and the lump-directory offset; in a well-formed WAD the 16-bytes-
    // per-lump directory runs to the exact end of the file.
    private static bool HasConsistentIwadStructure(string wadPath)
    {
        using FileStream wadStream = File.OpenRead(wadPath);
        if (wadStream.Length < 12)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[12];
        wadStream.ReadExactly(header);

        string magic = Encoding.ASCII.GetString(header[..4]);
        int lumpCount = BinaryPrimitives.ReadInt32LittleEndian(header[4..8]);
        int directoryOffset = BinaryPrimitives.ReadInt32LittleEndian(header[8..12]);

        return magic == "IWAD" && directoryOffset + (16L * lumpCount) == wadStream.Length;
    }

    private sealed class StageProgress : IProgress<double>
    {
        private readonly IProgress<AssetProgress> _inner;
        private readonly AssetStage _stage;

        public StageProgress(IProgress<AssetProgress> inner, AssetStage stage)
        {
            _inner = inner;
            _stage = stage;
        }

        public void Report(double value) => _inner?.Report(new AssetProgress(_stage, value));
    }
}
