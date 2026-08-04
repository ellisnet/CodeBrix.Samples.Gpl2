using System;
using System.Collections.Generic;

namespace Wolfenstein.Brix.Assets;

/// <summary>
/// The known-good facts about the Wolfenstein 3D Episode 1 shareware v1.4
/// distribution the application installs its game assets from: the
/// 1wolf14.zip download, the .WL1 game-data files inside its W3DSW14.SHR
/// installer archive, and where to browse for it.
/// </summary>
public static class WolfensteinAssetCatalog
{
    /// <summary>The only file the built-in browser is allowed to download.</summary>
    public const string AssetFileName = "1wolf14.zip";

    /// <summary>The exact size of the authentic 1wolf14.zip, in bytes.</summary>
    public const long AssetZipSize = 856_401;

    /// <summary>The CRC-32 of the authentic 1wolf14.zip.</summary>
    public const long AssetZipCrc32 = 0xF90C4C14;

    /// <summary>The MD5 (lowercase hex) of the authentic 1wolf14.zip.</summary>
    public const string AssetZipMd5 = "a29432cd4a5184d552d8e5da8f80a531";

    /// <summary>The custom Apogee installer archive inside the zip that holds the game files.</summary>
    public const string ShrEntryName = "W3DSW14.SHR";

    /// <summary>The Apogee license text, kept alongside the game files (not verified by hash).</summary>
    public const string VendorDocFileName = "VENDOR.DOC";

    /// <summary>The page Assets Mode browses to by default: the classicdosgames.com Wolfenstein 3D entry.</summary>
    public const string DefaultBrowseUrl = "https://www.classicdosgames.com/game/Wolfenstein_3D.html";

    /// <summary>
    /// The documented sizes and MD5 hashes of the Wolfenstein 3D shareware
    /// v1.4 .WL1 game-data files (per Debian game-data-packager's wolf3d
    /// data) — the required asset set, all eight verified on extraction and
    /// re-verified at every launch.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (long Size, string Md5)> KnownWl1Files =
        new Dictionary<string, (long Size, string Md5)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUDIOHED.WL1"] = (1_156, "58aa1b9892d5adfa725fab343d9446f8"),
            ["AUDIOT.WL1"] = (132_613, "4b6109e957b584e4ad7f376961f3887e"),
            ["GAMEMAPS.WL1"] = (27_425, "30fecd7cce6bc70402651ec922d2da3d"),
            ["MAPHEAD.WL1"] = (402, "7b6dd4e55c33c33a41d1600be5df3228"),
            ["VGADICT.WL1"] = (1_024, "76a6128f3c0dd9b77939ce8313992746"),
            ["VGAGRAPH.WL1"] = (326_568, "74decb641b1a4faed173e10ab744bff0"),
            ["VGAHEAD.WL1"] = (471, "61bf1616e78367853c91f2c04e2c1cb7"),
            ["VSWAP.WL1"] = (742_912, "6efa079414b817c97db779cecfb081c9"),
        };
}
