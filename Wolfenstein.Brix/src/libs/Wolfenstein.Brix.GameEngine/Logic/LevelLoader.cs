//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/code/wolf/wolf_level.c (Level_LoadMap's tile classification
// pass, Lvl_SpawnObj/Lvl_SpawnStatic with the static_wl6 table, and
// Level_ScanInfoPlane's difficulty-gated enemy spawn table). Works
// directly from the raw GAMEMAPS planes; like the original, raw map
// row 0 (north) becomes logic tile y=63.
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

using Wolfenstein.Brix.GameEngine.Assets;

namespace Wolfenstein.Brix.GameEngine.Logic;

/// <summary>Builds a <see cref="LevelState"/> from decoded map planes and spawns its contents.</summary>
public static class LevelLoader
{
    private readonly struct StatInfo
    {
        public StatInfo(bool block, PowerupKind? powerup)
        {
            Block = block;
            Powerup = powerup;
        }

        public bool Block { get; }

        public PowerupKind? Powerup { get; }
    }

    // The static_wl6 table: blocking flag and powerup kind per static
    // object type (plane-2 value - 23).
    private static readonly StatInfo[] Statics =
    {
        new StatInfo(false, null),                    // puddle
        new StatInfo(true, null),                     // green barrel
        new StatInfo(true, null),                     // table/chairs
        new StatInfo(true, null),                     // floor lamp
        new StatInfo(false, null),                    // chandelier
        new StatInfo(true, null),                     // hanged man
        new StatInfo(false, PowerupKind.Alpo),        // bad food
        new StatInfo(true, null),                     // red pillar
        new StatInfo(true, null),                     // tree
        new StatInfo(false, null),                    // skeleton flat
        new StatInfo(true, null),                     // sink
        new StatInfo(true, null),                     // potted plant
        new StatInfo(true, null),                     // urn
        new StatInfo(true, null),                     // bare table
        new StatInfo(false, null),                    // ceiling light
        new StatInfo(false, null),                    // kitchen stuff
        new StatInfo(true, null),                     // suit of armor
        new StatInfo(true, null),                     // hanging cage
        new StatInfo(true, null),                     // skeleton in cage
        new StatInfo(false, null),                    // skeleton relax
        new StatInfo(false, PowerupKind.Key1),        // gold key
        new StatInfo(false, PowerupKind.Key2),        // silver key
        new StatInfo(true, null),                     // stuff (bed)
        new StatInfo(false, null),                    // stuff (basket)
        new StatInfo(false, PowerupKind.Food),        // good food
        new StatInfo(false, PowerupKind.FirstAid),    // first aid
        new StatInfo(false, PowerupKind.Clip),        // clip
        new StatInfo(false, PowerupKind.MachineGun),  // machine gun
        new StatInfo(false, PowerupKind.ChainGun),    // gatling gun
        new StatInfo(false, PowerupKind.Cross),       // cross
        new StatInfo(false, PowerupKind.Chalice),     // chalice
        new StatInfo(false, PowerupKind.Bible),       // bible/chest
        new StatInfo(false, PowerupKind.Crown),       // crown
        new StatInfo(false, PowerupKind.FullHeal),    // one up
        new StatInfo(false, PowerupKind.Gibs),        // gibs
        new StatInfo(true, null),                     // barrel
        new StatInfo(true, null),                     // well
        new StatInfo(true, null),                     // empty well
        new StatInfo(false, PowerupKind.Gibs2),       // gibs 2
        new StatInfo(true, null),                     // flag
        new StatInfo(true, null),                     // call apogee
        new StatInfo(false, null),                    // junk
        new StatInfo(false, null),                    // junk
        new StatInfo(false, null),                    // junk
        new StatInfo(false, null),                    // pots
        new StatInfo(true, null),                     // stove
        new StatInfo(true, null),                     // spears
        new StatInfo(false, null),                    // vines
    };

