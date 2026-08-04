//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), files
// wolf3d/code/wolf/wolf_level.h (tile flags, LevelData_t) and
// wolf_local.h (level_locals_t). The logic map is y-up: tile (x, 0)
// is the SOUTH edge, exactly as the original flips the raw plane rows
// at load.
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

using System.Collections.Generic;

namespace Wolfenstein.Brix.GameEngine.Logic;

/// <summary>The per-tile map flags (the original's tilemap bit values).</summary>
public static class TileFlag
{
    /// <summary>A solid wall.</summary>
    public const long Wall = 1;

    /// <summary>A moving pushwall occupies the tile.</summary>
    public const long PushWall = 1 << 20;

    /// <summary>A door occupies the tile.</summary>
    public const long Door = 2;

    /// <summary>A pushwall secret can be pushed here.</summary>
    public const long Secret = 4;

    /// <summary>Non-blocking scenery.</summary>
    public const long Dress = 8;

    /// <summary>Blocking scenery.</summary>
    public const long Block = 16;

    /// <summary>A live actor occupies the tile.</summary>
    public const long Actor = 32;

    /// <summary>A corpse occupies the tile.</summary>
    public const long DeadActor = 64;

    /// <summary>A powerup lies on the tile.</summary>
    public const long Powerup = 128;

    /// <summary>An ambush marker tile.</summary>
    public const long Ambush = 256;

    /// <summary>The victory trigger tile.</summary>
    public const long Exit = 512;

    /// <summary>An elevator that leads to the secret level.</summary>
    public const long SecretLevel = 1024;

    /// <summary>An elevator wall tile.</summary>
    public const long Elevator = 1 << 11;

    /// <summary>Patrol waypoint: turn east.</summary>
    public const long TurnE = 1 << 12;

    /// <summary>Patrol waypoint: turn north-east.</summary>
    public const long TurnNE = 1 << 13;

    /// <summary>Patrol waypoint: turn north.</summary>
    public const long TurnN = 1 << 14;

    /// <summary>Patrol waypoint: turn north-west.</summary>
    public const long TurnNW = 1 << 15;

    /// <summary>Patrol waypoint: turn west.</summary>
    public const long TurnW = 1 << 16;

    /// <summary>Patrol waypoint: turn south-west.</summary>
    public const long TurnSW = 1 << 17;

    /// <summary>Patrol waypoint: turn south.</summary>
    public const long TurnS = 1 << 18;

    /// <summary>Patrol waypoint: turn south-east.</summary>
    public const long TurnSE = 1 << 19;

    /// <summary>Anything that blocks movement and sight like a wall.</summary>
    public const long SolidTile = Wall | Block | PushWall;

    /// <summary>Anything that blocks actor movement.</summary>
    public const long BlocksMoveTile = Wall | Block | PushWall | Actor;

    /// <summary>Any patrol waypoint.</summary>
    public const long Waypoint = TurnE | TurnNE | TurnN | TurnNW | TurnW | TurnSW | TurnS | TurnSE;
}

/// <summary>A static scenery sprite (non-pickup) placed at load.</summary>
public sealed class StaticSprite
{
    /// <summary>Creates a placed scenery sprite.</summary>
    public StaticSprite(int tileX, int tileY, int texture)
    {
        TileX = tileX;
        TileY = tileY;
        Texture = texture;
    }

    /// <summary>The tile x position.</summary>
    public int TileX { get; }

    /// <summary>The tile y position (y-up).</summary>
    public int TileY { get; }

    /// <summary>The sprite texture.</summary>
    public int Texture { get; }
}

/// <summary>A placed pickup.</summary>
public sealed class Powerup
{
    /// <summary>Creates a placed pickup.</summary>
    public Powerup(int tileX, int tileY, PowerupKind kind)
    {
        TileX = tileX;
        TileY = tileY;
        Kind = kind;
    }

    /// <summary>The tile x position.</summary>
    public int TileX { get; }

    /// <summary>The tile y position (y-up).</summary>
    public int TileY { get; }

    /// <summary>What the pickup is.</summary>
    public PowerupKind Kind { get; }
}

