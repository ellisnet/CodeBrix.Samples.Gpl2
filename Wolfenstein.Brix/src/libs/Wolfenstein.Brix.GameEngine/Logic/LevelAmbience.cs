//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/wolfextractor/wolf/wolf_map.c (the vgaCeilingWL6 table's
// episode 1 entries).
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

namespace Wolfenstein.Brix.GameEngine.Logic;

/// <summary>Per-level presentation facts that are not stored in the map files.</summary>
public static class LevelAmbience
{
    // Episode 1 in map order: maps 1-8, boss, secret.
    private static readonly byte[] CeilingColors =
    {
        0x1D, 0x1D, 0x1D, 0x1D, 0x1D, 0x1D, 0x1D, 0x1D, 0x1D, 0xBF,
    };

    /// <summary>The ceiling palette index for a level (the floor is always 0x19).</summary>
    public static byte CeilingColorFor(int levelIndex) =>
        levelIndex >= 0 && levelIndex < CeilingColors.Length ? CeilingColors[levelIndex] : (byte)0x1D;
}