    /// <summary>
    /// Loads a level: classifies the wall plane, spawns doors, statics
    /// and pickups, patches unknown areas, then spawns the enemies.
    /// </summary>
    public static LevelState Load(WolfLogic logic, MapData mapData, int levelIndex)
    {
        var level = new LevelState { LevelIndex = levelIndex };
        logic.AttachLevel(level);

        // Pass 1: object plane (statics/pickups/start/waypoints/markers)
        // and wall-plane classification, with the original y flip.
        for (var rawY = 0; rawY < 64; rawY++)
        {
            var y = 63 - rawY;
            for (var x = 0; x < 64; x++)
            {
                var layer1 = mapData.Plane0[rawY * 64 + x];
                var layer2 = mapData.Plane1[rawY * 64 + x];

                if (layer2 != 0)
                {
                    SpawnMapObject(logic, level, layer2, x, y);
                }

                if (layer1 == 0)
                {
                    level.Areas[x, y] = -3; // Unknown area.
                }
                else if (layer1 < 0x6A)
                {
                    if ((layer1 >= 90 && layer1 <= 95) || layer1 == 100 || layer1 == 101)
                    {
                        level.TileMap[x, y] |= TileFlag.Door;
                        DoorLogic.SpawnDoor(level, x, y, layer1);
                        level.Areas[x, y] = -2; // Door area.
                    }
                    else
                    {
                        level.TileMap[x, y] |= TileFlag.Wall;
                        level.WallTexX[x, y] = (layer1 - 1) * 2 + 1;
                        level.WallTexY[x, y] = (layer1 - 1) * 2;
                        level.Areas[x, y] = -1; // Wall area.
                        if (layer1 == 0x15)
                        {
                            level.TileMap[x, y] |= TileFlag.Elevator;
                        }
                    }
                }
                else if (layer1 == LevelState.AmbushTile)
                {
                    level.TileMap[x, y] |= TileFlag.Ambush;
                    level.Areas[x, y] = -3;
                }
                else if (layer1 >= LevelState.FirstArea &&
                         layer1 < LevelState.FirstArea + LevelState.NumAreas)
                {
                    if (layer1 == LevelState.FirstArea)
                    {
                        // The first area value doubles as the
                        // secret-elevator marker.
                        level.TileMap[x, y] |= TileFlag.SecretLevel;
                    }

                    level.Areas[x, y] = layer1 - LevelState.FirstArea;
                }
                else
                {
                    level.Areas[x, y] = -3;
                }
            }
        }

        // Replace unknown areas with an adjacent area, avoiding the
        // silent-attack problem for ambush guards (JDC fix).
        for (var x = 1; x < 63; x++)
        {
            for (var y = 1; y < 63; y++)
            {
                if (level.Areas[x, y] != -3)
                {
                    continue;
                }

                if (level.Areas[x - 1, y] >= 0)
                {
                    level.Areas[x, y] = level.Areas[x - 1, y];
                }
                else if (level.Areas[x + 1, y] >= 0)
                {
                    level.Areas[x, y] = level.Areas[x + 1, y];
                }
                else if (level.Areas[x, y - 1] >= 0)
                {
                    level.Areas[x, y] = level.Areas[x, y - 1];
                }
                else if (level.Areas[x, y + 1] >= 0)
                {
                    level.Areas[x, y] = level.Areas[x, y + 1];
                }
            }
        }

        DoorLogic.SetDoorAreas(level);

        // Pass 2: the enemy spawns (Level_ScanInfoPlane).
        ScanInfoPlane(logic, level, mapData);

        return level;
    }

    private static void SpawnMapObject(WolfLogic logic, LevelState level, int type, int x, int y)
    {
        if (type >= 23 && type < 23 + Statics.Length)
        {
            SpawnStatic(logic, level, type - 23, x, y);
            return;
        }

        switch (type)
        {
            case 0x13: // Start, facing north.
                SetSpawn(level, x, y, WolfMath.Ang90);
                break;
            case 0x14: // Start, facing east.
                SetSpawn(level, x, y, 0);
                break;
            case 0x15: // Start, facing south.
                SetSpawn(level, x, y, WolfMath.Ang270);
                break;
            case 0x16: // Start, facing west.
                SetSpawn(level, x, y, WolfMath.Ang180);
                break;

            case 0x5A:
                level.TileMap[x, y] |= TileFlag.TurnE;
                break;
            case 0x5B:
                level.TileMap[x, y] |= TileFlag.TurnNE;
                break;
            case 0x5C:
                level.TileMap[x, y] |= TileFlag.TurnN;
                break;
            case 0x5D:
                level.TileMap[x, y] |= TileFlag.TurnNW;
                break;
            case 0x5E:
                level.TileMap[x, y] |= TileFlag.TurnW;
                break;
            case 0x5F:
                level.TileMap[x, y] |= TileFlag.TurnSW;
                break;
            case 0x60:
                level.TileMap[x, y] |= TileFlag.TurnS;
                break;
            case 0x61:
                level.TileMap[x, y] |= TileFlag.TurnSE;
                break;

            case 0x62: // Pushwall modifier.
                level.TileMap[x, y] |= TileFlag.Secret;
                level.TotalSecrets++;
                break;

            case 0x63: // Victory trigger.
                level.TileMap[x, y] |= TileFlag.Exit;
                break;
        }
    }

    private static void SetSpawn(LevelState level, int x, int y, int angle)
    {
        level.SpawnX = WolfMath.Tile2Pos(x);
        level.SpawnY = WolfMath.Tile2Pos(y);
        level.SpawnAngle = angle;
    }

    private static void SpawnStatic(WolfLogic logic, LevelState level, int type, int x, int y)
    {
        var info = Statics[type];
        if (info.Powerup == null)
        {
            level.TileMap[x, y] |= info.Block ? TileFlag.Block : TileFlag.Dress;
            level.Statics.Add(new StaticSprite(x, y, Spr.SPR_STAT_0 + type));
        }
        else
        {
            logic.PlayerLogic.SpawnPowerup(x, y, info.Powerup.Value);
            switch (info.Powerup.Value)
            {
                case PowerupKind.Cross:
                case PowerupKind.Chalice:
                case PowerupKind.Bible:
                case PowerupKind.Crown:
                case PowerupKind.FullHeal:
                    level.TotalTreasure++;
                    break;
            }
        }
    }

