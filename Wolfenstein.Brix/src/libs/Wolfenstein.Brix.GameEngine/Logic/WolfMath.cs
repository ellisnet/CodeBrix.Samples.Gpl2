//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), files
// wolf3d/code/wolf/wolf_math.h and wolf_math.c. The game logic keeps
// the original conventions verbatim: positions are 16.16 fixed point
// (one tile = 0x10000), the map is y-up, and angles are FINE units
// (128 per degree, 0 = east, 90 degrees = north).
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

using System;

namespace Wolfenstein.Brix.GameEngine.Logic;

/// <summary>The four cardinal directions (0 = east, counter-clockwise), plus none.</summary>
public enum Dir4
{
    /// <summary>Toward positive x.</summary>
    East,

    /// <summary>Toward positive y (the logic map is y-up).</summary>
    North,

    /// <summary>Toward negative x.</summary>
    West,

    /// <summary>Toward negative y.</summary>
    South,

    /// <summary>No direction.</summary>
    NoDir,
}

/// <summary>The eight directions (0 = east, counter-clockwise), plus none.</summary>
public enum Dir8
{
    /// <summary>Toward positive x.</summary>
    East,

    /// <summary>Diagonal +x +y.</summary>
    NorthEast,

    /// <summary>Toward positive y.</summary>
    North,

    /// <summary>Diagonal -x +y.</summary>
    NorthWest,

    /// <summary>Toward negative x.</summary>
    West,

    /// <summary>Diagonal -x -y.</summary>
    SouthWest,

    /// <summary>Toward negative y.</summary>
    South,

    /// <summary>Diagonal +x -y.</summary>
    SouthEast,

    /// <summary>No direction.</summary>
    NoDir,
}

/// <summary>
/// The original math conventions: fixed-point positions, FINE angles
/// and the direction lookup tables.
/// </summary>
public static class WolfMath
{
    /// <summary>One tile in fixed-point units.</summary>
    public const int TileGlobal = 0x10000;

    /// <summary>Half a tile in fixed-point units.</summary>
    public const int HalfTile = 0x8000;

    /// <summary>The tile shift (position &gt;&gt; TileShift = tile).</summary>
    public const int TileShift = 16;

    /// <summary>The minimum distance kept between the player and walls/actors.</summary>
    public const int MinDist = 0x5800;

    /// <summary>One degree in FINE angle units.</summary>
    public const int Ang1 = 128;

    /// <summary>45 degrees in FINE units.</summary>
    public const int Ang45 = 5760;

    /// <summary>90 degrees in FINE units.</summary>
    public const int Ang90 = 11520;

    /// <summary>180 degrees in FINE units.</summary>
    public const int Ang180 = 23040;

    /// <summary>270 degrees in FINE units.</summary>
    public const int Ang270 = 34560;

    /// <summary>360 degrees in FINE units.</summary>
    public const int Ang360 = 46080;

    /// <summary>Per-direction tile x deltas, indexed by <see cref="Dir4"/>.</summary>
    public static readonly int[] Dx4Dir = { 1, 0, -1, 0, 0 };

    /// <summary>Per-direction tile y deltas, indexed by <see cref="Dir4"/>.</summary>
    public static readonly int[] Dy4Dir = { 0, 1, 0, -1, 0 };

    /// <summary>Per-direction tile x deltas, indexed by <see cref="Dir8"/>.</summary>
    public static readonly int[] Dx8Dir = { 1, 1, 0, -1, -1, -1, 0, 1, 0 };

    /// <summary>Per-direction tile y deltas, indexed by <see cref="Dir8"/>.</summary>
    public static readonly int[] Dy8Dir = { 0, 1, 1, 1, 0, -1, -1, -1, 0 };

    /// <summary>The opposite of each <see cref="Dir4"/>.</summary>
    public static readonly Dir4[] Opposite4 = { Dir4.West, Dir4.South, Dir4.East, Dir4.North, Dir4.NoDir };

    /// <summary>The opposite of each <see cref="Dir8"/>.</summary>
    public static readonly Dir8[] Opposite8 =
    {
        Dir8.West, Dir8.SouthWest, Dir8.South, Dir8.SouthEast,
        Dir8.East, Dir8.NorthEast, Dir8.North, Dir8.NorthWest, Dir8.NoDir,
    };

    /// <summary>Maps a <see cref="Dir4"/> to the corresponding <see cref="Dir8"/>.</summary>
    public static readonly Dir8[] Dir4To8 = { Dir8.East, Dir8.North, Dir8.West, Dir8.South, Dir8.NoDir };

    /// <summary>The FINE angle of each <see cref="Dir8"/>.</summary>
    public static readonly int[] Dir8Angle =
    {
        0, Ang45, Ang90, Ang90 + Ang45, Ang180, Ang180 + Ang45, Ang270, Ang270 + Ang45, 0,
    };

    /// <summary>The FINE angle of each <see cref="Dir4"/>.</summary>
    public static readonly int[] Dir4Angle = { 0, Ang90, Ang180, Ang270, 0 };

