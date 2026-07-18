//
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//

using System.IO;

namespace Wolfenstein.Brix.GameEngine.Assets;

/// <summary>
/// The complete decoded shareware data set, loaded from the assets
/// folder that the application's Assets Mode downloads and verifies.
/// Everything is parsed from the original .WL1 files into memory at
/// every startup - the engine never writes derived files.
/// </summary>
public sealed class WolfAssets
{
    /// <summary>The shareware data file extension.</summary>
    public const string FileExtension = "WL1";

    // RE-ENABLE ADDITIONAL .WL6 FILE SUPPORT HERE: the parsers handle
    // the registered data files as-is; to support them, make the
    // extension selectable ("WL6"), pick it by probing which set is
    // present in the assets folder, and remove the shareware-only
    // level-count assumptions in the level-flow logic.

    private WolfAssets()
    {
    }

    /// <summary>The decoded VSWAP contents (walls, sprites, digitized sounds).</summary>
    public VswapFile Vswap { get; private set; }

    /// <summary>The decoded map set.</summary>
    public GameMapsFile Maps { get; private set; }

    /// <summary>The decoded menu/title graphics and fonts.</summary>
    public VgaGraphFile Graphics { get; private set; }

    /// <summary>The raw AdLib sound-effect and IMF music chunks.</summary>
    public AudioTFile Audio { get; private set; }

    /// <summary>
    /// Loads every data file from the assets folder. The folder must
    /// contain the eight shareware v1.4 data files; the application
    /// verifies their md5s before the engine is handed the folder.
    /// </summary>
    public static WolfAssets Load(string assetsFolderPath)
    {
        string PathOf(string baseName)
        {
            var candidate = Path.Combine(assetsFolderPath, baseName + "." + FileExtension);
            if (!File.Exists(candidate))
            {
                throw new FileNotFoundException(
                    $"The Wolfenstein 3-D shareware data file {baseName}.{FileExtension} was not found in " +
                    $"{assetsFolderPath}. Use the application's Assets Mode to download and install the " +
                    "shareware episode.", candidate);
            }

            return candidate;
        }

        return new WolfAssets
        {
            Vswap = VswapFile.Load(PathOf("VSWAP")),
            Maps = GameMapsFile.Load(PathOf("MAPHEAD"), PathOf("GAMEMAPS")),
            Graphics = VgaGraphFile.Load(PathOf("VGADICT"), PathOf("VGAHEAD"), PathOf("VGAGRAPH")),
            Audio = AudioTFile.Load(PathOf("AUDIOHED"), PathOf("AUDIOT")),
        };
    }
}