    private static void ScanInfoPlane(WolfLogic logic, LevelState level, MapData mapData)
    {
        var skill = logic.Difficulty;
        var actors = logic.ActorLogic;

        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                int tile = mapData.Plane1[(63 - y) * 64 + x];
                if (tile == 0)
                {
                    continue;
                }

                switch (tile)
                {
                    // Guards, standing.
                    case >= 180 and <= 183 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnStand(EnemyKind.Guard, x, y, tile - 180);
                        break;
                    case >= 144 and <= 147 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnStand(EnemyKind.Guard, x, y, tile - 144);
                        break;
                    case >= 108 and <= 111:
                        actors.SpawnStand(EnemyKind.Guard, x, y, tile - 108);
                        break;

                    // Guards, patrolling.
                    case >= 184 and <= 187 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnPatrol(EnemyKind.Guard, x, y, tile - 184);
                        break;
                    case >= 148 and <= 151 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnPatrol(EnemyKind.Guard, x, y, tile - 148);
                        break;
                    case >= 112 and <= 115:
                        actors.SpawnPatrol(EnemyKind.Guard, x, y, tile - 112);
                        break;

                    case 124:
                        actors.SpawnDeadGuard(EnemyKind.Guard, x, y);
                        break;

                    // Officers, standing.
                    case >= 188 and <= 191 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnStand(EnemyKind.Officer, x, y, tile - 188);
                        break;
                    case >= 152 and <= 155 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnStand(EnemyKind.Officer, x, y, tile - 152);
                        break;
                    case >= 116 and <= 119:
                        actors.SpawnStand(EnemyKind.Officer, x, y, tile - 116);
                        break;

                    // Officers, patrolling.
                    case >= 192 and <= 195 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnPatrol(EnemyKind.Officer, x, y, tile - 192);
                        break;
                    case >= 156 and <= 159 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnPatrol(EnemyKind.Officer, x, y, tile - 156);
                        break;
                    case >= 120 and <= 123:
                        actors.SpawnPatrol(EnemyKind.Officer, x, y, tile - 120);
                        break;

                    // SS, standing.
                    case >= 198 and <= 201 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnStand(EnemyKind.SS, x, y, tile - 198);
                        break;
                    case >= 162 and <= 165 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnStand(EnemyKind.SS, x, y, tile - 162);
                        break;
                    case >= 126 and <= 129:
                        actors.SpawnStand(EnemyKind.SS, x, y, tile - 126);
                        break;

                    // SS, patrolling.
                    case >= 202 and <= 205 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnPatrol(EnemyKind.SS, x, y, tile - 202);
                        break;
                    case >= 166 and <= 169 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnPatrol(EnemyKind.SS, x, y, tile - 166);
                        break;
                    case >= 130 and <= 133:
                        actors.SpawnPatrol(EnemyKind.SS, x, y, tile - 130);
                        break;

                    // Dogs, standing.
                    case >= 206 and <= 209 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnStand(EnemyKind.Dog, x, y, tile - 206);
                        break;
                    case >= 170 and <= 173 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnStand(EnemyKind.Dog, x, y, tile - 170);
                        break;
                    case >= 134 and <= 137:
                        actors.SpawnStand(EnemyKind.Dog, x, y, tile - 134);
                        break;

                    // Dogs, patrolling.
                    case >= 210 and <= 213 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnPatrol(EnemyKind.Dog, x, y, tile - 210);
                        break;
                    case >= 174 and <= 177 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnPatrol(EnemyKind.Dog, x, y, tile - 174);
                        break;
                    case >= 138 and <= 141:
                        actors.SpawnPatrol(EnemyKind.Dog, x, y, tile - 138);
                        break;

                    // The episode 1 boss.
                    case 214:
                        actors.SpawnBoss(EnemyKind.Boss, x, y);
                        break;

                    // Mutants, standing (registered episodes; kept for
                    // table completeness).
                    case >= 252 and <= 255 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnStand(EnemyKind.Mutant, x, y, tile - 252);
                        break;
                    case >= 234 and <= 237 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnStand(EnemyKind.Mutant, x, y, tile - 234);
                        break;
                    case >= 216 and <= 219:
                        actors.SpawnStand(EnemyKind.Mutant, x, y, tile - 216);
                        break;

                    // Mutants, patrolling.
                    case >= 256 and <= 259 when skill >= DifficultyLevel.IAmDeathIncarnate:
                        actors.SpawnPatrol(EnemyKind.Mutant, x, y, tile - 256);
                        break;
                    case >= 238 and <= 241 when skill >= DifficultyLevel.BringEmOn:
                        actors.SpawnPatrol(EnemyKind.Mutant, x, y, tile - 238);
                        break;
                    case >= 220 and <= 223:
                        actors.SpawnPatrol(EnemyKind.Mutant, x, y, tile - 220);
                        break;
                }
            }
        }
    }
}
