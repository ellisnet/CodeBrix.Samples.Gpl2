using System;
using System.Collections.Generic;
using System.IO;

namespace Doom.Brix.Assets;

/// <summary>
/// Classifies downloads (and URLs) for the built-in browser's Assets Mode
/// policy: the one permitted asset-file download proceeds, every other
/// download gets canceled, and ordinary page navigation passes through.
/// </summary>
public static class AssetUrlClassifier
{
    // File extensions that mark a link as "this is a file download, not a page".
    static readonly HashSet<string> downloadExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".exe", ".msi", ".7z", ".rar", ".gz", ".tgz", ".bz2", ".xz", ".tar",
        ".iso", ".dmg", ".deb", ".rpm", ".appimage", ".cab", ".arj", ".lha", ".lzh", ".bin",
    };

    /// <summary>
    /// Whether the URL points at the one permitted asset file
    /// (<see cref="DoomAssetCatalog.AssetFileName"/>), by the file name
    /// visible in the URL's path.
    /// </summary>
    public static bool IsAssetFileUrl(string url) =>
        string.Equals(GetUrlFileName(url), DoomAssetCatalog.AssetFileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the URL looks like a file download (by extension) — the asset
    /// file included; check <see cref="IsAssetFileUrl"/> first.
    /// </summary>
    public static bool IsDownloadLikeUrl(string url)
    {
        var fileName = GetUrlFileName(url);
        if (string.IsNullOrEmpty(fileName))
            return false;
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) && downloadExtensions.Contains(extension);
    }

    /// <summary>
    /// Whether a download the browser is starting is the one permitted asset
    /// file, judged by the download's suggested target file name (with the
    /// browser's collision suffix — "doom19s (2).zip" — ignored) or, as a
    /// fallback, by the file name visible in the download URL's path (a
    /// mirror may serve the file under a URL that does not show its name).
    /// </summary>
    public static bool IsAssetDownload(string url, string suggestedFileName) =>
        string.Equals(StripCollisionSuffix(suggestedFileName), DoomAssetCatalog.AssetFileName,
            StringComparison.OrdinalIgnoreCase)
        || IsAssetFileUrl(url);

    /// <summary>
    /// Removes the " (N)" a browser appends before the extension to make a
    /// suggested download name collision-free; other names pass unchanged.
    /// </summary>
    public static string StripCollisionSuffix(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return fileName;
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var open = stem.LastIndexOf(" (", StringComparison.Ordinal);
        if (open <= 0 || !stem.EndsWith(")", StringComparison.Ordinal))
            return fileName;
        var digits = stem.AsSpan(open + 2, stem.Length - open - 3);
        if (digits.IsEmpty)
            return fileName;
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c))
                return fileName;
        }
        return string.Concat(stem.AsSpan(0, open), extension);
    }

    /// <summary>The file name in the URL's path (query and fragment ignored), or null.</summary>
    public static string GetUrlFileName(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        var fileName = Path.GetFileName(path);
        return string.IsNullOrEmpty(fileName) ? null : fileName;
    }
}