    /// <summary>Maps (dx+1, dy+1) deltas to a <see cref="Dir4"/> (cardinals only).</summary>
    public static readonly Dir4[,] Dir4d =
    {
        { Dir4.NoDir, Dir4.West, Dir4.NoDir },
        { Dir4.South, Dir4.NoDir, Dir4.North },
        { Dir4.NoDir, Dir4.East, Dir4.NoDir },
    };

    /// <summary>
    /// The diagonal between two cardinal <see cref="Dir8"/> values, or
    /// NoDir when the pair does not form a diagonal.
    /// </summary>
    public static Dir8 Diagonal(Dir8 a, Dir8 b)
    {
        return (a, b) switch
        {
            (Dir8.East, Dir8.North) or (Dir8.North, Dir8.East) => Dir8.NorthEast,
            (Dir8.East, Dir8.South) or (Dir8.South, Dir8.East) => Dir8.SouthEast,
            (Dir8.West, Dir8.North) or (Dir8.North, Dir8.West) => Dir8.NorthWest,
            (Dir8.West, Dir8.South) or (Dir8.South, Dir8.West) => Dir8.SouthWest,
            _ => Dir8.NoDir,
        };
    }

    /// <summary>The fixed-point position of a tile's center.</summary>
    public static int Tile2Pos(int tile) => (tile << TileShift) + HalfTile;

    /// <summary>The tile containing a fixed-point position.</summary>
    public static int Pos2Tile(int pos) => pos >> TileShift;

    /// <summary>Converts a FINE angle to radians.</summary>
    public static double Fine2Rad(int fine) => fine * Math.PI / Ang180;

    /// <summary>Converts radians to a FINE angle.</summary>
    public static int Rad2Fine(double radians) => (int)(radians * Ang180 / Math.PI);

    /// <summary>The sine of a FINE angle.</summary>
    public static double SinFine(int fine) => Math.Sin(Fine2Rad(fine));

    /// <summary>The cosine of a FINE angle.</summary>
    public static double CosFine(int fine) => Math.Cos(Fine2Rad(fine));

    /// <summary>Clips a FINE angle to [0, 360 degrees).</summary>
    public static int NormalizeAngle(int alpha)
    {
        if (alpha >= Ang360)
        {
            alpha %= Ang360;
        }

        if (alpha < 0)
        {
            alpha = Ang360 - (-alpha % Ang360);
            if (alpha == Ang360)
            {
                alpha = 0;
            }
        }

        return alpha;
    }

    /// <summary>Normalizes a radian angle to [0, 2 pi).</summary>
    public static double NormalizeRadians(double angle)
    {
        angle %= 2.0 * Math.PI;
        if (angle < 0.0)
        {
            angle += 2.0 * Math.PI;
        }

        return angle;
    }

    /// <summary>The nearest cardinal direction for a radian angle.</summary>
    public static Dir4 Get4dir(double angle)
    {
        angle = NormalizeRadians(angle + Math.PI / 4.0);
        if (angle < Math.PI / 2.0)
        {
            return Dir4.East;
        }

        if (angle < Math.PI)
        {
            return Dir4.North;
        }

        if (angle < 3.0 * Math.PI / 2.0)
        {
            return Dir4.West;
        }

        return Dir4.South;
    }

    /// <summary>The nearest eight-way direction for a radian angle.</summary>
    public static Dir8 Get8dir(double angle)
    {
        angle = NormalizeRadians(angle + Math.PI / 12.0);
        if (angle <= Math.PI / 4.0)
        {
            return Dir8.East;
        }

        if (angle < Math.PI / 2.0)
        {
            return Dir8.NorthEast;
        }

        if (angle <= 3.0 * Math.PI / 4.0)
        {
            return Dir8.North;
        }

        if (angle < Math.PI)
        {
            return Dir8.NorthWest;
        }

        if (angle <= 5.0 * Math.PI / 4.0)
        {
            return Dir8.West;
        }

        if (angle < 3.0 * Math.PI / 2.0)
        {
            return Dir8.SouthWest;
        }

        if (angle <= 7.0 * Math.PI / 4.0)
        {
            return Dir8.South;
        }

        return Dir8.SouthEast;
    }

    /// <summary>The perpendicular distance from point (x, y) to a line at FINE angle a through the origin.</summary>
    public static int Point2LineDist(int x, int y, int fineAngle) =>
        Math.Abs((int)(x * SinFine(fineAngle) - y * CosFine(fineAngle)));

    /// <summary>The along-line length to the point nearest (x, y) on a line at FINE angle a.</summary>
    public static int LineLen2Point(int x, int y, int fineAngle) =>
        (int)(x * CosFine(fineAngle) + y * SinFine(fineAngle));

    /// <summary>The radian angle of point 1 as seen from point 2.</summary>
    public static double TransformPoint(double point1X, double point1Y, double point2X, double point2Y) =>
        NormalizeRadians(Math.Atan2(point1Y - point2Y, point1X - point2X));
}
