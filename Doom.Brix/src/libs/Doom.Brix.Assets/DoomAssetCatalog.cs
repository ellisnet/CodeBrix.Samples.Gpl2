namespace Doom.Brix.Assets;

/// <summary>
/// The known-good facts about the DOOM shareware v1.9 distribution the
/// application installs its game assets from: the doom19s.zip download,
/// the DOOM1.WAD inside it, and where to browse for it.
/// </summary>
public static class DoomAssetCatalog
{
    /// <summary>The only file the built-in browser is allowed to download.</summary>
    public const string AssetFileName = "doom19s.zip";

    /// <summary>The exact size of the authentic doom19s.zip, in bytes.</summary>
    public const long AssetZipSize = 2_450_688;

    /// <summary>The CRC-32 of the authentic doom19s.zip.</summary>
    public const long AssetZipCrc32 = 0xE692E533;

    /// <summary>The MD5 (lowercase hex) of the authentic doom19s.zip.</summary>
    public const string AssetZipMd5 = "244d181457c9be5f28b91b488e67e042";

    /// <summary>The one game-asset file the application needs: the shareware IWAD.</summary>
    public const string WadFileName = "DOOM1.WAD";

    /// <summary>The exact size of the shareware v1.9 DOOM1.WAD, in bytes.</summary>
    public const long WadSize = 4_196_020;

    /// <summary>The CRC-32 of the shareware v1.9 DOOM1.WAD.</summary>
    public const long WadCrc32 = 0x162B696A;

    /// <summary>The MD5 (lowercase hex) of the shareware v1.9 DOOM1.WAD.</summary>
    public const string WadMd5 = "f0cefca49926d00903cf57551d901abe";

    /// <summary>The page Assets Mode browses to by default: the idgames entry for doom19s.zip.</summary>
    public const string DefaultBrowseUrl = "https://www.doomworld.com/idgames/idstuff/doom/doom19s";

    // The two halves of the split PKZIP self-extracting archive inside
    // doom19s.zip; concatenated in this order they are DOOMS_19.EXE.
    internal static readonly string[] ArchivePartNames = { "DOOMS_19.1", "DOOMS_19.2" };
}
