//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), files
// wolf3d/code/wolf/wolf_doors.c and wolf_areas.c. The iOS build
// disabled closing an open door with the use key; the original DOS
// behavior is restored here (marked below). Door face textures use the
// VSWAP wall-picture numbering verified on screen in Phase 2.
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

/// <summary>A door's animation state (the original dr_state).</summary>
public enum DoorAction
{
    /// <summary>Sliding closed.</summary>
    Closing,

    /// <summary>Fully closed.</summary>
    Closed,

    /// <summary>Sliding open.</summary>
    Opening,

    /// <summary>Fully open (auto-closes after a timeout).</summary>
    Open,
}

/// <summary>The door kinds (the original DOOR_* type values).</summary>
public enum DoorKind
{
    /// <summary>A plain door in a vertical wall run.</summary>
    Vertical,

    /// <summary>A plain door in a horizontal wall run.</summary>
    Horizontal,

    /// <summary>A gold-locked door (vertical).</summary>
    GoldVertical,

    /// <summary>A gold-locked door (horizontal).</summary>
    GoldHorizontal,

    /// <summary>A silver-locked door (vertical).</summary>
    SilverVertical,

    /// <summary>A silver-locked door (horizontal).</summary>
    SilverHorizontal,

    /// <summary>An elevator door (vertical).</summary>
    ElevatorVertical,

    /// <summary>An elevator door (horizontal).</summary>
    ElevatorHorizontal,
}

/// <summary>One sliding door.</summary>
public sealed class WolfDoor
{
    /// <summary>Creates a closed door.</summary>
    public WolfDoor(int tileX, int tileY, DoorKind kind, bool vertical, int texture)
    {
        TileX = tileX;
        TileY = tileY;
        Kind = kind;
        Vertical = vertical;
        Texture = texture;
        Action = DoorAction.Closed;
    }

    /// <summary>The tile x position.</summary>
    public int TileX { get; }

    /// <summary>The tile y position (y-up).</summary>
    public int TileY { get; }

    /// <summary>The door kind (lock/elevator variants).</summary>
    public DoorKind Kind { get; }

    /// <summary>True when the door sits in a vertical wall run (slides along y).</summary>
    public bool Vertical { get; }

    /// <summary>The door face texture in the VSWAP wall numbering.</summary>
    public int Texture { get; }

    /// <summary>The animation state.</summary>
    public DoorAction Action { get; set; }

    /// <summary>The slide/hold tic counter (0..63 while sliding).</summary>
    public int TicCount { get; set; }

    /// <summary>The area on one side (sound propagation).</summary>
    public int Area1 { get; set; }

    /// <summary>The area on the other side.</summary>
    public int Area2 { get; set; }
}

/// <summary>
/// The door state machines and the connected-area bookkeeping that
/// drives sound propagation (areabyplayer).
/// </summary>
public sealed class DoorLogic
{
    /// <summary>A door slides over this many tics (and this many 1/64 openings).</summary>
    public const int DoorFullOpen = 63;

    /// <summary>Tics an open door waits before auto-closing.</summary>
    public const int DoorTimeout = 300;

    /// <summary>The minimum remaining-open tics while something blocks the doorway.</summary>
    public const int DoorMinOpen = 50;

    private readonly WolfLogic logic;
    private readonly byte[,] areaConnect = new byte[LevelState.NumAreas, LevelState.NumAreas];

    /// <summary>Creates the door logic bound to its game context.</summary>
    public DoorLogic(WolfLogic logic)
    {
        this.logic = logic;
    }

    /// <summary>Which areas currently connect to the player's area.</summary>
    public bool[] AreaByPlayer { get; } = new bool[LevelState.NumAreas];

    /// <summary>Spawns a door from a raw wall-plane value (90-101).</summary>
    public static WolfDoor SpawnDoor(LevelState level, int x, int y, int rawValue)
    {
        // Door face textures in VSWAP wall numbering (Phase 2 verified):
        // plain 99/98, locked 105/104, elevator 103/102.
        var door = rawValue switch
        {
            90 => new WolfDoor(x, y, DoorKind.Vertical, true, 99),
            91 => new WolfDoor(x, y, DoorKind.Horizontal, false, 98),
            92 => new WolfDoor(x, y, DoorKind.GoldVertical, true, 105),
            93 => new WolfDoor(x, y, DoorKind.GoldHorizontal, false, 104),
            94 => new WolfDoor(x, y, DoorKind.SilverVertical, true, 105),
            95 => new WolfDoor(x, y, DoorKind.SilverHorizontal, false, 104),
            100 => new WolfDoor(x, y, DoorKind.ElevatorVertical, true, 103),
            101 => new WolfDoor(x, y, DoorKind.ElevatorHorizontal, false, 102),
            _ => throw new ArgumentOutOfRangeException(nameof(rawValue), $"Not a door value: {rawValue}"),
        };

        level.DoorMap[x, y] = door;
        level.Doors.Add(door);
        return door;
    }

