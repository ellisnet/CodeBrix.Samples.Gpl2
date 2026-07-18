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
using System.IO;

namespace Wolfenstein.Brix.GameEngine.Logic;

/// <summary>
/// Serializes the complete mid-level game state to bytes and back.
/// Compatibility with the original DOS save format is explicitly not a
/// goal; the slots live in the application's settings store.
/// </summary>
public static class SaveGameSerializer
{
    private const uint Magic = 0x56534257; // "WBSV"
    private const int Version = 1;

    /// <summary>Captures the running game into a save-slot payload.</summary>
    public static byte[] Save(WolfLogic logic)
    {
        if (logic.Player.State != PlayState.Playing)
        {
            throw new InvalidOperationException("Save games can only be captured while playing.");
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var level = logic.Level;
        var player = logic.Player;

        writer.Write(Magic);
        writer.Write(Version);
        writer.Write((int)logic.Difficulty);
        writer.Write(level.LevelIndex);
        writer.Write(logic.Rng.Index);

        // Player.
        writer.Write(player.X);
        writer.Write(player.Y);
        writer.Write(player.Angle);
        writer.Write(player.Health);
        writer.Write(player.Lives);
        writer.Write(player.Ammo);
        writer.Write(player.OldScore);
        writer.Write(player.Score);
        writer.Write(player.NextExtra);
        writer.Write((int)player.Items);
        writer.Write((int)player.CurrentWeapon);
        writer.Write((int)player.PendingWeapon);
        writer.Write(player.AreaNumber);

        // Level counters.
        writer.Write(level.TotalSecrets);
        writer.Write(level.FoundSecrets);
        writer.Write(level.TotalTreasure);
        writer.Write(level.FoundTreasure);
        writer.Write(level.TotalMonsters);
        writer.Write(level.KilledMonsters);
        writer.Write(level.TimeTics);

        // The mutable tile state.
        for (var x = 0; x < 64; x++)
        {
            for (var y = 0; y < 64; y++)
            {
                writer.Write(level.TileMap[x, y]);
                writer.Write((short)level.WallTexX[x, y]);
                writer.Write((short)level.WallTexY[x, y]);
            }
        }

        // Doors.
        writer.Write(level.Doors.Count);
        foreach (var door in level.Doors)
        {
            writer.Write((int)door.Action);
            writer.Write(door.TicCount);
        }

        // Powerups.
        writer.Write(level.Powerups.Count);
        foreach (var pow in level.Powerups)
        {
            writer.Write(pow.TileX);
            writer.Write(pow.TileY);
            writer.Write((int)pow.Kind);
        }

        // Actors.
        writer.Write(logic.Guards.Count);
        foreach (var guard in logic.Guards)
        {
            writer.Write(guard.X);
            writer.Write(guard.Y);
            writer.Write(guard.Angle);
            writer.Write((int)guard.Kind);
            writer.Write(guard.Health);
            writer.Write(guard.Speed);
            writer.Write(guard.TicCount);
            writer.Write(guard.Temp2);
            writer.Write(guard.Distance);
            writer.Write(guard.TileX);
            writer.Write(guard.TileY);
            writer.Write(guard.AreaNumber);
            writer.Write(guard.WaitForDoorX);
            writer.Write(guard.WaitForDoorY);
            writer.Write((byte)guard.Flags);
            writer.Write((int)guard.State);
            writer.Write((int)guard.Dir);
            writer.Write(guard.SpriteTexture);
        }

        // The pushwall.
        writer.Write(logic.PushWallLogic.Active);
        writer.Write(logic.PushWallLogic.TilesMoved);
        writer.Write(logic.PushWallLogic.PointsMoved);
        writer.Write((int)logic.PushWallLogic.Dir);
        writer.Write(logic.PushWallLogic.X);
        writer.Write(logic.PushWallLogic.Y);
        writer.Write(logic.PushWallLogic.Dx);
        writer.Write(logic.PushWallLogic.Dy);
        writer.Write(logic.PushWallLogic.TexX);
        writer.Write(logic.PushWallLogic.TexY);

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// Restores a save-slot payload into the logic: reloads the level
    /// fresh, then overwrites everything mutable from the stream.
    /// Returns false when the payload is unreadable or from a
    /// different difficulty.
    /// </summary>
    public static bool Restore(WolfLogic logic, byte[] data)
    {
        if (data == null || data.Length < 16)
        {
            return false;
        }

        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version)
        {
            return false;
        }

        var difficulty = (DifficultyLevel)reader.ReadInt32();
        if (difficulty != logic.Difficulty)
        {
            return false;
        }

        var levelIndex = reader.ReadInt32();
        logic.StartLevel(levelIndex);
        logic.Rng.Index = reader.ReadInt32();

        var level = logic.Level;
        var player = logic.Player;

        player.X = reader.ReadInt32();
        player.Y = reader.ReadInt32();
        player.Angle = reader.ReadInt32();
        player.Health = reader.ReadInt32();
        player.Lives = reader.ReadInt32();
        player.Ammo = reader.ReadInt32();
        player.OldScore = reader.ReadInt32();
        player.Score = reader.ReadInt32();
        player.NextExtra = reader.ReadInt32();
        player.Items = (PlayerItems)reader.ReadInt32();
        player.CurrentWeapon = (Weapon)reader.ReadInt32();
        player.PendingWeapon = (Weapon)reader.ReadInt32();
        player.AreaNumber = reader.ReadInt32();
        player.TileX = WolfMath.Pos2Tile(player.X);
        player.TileY = WolfMath.Pos2Tile(player.Y);
        player.Attacking = false;
        player.AttackFrame = player.AttackCount = player.WeaponFrame = 0;
        player.State = PlayState.Playing;

        level.TotalSecrets = reader.ReadInt32();
        level.FoundSecrets = reader.ReadInt32();
        level.TotalTreasure = reader.ReadInt32();
        level.FoundTreasure = reader.ReadInt32();
        level.TotalMonsters = reader.ReadInt32();
        level.KilledMonsters = reader.ReadInt32();
        level.TimeTics = reader.ReadInt64();

        for (var x = 0; x < 64; x++)
        {
            for (var y = 0; y < 64; y++)
            {
                level.TileMap[x, y] = reader.ReadInt64();
                level.WallTexX[x, y] = reader.ReadInt16();
                level.WallTexY[x, y] = reader.ReadInt16();
            }
        }

        var doorCount = reader.ReadInt32();
        if (doorCount != level.Doors.Count)
        {
            return false;
        }

        foreach (var door in level.Doors)
        {
            door.Action = (DoorAction)reader.ReadInt32();
            door.TicCount = reader.ReadInt32();
        }

        level.Powerups.Clear();
        var powerupCount = reader.ReadInt32();
        for (var i = 0; i < powerupCount; i++)
        {
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            var kind = (PowerupKind)reader.ReadInt32();
            level.Powerups.Add(new Powerup(x, y, kind));
        }

        logic.Guards.Clear();
        var guardCount = reader.ReadInt32();
        for (var i = 0; i < guardCount; i++)
        {
            logic.Guards.Add(new Entity
            {
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                Angle = reader.ReadInt32(),
                Kind = (EnemyKind)reader.ReadInt32(),
                Health = reader.ReadInt32(),
                Speed = reader.ReadInt32(),
                TicCount = reader.ReadInt32(),
                Temp2 = reader.ReadInt32(),
                Distance = reader.ReadInt32(),
                TileX = reader.ReadInt32(),
                TileY = reader.ReadInt32(),
                AreaNumber = reader.ReadInt32(),
                WaitForDoorX = reader.ReadInt32(),
                WaitForDoorY = reader.ReadInt32(),
                Flags = (EntityFlags)reader.ReadByte(),
                State = (EnState)reader.ReadInt32(),
                Dir = (Dir8)reader.ReadInt32(),
                SpriteTexture = reader.ReadInt32(),
            });
        }

        logic.PushWallLogic.RestoreFrom(
            reader.ReadBoolean(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            (Dir4)reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());

        logic.DoorLogic.RestoreAreaConnections(player.AreaNumber);
        return true;
    }
}
