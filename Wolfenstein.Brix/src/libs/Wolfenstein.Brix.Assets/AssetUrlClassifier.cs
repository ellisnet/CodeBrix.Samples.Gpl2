using System;
using System.Collections.Generic;
using System.IO;

namespace Wolfenstein.Brix.Assets;

/// <summary>
/// Classifies URLs the built-in browser is about to navigate to: the one
/// permitted asset-file download, other download-looking links (which get
/// canceled), and ordinary page navigation (which passes through).
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
    /// (<see cref="WolfensteinAssetCatalog.AssetFileName"/>), by the file name
    /// visible in the URL's path.
    /// </summary>
    public static bool IsAssetFileUrl(string url) =>
        string.Equals(GetUrlFileName(url), WolfensteinAssetCatalog.AssetFileName, StringComparison.OrdinalIgnoreCase);

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