/// <summary>The pickup kinds (the original pow_t, in table order).</summary>
public enum PowerupKind
{
    /// <summary>Gibs: 1 health when nearly dead.</summary>
    Gibs,

    /// <summary>Second gibs variant.</summary>
    Gibs2,

    /// <summary>Dog food: 4 health.</summary>
    Alpo,

    /// <summary>First-aid kit: 25 health.</summary>
    FirstAid,

    /// <summary>The gold key.</summary>
    Key1,

    /// <summary>The silver key.</summary>
    Key2,

    /// <summary>Unused key slot.</summary>
    Key3,

    /// <summary>Unused key slot.</summary>
    Key4,

    /// <summary>Cross: 100 points.</summary>
    Cross,

    /// <summary>Chalice: 500 points.</summary>
    Chalice,

    /// <summary>Bible (chest): 1000 points.</summary>
    Bible,

    /// <summary>Crown: 5000 points.</summary>
    Crown,

    /// <summary>Ammo clip: 8 bullets.</summary>
    Clip,

    /// <summary>Dropped ammo clip: 4 bullets.</summary>
    Clip2,

    /// <summary>The machine gun.</summary>
    MachineGun,

    /// <summary>The chain gun.</summary>
    ChainGun,

    /// <summary>Plate of food: 10 health.</summary>
    Food,

    /// <summary>Extra life: full health and 25 bullets.</summary>
    FullHeal,
}

/// <summary>
/// The loaded level: tile planes, flags, areas, wall textures, doors,
/// placed sprites and the per-level score-keeping counters.
/// </summary>
public sealed class LevelState
{
    /// <summary>The number of sound-propagation areas.</summary>
    public const int NumAreas = 37;

    /// <summary>The first area value in the wall plane.</summary>
    public const int FirstArea = 0x6B;

    /// <summary>The ambush marker value in the wall plane.</summary>
    public const int AmbushTile = 0x6A;

    /// <summary>The per-tile flags, indexed [x, y] (y-up).</summary>
    public long[,] TileMap { get; } = new long[64, 64];

    /// <summary>The x-crossing wall texture per tile.</summary>
    public int[,] WallTexX { get; } = new int[64, 64];

    /// <summary>The y-crossing wall texture per tile.</summary>
    public int[,] WallTexY { get; } = new int[64, 64];

    /// <summary>Area numbers per tile: -1 wall, -2 door, -3 unknown.</summary>
    public int[,] Areas { get; } = new int[64, 64];

    /// <summary>The doors, indexed by tile; null when the tile has no door.</summary>
    public WolfDoor[,] DoorMap { get; } = new WolfDoor[64, 64];

    /// <summary>The doors in spawn order.</summary>
    public List<WolfDoor> Doors { get; } = new List<WolfDoor>();

    /// <summary>The static scenery sprites.</summary>
    public List<StaticSprite> Statics { get; } = new List<StaticSprite>();

    /// <summary>The live pickups.</summary>
    public List<Powerup> Powerups { get; } = new List<Powerup>();

    /// <summary>The player spawn position (fixed-point) and FINE angle.</summary>
    public int SpawnX { get; set; }

    /// <summary>The player spawn y (fixed-point, y-up).</summary>
    public int SpawnY { get; set; }

    /// <summary>The player spawn angle in FINE units.</summary>
    public int SpawnAngle { get; set; }

    /// <summary>The zero-based level index within the episode.</summary>
    public int LevelIndex { get; set; }

    /// <summary>Total secret pushwalls in the level.</summary>
    public int TotalSecrets { get; set; }

    /// <summary>Secrets found so far.</summary>
    public int FoundSecrets { get; set; }

    /// <summary>Total treasure items in the level.</summary>
    public int TotalTreasure { get; set; }

    /// <summary>Treasure found so far.</summary>
    public int FoundTreasure { get; set; }

    /// <summary>Total enemies in the level.</summary>
    public int TotalMonsters { get; set; }

    /// <summary>Enemies killed so far.</summary>
    public int KilledMonsters { get; set; }

    /// <summary>Tics spent in the level (for the par-time display).</summary>
    public long TimeTics { get; set; }
}
