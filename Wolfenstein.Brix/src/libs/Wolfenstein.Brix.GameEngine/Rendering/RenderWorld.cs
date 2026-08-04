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

using System;
using System.Collections.Generic;
using Wolfenstein.Brix.GameEngine.Logic;

namespace Wolfenstein.Brix.GameEngine.Rendering;

/// <summary>What a map tile renders as.</summary>
public enum RenderCellKind
{
    /// <summary>Nothing solid.</summary>
    Empty,

    /// <summary>A textured wall.</summary>
    Wall,

    /// <summary>A (possibly sliding) door.</summary>
    DoorCell,
}

/// <summary>A billboard sprite to draw this frame.</summary>
public readonly struct RenderSprite
{
    /// <summary>Creates a sprite instance.</summary>
    public RenderSprite(double x, double y, int texture)
    {
        X = x;
        Y = y;
        Texture = texture;
    }

    /// <summary>The world x in render coordinates (tiles).</summary>
    public double X { get; }

    /// <summary>The world y in render coordinates (tiles, y-down).</summary>
    public double Y { get; }

    /// <summary>The sprite texture number.</summary>
    public int Texture { get; }
}

/// <summary>
/// The renderer's read-only view over <see cref="WolfLogic"/>: converts
/// the logic's y-up fixed-point world into the renderer's y-down tile
/// coordinates, resolves per-tile render info, and assembles the
/// frame's sprite list (statics, pickups, actors) depth-sorted far to
/// near.
/// </summary>
public sealed class RenderWorld
{
    private const double FixedToTile = 1.0 / WolfMath.TileGlobal;

    private readonly WolfLogic logic;
    private readonly List<RenderSprite> sprites = new List<RenderSprite>(256);

    /// <summary>Creates the view over a game simulation.</summary>
    public RenderWorld(WolfLogic logic)
    {
        this.logic = logic;
    }

    /// <summary>The game simulation being viewed.</summary>
    public WolfLogic Logic => logic;

    /// <summary>The camera x position in render coordinates.</summary>
    public double CamX { get; private set; }

    /// <summary>The camera y position in render coordinates.</summary>
    public double CamY { get; private set; }

    /// <summary>The camera facing x component.</summary>
    public double DirX { get; private set; }

    /// <summary>The camera facing y component.</summary>
    public double DirY { get; private set; }

    /// <summary>The projection plane x component.</summary>
    public double PlaneX { get; private set; }

    /// <summary>The projection plane y component.</summary>
    public double PlaneY { get; private set; }

    /// <summary>The frame's depth-sorted sprites (farthest first).</summary>
    public IReadOnlyList<RenderSprite> Sprites => sprites;

    /// <summary>
    /// The camera projection-plane length relative to the unit facing
    /// direction (the classic raycaster field of view).
    /// </summary>
    public const double FocalPlaneScale = 2.0 / 3.0;

    /// <summary>Snapshots the camera and rebuilds the sprite list for one frame.</summary>
    public void Refresh()
    {
        var player = logic.Player;
        CamX = player.X * FixedToTile;
        CamY = 64.0 - (player.Y * FixedToTile);

        // Logic angles are counter-clockwise with y-up; the render world
        // is y-down, so the direction's y negates.
        var radians = WolfMath.Fine2Rad(player.Angle);
        DirX = Math.Cos(radians);
        DirY = -Math.Sin(radians);
        PlaneX = -DirY * FocalPlaneScale;
        PlaneY = DirX * FocalPlaneScale;

        sprites.Clear();
        var level = logic.Level;
        foreach (var stat in level.Statics)
        {
            sprites.Add(new RenderSprite(
                stat.TileX + 0.5, 64.0 - (stat.TileY + 0.5), stat.Texture));
        }

        foreach (var pow in level.Powerups)
        {
            sprites.Add(new RenderSprite(
                pow.TileX + 0.5, 64.0 - (pow.TileY + 0.5),
                PlayerLogic.PowerupTexture(pow.Kind)));
        }

        foreach (var guard in logic.Guards)
        {
            sprites.Add(new RenderSprite(
                guard.X * FixedToTile, 64.0 - (guard.Y * FixedToTile), guard.SpriteTexture));
        }

        // Depth sort, farthest first (painter's order).
        sprites.Sort((a, b) =>
        {
            var da = ((a.X - CamX) * (a.X - CamX)) + ((a.Y - CamY) * (a.Y - CamY));
            var db = ((b.X - CamX) * (b.X - CamX)) + ((b.Y - CamY) * (b.Y - CamY));
            return db.CompareTo(da);
        });
    }

    /// <summary>True when a render-coordinate tile lies inside the map.</summary>
    public bool InMap(int x, int y) => x >= 0 && x < 64 && y >= 0 && y < 64;

    /// <summary>What the tile at render coordinates renders as.</summary>
    public RenderCellKind KindAt(int x, int y)
    {
        var flags = logic.Level.TileMap[x, 63 - y];
        if ((flags & (TileFlag.Wall | TileFlag.PushWall)) != 0)
        {
            return RenderCellKind.Wall;
        }

        if ((flags & TileFlag.Door) != 0)
        {
            return RenderCellKind.DoorCell;
        }

        return RenderCellKind.Empty;
    }

    /// <summary>
    /// The wall texture for a hit side at render coordinates, with the
    /// door-jamb faces substituted next to doors.
    /// </summary>
    public int WallTexture(int x, int y, Side side)
    {
        var level = logic.Level;
        var yUp = 63 - y;
        if (side == Side.NorthSouth)
        {
            // A wall face toward a door tile shows the jamb texture.
            if ((x > 0 && (level.TileMap[x - 1, yUp] & TileFlag.Door) != 0) ||
                (x < 63 && (level.TileMap[x + 1, yUp] & TileFlag.Door) != 0))
            {
                return 100;
            }

            return level.WallTexX[x, yUp];
        }

        if ((yUp > 0 && (level.TileMap[x, yUp - 1] & TileFlag.Door) != 0) ||
            (yUp < 63 && (level.TileMap[x, yUp + 1] & TileFlag.Door) != 0))
        {
            return 101;
        }

        return level.WallTexY[x, yUp];
    }

    /// <summary>The door at render coordinates (null when there is none).</summary>
    public WolfDoor DoorAt(int x, int y) => logic.Level.DoorMap[x, 63 - y];

    /// <summary>
    /// How far the door at render coordinates has slid open, 0 (closed)
    /// to 64 (fully open), in the renderer's texture-column units.
    /// </summary>
    public double DoorOffset(int x, int y)
    {
        var door = logic.Level.DoorMap[x, 63 - y];
        if (door == null)
        {
            return 0.0;
        }

        return door.Action == DoorAction.Open
            ? 64.0
            : door.TicCount * 64.0 / DoorLogic.DoorFullOpen;
    }
}