    /// <summary>Assigns each door the areas on its two open sides.</summary>
    public static void SetDoorAreas(LevelState level)
    {
        foreach (var door in level.Doors)
        {
            var x = door.TileX;
            var y = door.TileY;
            if (door.Vertical)
            {
                door.Area1 = level.Areas[x + 1, y] >= 0 ? level.Areas[x + 1, y] : 0;
                door.Area2 = level.Areas[x - 1, y] >= 0 ? level.Areas[x - 1, y] : 0;
            }
            else
            {
                door.Area1 = level.Areas[x, y + 1] >= 0 ? level.Areas[x, y + 1] : 0;
                door.Area2 = level.Areas[x, y - 1] >= 0 ? level.Areas[x, y - 1] : 0;
            }
        }
    }

    /// <summary>
    /// Rebuilds the area-connection matrix from the doors' current
    /// states (used after restoring a save game): every door that is
    /// not fully closed joins its two areas.
    /// </summary>
    public void RestoreAreaConnections(int playerArea)
    {
        Array.Clear(areaConnect, 0, areaConnect.Length);
        foreach (var door in logic.Level.Doors)
        {
            if (door.Action != DoorAction.Closed)
            {
                JoinAreas(door.Area1, door.Area2);
            }
        }

        ConnectAreas(playerArea);
    }

    /// <summary>Clears the connection matrix and marks the player's area.</summary>
    public void InitAreas(int areaNumber)
    {
        Array.Clear(areaConnect, 0, areaConnect.Length);
        Array.Clear(AreaByPlayer, 0, AreaByPlayer.Length);
        AreaByPlayer[areaNumber] = true;
    }

    /// <summary>Recomputes which areas connect to the player's area.</summary>
    public void ConnectAreas(int areaNumber)
    {
        Array.Clear(AreaByPlayer, 0, AreaByPlayer.Length);
        AreaByPlayer[areaNumber] = true;
        RecursiveConnect(areaNumber);
    }

    private void RecursiveConnect(int areaNumber)
    {
        for (var i = 0; i < LevelState.NumAreas; i++)
        {
            if (areaConnect[areaNumber, i] > 0 && !AreaByPlayer[i])
            {
                AreaByPlayer[i] = true;
                RecursiveConnect(i);
            }
        }
    }

    private void JoinAreas(int area1, int area2)
    {
        areaConnect[area1, area2]++;
        areaConnect[area2, area1]++;
    }

    private void DisconnectAreas(int area1, int area2)
    {
        areaConnect[area1, area2]--;
        areaConnect[area2, area1]--;
    }

    /// <summary>Opens a door (or resets an open door's close timer).</summary>
    public static void OpenDoor(WolfDoor door)
    {
        if (door.Action == DoorAction.Open)
        {
            door.TicCount = 0;
        }
        else
        {
            door.Action = DoorAction.Opening;
        }
    }

    private void ChangeDoorState(WolfDoor door)
    {
        if (door.Action < DoorAction.Opening)
        {
            OpenDoor(door);
        }
        else if (door.Action == DoorAction.Open && CanCloseDoor(door.TileX, door.TileY, door.Vertical))
        {
            // Restored DOS behavior (the iOS build disabled closing an
            // open door with the use key).
            door.Action = DoorAction.Closing;
            door.TicCount = DoorFullOpen;
        }
    }

    /// <summary>The use action on a door, honoring key locks.</summary>
    public void TryUse(WolfDoor door, PlayerItems keys)
    {
        switch (door.Kind)
        {
            case DoorKind.Vertical:
            case DoorKind.Horizontal:
            case DoorKind.ElevatorVertical:
            case DoorKind.ElevatorHorizontal:
                ChangeDoorState(door);
                break;

            case DoorKind.GoldVertical:
            case DoorKind.GoldHorizontal:
                if ((keys & PlayerItems.Key1) != 0)
                {
                    ChangeDoorState(door);
                }
                else
                {
                    logic.Notify("You need a gold key");
                }

                break;

            case DoorKind.SilverVertical:
            case DoorKind.SilverHorizontal:
                if ((keys & PlayerItems.Key2) != 0)
                {
                    ChangeDoorState(door);
                }
                else
                {
                    logic.Notify("You need a silver key");
                }

                break;
        }
    }

