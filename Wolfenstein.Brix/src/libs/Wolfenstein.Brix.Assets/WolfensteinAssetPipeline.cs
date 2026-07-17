using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Compression.Checksum;
using CodeBrix.Compression.Dcl;
using CodeBrix.Compression.Zip;
using Wolfenstein.Brix.Assets.Internal;
using Wolfenstein.Brix.Assets.Models;

namespace Wolfenstein.Brix.Assets;

/// <summary>
/// The whole "get the game assets installed" pipeline for Wolfenstein.Brix:
/// download 1wolf14.zip to a temp folder, verify its authenticity (size,
/// CRC-32, MD5), pull the W3DSW14.SHR installer archive out of it entirely
/// in memory, walk the .SHR catalog decompressing ONLY the eight .WL1
/// game-data files plus VENDOR.DOC with DCL "implode" decompression (the
/// DOS executables are skipped without ever touching disk), verify the
/// .WL1 files against their documented sizes/MD5s and id Software's
/// structural signatures, and clean the temp folder up.
/// </summary>
public static class WolfensteinAssetPipeline
{
    private const int CopyBufferSize = 81920;

    // The W3DSW14.SHR catalog layout (reverse-engineered; little-endian):
    // a 58-byte global header holding the file count, then per file a
    // 168-byte record header followed by that file's DCL-compressed data.
    private const int GlobalHeaderSize = 58;
    private const int FileCountOffset = 16;
    private const int RecordHeaderSize = 168;
    private const int NameFieldSize = 128;
    private const int CompressedSizeOffset = 136;

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

