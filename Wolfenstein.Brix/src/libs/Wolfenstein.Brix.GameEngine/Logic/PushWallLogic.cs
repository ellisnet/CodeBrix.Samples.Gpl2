//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/code/wolf/wolf_pushwalls.c.
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

/// <summary>The one-at-a-time moving secret pushwall.</summary>
public sealed class PushWallLogic
{
    private readonly WolfLogic logic;

    /// <summary>Creates the pushwall logic bound to its game context.</summary>
    public PushWallLogic(WolfLogic logic)
    {
        this.logic = logic;
    }

    /// <summary>True while a pushwall is moving.</summary>
    public bool Active { get; private set; }

    /// <summary>Whole tiles moved so far (a pushwall moves at most 3).</summary>
    public int TilesMoved { get; private set; }

    /// <summary>The 0-127 fractional progress into the current tile move.</summary>
    public int PointsMoved { get; private set; }

    /// <summary>The direction of travel.</summary>
    public Dir4 Dir { get; private set; }

    /// <summary>The tile currently holding the moving wall.</summary>
    public int X { get; private set; }

    /// <summary>The tile y currently holding the moving wall.</summary>
    public int Y { get; private set; }

    /// <summary>The per-step tile delta.</summary>
    public int Dx { get; private set; }

    /// <summary>The per-step tile y delta.</summary>
    public int Dy { get; private set; }

    /// <summary>The moving wall's x-crossing texture.</summary>
    public int TexX { get; private set; }

    /// <summary>The moving wall's y-crossing texture.</summary>
    public int TexY { get; private set; }

    /// <summary>Restores the pushwall state from a save game.</summary>
    public void RestoreFrom(
        bool active, int tilesMoved, int pointsMoved, Dir4 dir,
        int x, int y, int dx, int dy, int texX, int texY)
    {
        Active = active;
        TilesMoved = tilesMoved;
        PointsMoved = pointsMoved;
        Dir = dir;
        X = x;
        Y = y;
        Dx = dx;
        Dy = dy;
        TexX = texX;
        TexY = texY;
    }

    /// <summary>Resets the pushwall state for a new level.</summary>
    public void Reset()
    {
        Active = false;
        TilesMoved = PointsMoved = 0;
        X = Y = Dx = Dy = TexX = TexY = 0;
        Dir = Dir4.NoDir;
    }

    /// <summary>Tries to start a pushwall moving (PushWall_Push).</summary>
    public bool Push(int x, int y, Dir4 dir)
    {
        if (Active)
        {
            return false; // Only one pushwall moves at a time.
        }

        var level = logic.Level;
        var dx = WolfMath.Dx4Dir[(int)dir];
        var dy = WolfMath.Dy4Dir[(int)dir];

        if ((level.TileMap[x + dx, y + dy] & (TileFlag.SolidTile | TileFlag.Door)) != 0)
        {
            return true; // Something is blocking; the push is consumed.
        }

        level.TileMap[x, y] &= ~TileFlag.Secret;
        level.TileMap[x, y] &= ~TileFlag.Wall;
        level.TileMap[x, y] |= TileFlag.PushWall;

        level.FoundSecrets++;
        logic.Notify(level.FoundSecrets == level.TotalSecrets
            ? "You found the last secret!"
            : "You found a secret!");
        logic.PlayDigitizedSound(WolfDigitizedSounds.PushWallActivate);

        // Make the tile behind the pushwall unpassable while it moves.
        level.TileMap[x + dx, y + dy] |= TileFlag.PushWall;
        level.WallTexX[x + dx, y + dy] = level.WallTexX[x, y];
        level.WallTexY[x + dx, y + dy] = level.WallTexY[x, y];

        Active = true;
        TilesMoved = PointsMoved = 0;
        Dir = dir;
        X = x;
        Y = y;
        Dx = dx;
        Dy = dy;
        TexX = level.WallTexX[x, y];
        TexY = level.WallTexY[x, y];
        return true;
    }

    /// <summary>Advances the moving pushwall (PushWall_Process).</summary>
    public void Process(int tics)
    {
        if (!Active)
        {
            return;
        }

        var level = logic.Level;
        PointsMoved += tics;
        if (PointsMoved < 128)
        {
            return;
        }

        PointsMoved -= 128;
        TilesMoved++;

        // Free the old tile and occupy the next.
        level.TileMap[X, Y] &= ~TileFlag.PushWall;
        X += Dx;
        Y += Dy;

        if ((level.TileMap[X + Dx, Y + Dy] &
             (TileFlag.SolidTile | TileFlag.Door | TileFlag.Actor | TileFlag.Powerup)) != 0 ||
            TilesMoved == 3)
        {
            // The wall settles here.
            level.TileMap[X, Y] &= ~TileFlag.PushWall;
            level.TileMap[X, Y] |= TileFlag.Wall;
            level.WallTexX[X, Y] = TexX;
            level.WallTexY[X, Y] = TexY;
            Active = false;
        }
        else
        {
            level.TileMap[X + Dx, Y + Dy] |= TileFlag.PushWall;
        }
    }
}
