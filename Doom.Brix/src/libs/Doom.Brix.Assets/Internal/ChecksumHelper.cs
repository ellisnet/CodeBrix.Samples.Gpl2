using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using CodeBrix.Compression.Checksum;

namespace Doom.Brix.Assets.Internal;

internal static class ChecksumHelper
{
    /// <summary>
    /// Computes the file's size, CRC-32 and MD5 (lowercase hex) in a single
    /// streaming pass with the shared copy buffer.
    /// </summary>
    public static (long Size, long Crc32, string Md5Hex) ComputeFileChecksums(
        string filePath, byte[] buffer, CancellationToken cancellationToken = default)
    {
        var crc = new Crc32();
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        long totalBytes = 0;

        using (var stream = File.OpenRead(filePath))
        {
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                crc.Update(new ArraySegment<byte>(buffer, 0, bytesRead));
                md5.AppendData(buffer, 0, bytesRead);
                totalBytes += bytesRead;
            }
        }

        return (totalBytes, crc.Value, Convert.ToHexStringLower(md5.GetHashAndReset()));
    }
}