        var tempDirectory = Path.Combine(Path.GetTempPath(), "Wolfenstein.Brix", Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        var zipPath = Path.Combine(tempDirectory, WolfensteinAssetCatalog.AssetFileName);
        try
        {
            progress?.Report(new AssetProgress(AssetStage.Downloading, 0d));
            try
            {
                await Downloader.DownloadFileAsync(url, referrerUrl, zipPath, WolfensteinAssetCatalog.AssetZipSize,
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

            // Hashing and decompression are pure CPU/disk work; keep them off the caller's (UI) thread.
            await Task.Run(() =>
            {
                progress?.Report(new AssetProgress(AssetStage.Verifying, 0d));
                VerifyDownloadedZip(zipPath, cancellationToken);
                progress?.Report(new AssetProgress(AssetStage.Verifying, 1d));

                progress?.Report(new AssetProgress(AssetStage.Extracting, 0d));
                ExtractAssets(zipPath, assetsFolderPath, cancellationToken);
                progress?.Report(new AssetProgress(AssetStage.Extracting, 1d));
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try { Directory.Delete(tempDirectory, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Verifies the downloaded zip is the authentic 1wolf14.zip (size,
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

        if (checks.Size != WolfensteinAssetCatalog.AssetZipSize
            || checks.Crc32 != WolfensteinAssetCatalog.AssetZipCrc32
            || checks.Md5Hex != WolfensteinAssetCatalog.AssetZipMd5)
        {
            throw new AssetPipelineException(AssetStage.Verifying,
                $"The downloaded file is not the authentic {WolfensteinAssetCatalog.AssetFileName} "
                + $"(size {checks.Size:N0}, CRC-32 {checks.Crc32:x8}, MD5 {checks.Md5Hex}).");
        }
    }

    /// <summary>
    /// Extracts the game files from the verified 1wolf14.zip into the assets
    /// folder: the W3DSW14.SHR installer archive streams into memory (its
    /// CRC-32 verified against the zip's central directory), the catalog is
    /// walked decompressing only the eight .WL1 files plus VENDOR.DOC, each
    /// .WL1 is checked against its documented size and MD5 as it streams to
    /// disk, and the id Software structural signatures get a final look.
    /// </summary>
    public static void ExtractAssets(string zipPath, string assetsFolderPath, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(assetsFolderPath);
            var buffer = new byte[CopyBufferSize];

            byte[] shrArchive = ExtractShrFromZip(zipPath);
            ExtractCatalog(shrArchive, assetsFolderPath, buffer, cancellationToken);
            EnsureWl1Structure(assetsFolderPath);
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
    /// all eight .WL1 files matching their documented shareware v1.4 sizes
    /// and MD5s, with the id Software structural signatures intact.
    /// VENDOR.DOC is stored but never gates verification. Never throws.
    /// </summary>
    public static bool VerifyInstalledAssets(string assetsFolderPath)
    {
        if (string.IsNullOrWhiteSpace(assetsFolderPath))
        {
            return false;
        }

        try
        {
            var buffer = new byte[CopyBufferSize];
            foreach (var known in WolfensteinAssetCatalog.KnownWl1Files)
            {
                var filePath = Path.Combine(assetsFolderPath, known.Key);
                if (!File.Exists(filePath))
                {
                    return false;
                }

                var checks = ChecksumHelper.ComputeFileChecksums(filePath, buffer);
                if (checks.Size != known.Value.Size || checks.Md5Hex != known.Value.Md5)
                {
                    return false;
                }
            }

            EnsureWl1Structure(assetsFolderPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Reads the whole W3DSW14.SHR entry into memory (the catalog walk wants
    // random access, and it is only ~790 KB), provably well-formed before any
    // parsing: the zip's central directory declares the entry's uncompressed
    // size and CRC-32, and both are checked here.
    private static byte[] ExtractShrFromZip(string zipPath)
    {
        using var zipFile = new ZipFile(zipPath);

        int index = zipFile.FindEntry(WolfensteinAssetCatalog.ShrEntryName, ignoreCase: true);
        if (index < 0)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                $"Entry '{WolfensteinAssetCatalog.ShrEntryName}' not found in '{zipPath}'.");
        }

        ZipEntry entry = zipFile[index];
        var data = new byte[entry.Size];

        using (Stream entryStream = zipFile.GetInputStream(entry))
        {
            entryStream.ReadExactly(data);
            if (entryStream.ReadByte() != -1)
            {
                throw new AssetPipelineException(AssetStage.Extracting,
                    $"'{WolfensteinAssetCatalog.ShrEntryName}' is larger than its declared size.");
            }
        }

        var crc = new Crc32();
        crc.Update(data);
        if (crc.Value != entry.Crc)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                $"'{WolfensteinAssetCatalog.ShrEntryName}' CRC-32 mismatch: computed {crc.Value:x8}, declared {entry.Crc:x8}.");
        }

        return data;
    }

    // Walks the .SHR catalog. Wanted records (the eight .WL1 files plus
    // VENDOR.DOC) decompress through DclInputStream in a single pass that
    // feeds the MD5 computation and the disk write from the same buffer
    // fill; everything else — including the DOS executables — is skipped
    // over by its compressed size WITHOUT decompressing, so nothing with an
    // .EXE extension ever touches disk. The walk must consume the archive
    // exactly to its final byte, and all eight .WL1 files must verify.
    private static void ExtractCatalog(byte[] archive, string outputDir, byte[] buffer,
        CancellationToken cancellationToken)
    {
        if (archive.Length < GlobalHeaderSize + RecordHeaderSize)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                "The installer archive is too small to be a .SHR catalog.");
        }

        int fileCount = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(FileCountOffset, 2));
        int position = GlobalHeaderSize;
        int wl1Verified = 0;

        for (int i = 0; i < fileCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (position + RecordHeaderSize > archive.Length)
            {
                throw new AssetPipelineException(AssetStage.Extracting,
                    $"Record {i + 1} header runs past the end of the archive.");
            }

            ReadOnlySpan<byte> header = archive.AsSpan(position, RecordHeaderSize);

            // The name field is NUL terminated, but bytes after the terminator
            // are uninitialized garbage - cut at the FIRST NUL.
            ReadOnlySpan<byte> nameField = header[..NameFieldSize];
            int nameEnd = nameField.IndexOf((byte) 0);
            string name = Encoding.ASCII.GetString(nameEnd >= 0 ? nameField[..nameEnd] : nameField);
            if (name.Length == 0 || name.IndexOfAny(new[] { '/', '\\' }) >= 0 || name.Contains(".."))
            {
                throw new AssetPipelineException(AssetStage.Extracting,
                    $"Record {i + 1} has a missing or unsafe file name.");
            }

            int compressedSize = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(CompressedSizeOffset, 4));
            position += RecordHeaderSize;
            if (compressedSize < 0 || position + compressedSize > archive.Length)
            {
                throw new AssetPipelineException(AssetStage.Extracting,
                    $"'{name}' data runs past the end of the archive.");
            }

            bool isKnownWl1 = WolfensteinAssetCatalog.KnownWl1Files.TryGetValue(name, out (long Size, string Md5) known);
            bool keep = isKnownWl1
                || string.Equals(name, WolfensteinAssetCatalog.VendorDocFileName, StringComparison.OrdinalIgnoreCase);

            if (keep)
            {
                var destinationPath = Path.Combine(outputDir, name);
                long uncompressedSize = 0;
                string md5Hex;

                using (var compressedStream = new MemoryStream(archive, position, compressedSize, writable: false))
                using (var dclStream = new DclInputStream(compressedStream))
                using (var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5))
                using (FileStream outputStream = File.Create(destinationPath))
                {
                    int bytesRead;
                    while ((bytesRead = dclStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        md5.AppendData(buffer, 0, bytesRead);
                        outputStream.Write(buffer, 0, bytesRead);
                        uncompressedSize += bytesRead;
                    }
                    md5Hex = Convert.ToHexStringLower(md5.GetHashAndReset());
                }

                if (isKnownWl1)
                {
                    if (uncompressedSize != known.Size || md5Hex != known.Md5)
                    {
                        throw new AssetPipelineException(AssetStage.Extracting,
                            $"'{name}' does not match the documented shareware v1.4 file "
                            + $"(size {uncompressedSize:N0}, MD5 {md5Hex}).");
                    }
                    wl1Verified++;
                }
            }

            position += compressedSize;
        }

        if (position != archive.Length)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                $"{archive.Length - position:N0} unexpected byte(s) remain after the last catalog record.");
        }

        if (wl1Verified != WolfensteinAssetCatalog.KnownWl1Files.Count)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                $"Only {wl1Verified} of the {WolfensteinAssetCatalog.KnownWl1Files.Count} expected .WL1 files were found.");
        }
    }

    // DCL provides no checksums of its own, so beyond the documented sizes
    // and MD5s, id Software's own signatures give a final structural layer:
    // GAMEMAPS.WL1 begins with the ASCII magic 'TED5v1.0' (the id map
    // editor), and MAPHEAD.WL1 begins with the RLEW compression tag 0xABCD.
    private static void EnsureWl1Structure(string assetsFolderPath)
    {
        Span<byte> gamemapsMagic = stackalloc byte[8];
        using (FileStream stream = File.OpenRead(Path.Combine(assetsFolderPath, "GAMEMAPS.WL1")))
        {
            stream.ReadExactly(gamemapsMagic);
        }
        if (!gamemapsMagic.SequenceEqual("TED5v1.0"u8))
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                "GAMEMAPS.WL1 does not start with the TED5v1.0 signature.");
        }

        Span<byte> rlewTag = stackalloc byte[2];
        using (FileStream stream = File.OpenRead(Path.Combine(assetsFolderPath, "MAPHEAD.WL1")))
        {
            stream.ReadExactly(rlewTag);
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(rlewTag) != 0xABCD)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                "MAPHEAD.WL1 does not start with the 0xABCD RLEW tag.");
        }
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