    private bool CanCloseDoor(int x, int y, bool vertical)
    {
        const int closeWall = WolfMath.MinDist;
        var player = logic.Player;
        var playerTileX = WolfMath.Pos2Tile(player.X);
        var playerTileY = WolfMath.Pos2Tile(player.Y);
        if (playerTileX == x && playerTileY == y)
        {
            return false;
        }

        if (vertical)
        {
            if (playerTileY == y &&
                (WolfMath.Pos2Tile(player.X + closeWall) == x || WolfMath.Pos2Tile(player.X - closeWall) == x))
            {
                return false;
            }

            foreach (var guard in logic.Guards)
            {
                if ((guard.TileX == x && guard.TileY == y) ||
                    (guard.TileX == x - 1 && guard.TileY == y && WolfMath.Pos2Tile(guard.X + closeWall) == x) ||
                    (guard.TileX == x + 1 && guard.TileY == y && WolfMath.Pos2Tile(guard.X - closeWall) == x))
                {
                    return false;
                }
            }
        }
        else
        {
            if (playerTileX == x &&
                (WolfMath.Pos2Tile(player.Y + closeWall) == y || WolfMath.Pos2Tile(player.Y - closeWall) == y))
            {
                return false;
            }

            foreach (var guard in logic.Guards)
            {
                if ((guard.TileX == x && guard.TileY == y) ||
                    (guard.TileX == x && guard.TileY == y - 1 && WolfMath.Pos2Tile(guard.Y + closeWall) == y) ||
                    (guard.TileX == x && guard.TileY == y + 1 && WolfMath.Pos2Tile(guard.Y - closeWall) == y))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Advances every door's state machine by the frame's tics.</summary>
    public void ProcessDoors(int tics)
    {
        foreach (var door in logic.Level.Doors)
        {
            switch (door.Action)
            {
                case DoorAction.Closed:
                    continue;

                case DoorAction.Opening:
                    if (door.TicCount >= DoorFullOpen)
                    {
                        door.Action = DoorAction.Open;
                        door.TicCount = 0;
                    }
                    else
                    {
                        if (door.TicCount == 0)
                        {
                            // Just starting to open: connect the areas.
                            JoinAreas(door.Area1, door.Area2);
                            ConnectAreas(logic.Player.AreaNumber);
                            if (AreaByPlayer[door.Area1])
                            {
                                logic.PlayDigitizedSound(WolfDigitizedSounds.DoorOpen);
                            }
                        }

                        door.TicCount += tics;
                        if (door.TicCount > DoorFullOpen)
                        {
                            door.TicCount = DoorFullOpen;
                        }
                    }

                    break;

                case DoorAction.Closing:
                    if (door.TicCount <= 0)
                    {
                        DisconnectAreas(door.Area1, door.Area2);
                        ConnectAreas(logic.Player.AreaNumber);
                        door.TicCount = 0;
                        door.Action = DoorAction.Closed;
                    }
                    else
                    {
                        if (door.TicCount == DoorFullOpen && AreaByPlayer[door.Area1])
                        {
                            logic.PlayDigitizedSound(WolfDigitizedSounds.DoorClose);
                        }

                        door.TicCount -= tics;
                        if (door.TicCount < 0)
                        {
                            door.TicCount = 0;
                        }
                    }

                    break;

                case DoorAction.Open:
                    if (door.TicCount > DoorMinOpen &&
                        !CanCloseDoor(door.TileX, door.TileY, door.Vertical))
                    {
                        // Something is in the doorway: do not close yet.
                        door.TicCount = DoorMinOpen;
                    }

                    if (door.TicCount >= DoorTimeout)
                    {
                        door.Action = DoorAction.Closing;
                        door.TicCount = DoorFullOpen;
                    }
                    else
                    {
                        door.TicCount += tics;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// How open the door at a tile is: 63 fully open, 0 closed, or the
    /// slide progress while animating.
    /// </summary>
    public static int DoorOpened(LevelState level, int x, int y)
    {
        var door = level.DoorMap[x, y];
        if (door == null)
        {
            return 0;
        }

        return door.Action == DoorAction.Open ? DoorFullOpen : door.TicCount;
    }
}
