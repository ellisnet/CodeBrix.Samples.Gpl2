//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), files
// wolf3d/code/wolf/wolf_actors.c (spawning, the per-actor state
// machine pump, the sprite-rotation pass), wolf_ai_com.c (the
// direction-choosing AI, sight checks, movement and the think
// functions) and wolf_actor_ai.c (sighting, damage, death). Function
// names keep the original T_/A_/AI_ prefixes for traceability.
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

namespace Wolfenstein.Brix.GameEngine.Logic;

/// <summary>The actor system: spawning, state machines, AI and damage.</summary>
public sealed class ActorLogic
{
    /// <summary>Minimum fixed-point distance kept between actor and player centers.</summary>
    public const int MinActorDist = 0x10000;

    // Sprite-rotation offsets by view direction (add8dir).
    private static readonly int[] Add8Dir = { 4, 5, 6, 7, 0, 1, 2, 3, 0 };

    private readonly WolfLogic logic;

    /// <summary>Creates the actor logic bound to its game context.</summary>
    public ActorLogic(WolfLogic logic)
    {
        this.logic = logic;
    }

    private LevelState Level => logic.Level;

    private PlayerState Player => logic.Player;

    private WolfRandom Rng => logic.Rng;

    private List<Entity> Guards => logic.Guards;

    /// <summary>Changes an actor's state and loads the state's timeout (A_StateChange).</summary>
    public static void StateChange(Entity ent, EnState newState)
    {
        ent.State = newState;
        ent.TicCount = newState == EnState.Remove
            ? 0
            : ActorTables.Get(ent.Kind, newState).Timeout;
    }

    /// <summary>Spawns a bare actor (SpawnActor).</summary>
    public Entity SpawnActor(EnemyKind which, int x, int y, Dir4 dir)
    {
        var actor = new Entity
        {
            X = WolfMath.Tile2Pos(x),
            Y = WolfMath.Tile2Pos(y),
            TileX = x,
            TileY = y,
            Angle = WolfMath.Dir4Angle[(int)dir],
            Dir = WolfMath.Dir4To8[(int)dir],
            Kind = which,
            Health = ActorTables.StartHitPoints[(int)logic.Difficulty][(int)which],
        };

        actor.AreaNumber = Level.Areas[x, y];
        if (actor.AreaNumber < 0)
        {
            // Ambush marker tiles are listed as -3 area.
            actor.AreaNumber = 0;
        }

        Guards.Add(actor);
        return actor;
    }

    /// <summary>Spawns a standing enemy (SpawnStand).</summary>
    public void SpawnStand(EnemyKind which, int x, int y, int dir)
    {
        var self = SpawnActor(which, x, y, (Dir4)dir);
        self.State = EnState.Stand;
        self.Speed = ActorTables.SpeedPatrol;
        var timeout = ActorTables.Get(which, EnState.Stand).Timeout;
        self.TicCount = timeout != 0 ? Rng.Next() % timeout + 1 : 0;
        self.Flags |= EntityFlags.Shootable;
        if ((Level.TileMap[x, y] & TileFlag.Ambush) != 0)
        {
            self.Flags |= EntityFlags.Ambush;
        }

        Level.TotalMonsters++;
    }

    /// <summary>Spawns a patrolling enemy (SpawnPatrol).</summary>
    public void SpawnPatrol(EnemyKind which, int x, int y, int dir)
    {
        var self = SpawnActor(which, x, y, (Dir4)dir);
        self.State = EnState.Path1;
        self.Speed = which == EnemyKind.Dog ? ActorTables.SpeedDog : ActorTables.SpeedPatrol;
        self.Distance = WolfMath.TileGlobal;
        var timeout = ActorTables.Get(which, EnState.Path1).Timeout;
        self.TicCount = timeout != 0 ? Rng.Next() % timeout + 1 : 0;
        self.Flags |= EntityFlags.Shootable;

        Level.TotalMonsters++;
    }

    /// <summary>Spawns a corpse (SpawnDeadGuard).</summary>
    public void SpawnDeadGuard(EnemyKind which, int x, int y)
    {
        var self = SpawnActor(which, x, y, Dir4.NoDir);
        self.State = EnState.Dead;
        self.Speed = 0;
        self.Health = 0;
        self.TicCount = 0;
    }

    /// <summary>Spawns a boss (SpawnBoss; Hans faces south).</summary>
    public void SpawnBoss(EnemyKind which, int x, int y)
    {
        var face = which switch
        {
            EnemyKind.Boss or EnemyKind.Schabbs or EnemyKind.Hitler => Dir4.South,
            EnemyKind.Fake => Dir4.North,
            _ => Dir4.NoDir,
        };

        var self = SpawnActor(which, x, y, face);
        self.State = EnState.Stand;
        self.Speed = ActorTables.SpeedPatrol;
        var timeout = ActorTables.Get(which, EnState.Stand).Timeout;
        self.TicCount = timeout != 0 ? Rng.Next() % timeout + 1 : 0;
        self.Flags |= EntityFlags.Shootable | EntityFlags.Ambush;

        Level.TotalMonsters++;
    }

    /// <summary>
    /// Runs every actor's state machine and think function, then picks
    /// each actor's sprite for the frame (ProcessGuards + DoGuard).
    /// </summary>
    public void ProcessGuards(int tics)
    {
        for (var n = 0; n < Guards.Count; n++)
        {
            var ent = Guards[n];
            if (!DoGuard(ent, tics))
            {
                Guards.RemoveAt(n--);
                continue;
            }

            var state = ActorTables.Get(ent.Kind, ent.State);
            var tex = state.Texture;
            if (state.Rotate)
            {
                var viewAngle = AngleWise(
                    WolfMath.Fine2Rad(Player.Angle), WolfMath.Fine2Rad(ent.Angle));
                tex += Add8Dir[(int)WolfMath.Get8dir(viewAngle)];
            }

            ent.SpriteTexture = tex;
        }
    }

    private bool DoGuard(Entity ent, int tics)
    {
        // Tic counts fire discrete actions separate from think functions.
        if (ent.TicCount != 0)
        {
            ent.TicCount -= tics;
            while (ent.TicCount <= 0)
            {
                var action = ActorTables.Get(ent.Kind, ent.State).ActionFunc;
                if (action != Think.None)
                {
                    RunThink(action, ent);
                    if (ent.State == EnState.Remove)
                    {
                        return false;
                    }
                }

                ent.State = ActorTables.Get(ent.Kind, ent.State).NextState;
                if (ent.State == EnState.Remove)
                {
                    return false;
                }

                var timeout = ActorTables.Get(ent.Kind, ent.State).Timeout;
                if (timeout == 0)
                {
                    ent.TicCount = 0;
                    break;
                }

                ent.TicCount += timeout;
            }
        }

        var think = ActorTables.Get(ent.Kind, ent.State).ThinkFunc;
        if (think != Think.None)
        {
            RunThink(think, ent, tics);
            if (ent.State == EnState.Remove)
            {
                return false;
            }
        }

        return true;
    }

    private void RunThink(Think think, Entity self, int tics = 1)
    {
        switch (think)
        {
            case Think.Stand:
                T_Stand(self, tics);
                break;
            case Think.Path:
                T_Path(self, tics);
                break;
            case Think.Chase:
                T_Chase(self, tics);
                break;
            case Think.DogChase:
                T_DogChase(self, tics);
                break;
            case Think.Shoot:
                T_Shoot(self, tics);
                break;
            case Think.Bite:
                T_Bite(self);
                break;
            case Think.DeathScream:
                A_DeathScream(self);
                break;
        }
    }

    // ---------------------------------------------------------------
    // Direction-choosing AI (wolf_ai_com.c)
    // ---------------------------------------------------------------

    private bool AI_ChangeDir(Entity self, Dir8 newDir)
    {
        var oldX = WolfMath.Pos2Tile(self.X);
        var oldY = WolfMath.Pos2Tile(self.Y);
        var newX = oldX + WolfMath.Dx8Dir[(int)newDir];
        var newY = oldY + WolfMath.Dy8Dir[(int)newDir];
        if (newX < 0 || newX > 63 || newY < 0 || newY > 63)
        {
            return false;
        }

        var waitForDoor = false;
        if (((int)newDir & 0x01) != 0)
        {
            // Diagonal: all three touched tiles must be clear.
            if ((Level.TileMap[newX, oldY] & TileFlag.SolidTile) != 0 ||
                (Level.TileMap[oldX, newY] & TileFlag.SolidTile) != 0 ||
                (Level.TileMap[newX, newY] & TileFlag.SolidTile) != 0)
            {
                return false;
            }

            foreach (var guard in Guards)
            {
                if (guard.State >= EnState.Die1)
                {
                    continue;
                }

                if ((guard.TileX == newX && guard.TileY == newY) ||
                    (guard.TileX == oldX && guard.TileY == newY) ||
                    (guard.TileX == newX && guard.TileY == oldY))
                {
                    return false;
                }
            }
        }
        else
        {
            // Cardinal.
            if ((Level.TileMap[newX, newY] & TileFlag.SolidTile) != 0)
            {
                return false;
            }

            if ((Level.TileMap[newX, newY] & TileFlag.Door) != 0)
            {
                if (self.Kind == EnemyKind.Fake || self.Kind == EnemyKind.Dog)
                {
                    // They cannot open doors.
                    if (Level.DoorMap[newX, newY].Action != DoorAction.Open)
                    {
                        return false;
                    }
                }
                else
                {
                    self.WaitForDoorX = newX;
                    self.WaitForDoorY = newY;
                    waitForDoor = true;
                }
            }

            if (!waitForDoor)
            {
                foreach (var guard in Guards)
                {
                    if (guard.State >= EnState.Die1)
                    {
                        continue;
                    }

                    if (guard.TileX == newX && guard.TileY == newY)
                    {
                        return false;
                    }
                }
            }
        }

        self.TileX = newX;
        self.TileY = newY;

        Level.TileMap[oldX, oldY] &= ~TileFlag.Actor;
        Level.TileMap[newX, newY] |= TileFlag.Actor;

        if (Level.Areas[newX, newY] > 0)
        {
            // Ambush tiles have no valid area; keep the old area there.
            self.AreaNumber = Level.Areas[newX, newY];
        }

        self.Distance = WolfMath.TileGlobal;
        self.Dir = newDir;
        return true;
    }

    private void AI_Path(Entity self)
    {
        var tileInfo = Level.TileMap[WolfMath.Pos2Tile(self.X), WolfMath.Pos2Tile(self.Y)];
        if ((tileInfo & TileFlag.Waypoint) != 0)
        {
            if ((tileInfo & TileFlag.TurnE) != 0)
            {
                self.Dir = Dir8.East;
            }
            else if ((tileInfo & TileFlag.TurnNE) != 0)
            {
                self.Dir = Dir8.NorthEast;
            }
            else if ((tileInfo & TileFlag.TurnN) != 0)
            {
                self.Dir = Dir8.North;
            }
            else if ((tileInfo & TileFlag.TurnNW) != 0)
            {
                self.Dir = Dir8.NorthWest;
            }
            else if ((tileInfo & TileFlag.TurnW) != 0)
            {
                self.Dir = Dir8.West;
            }
            else if ((tileInfo & TileFlag.TurnSW) != 0)
            {
                self.Dir = Dir8.SouthWest;
            }
            else if ((tileInfo & TileFlag.TurnS) != 0)
            {
                self.Dir = Dir8.South;
            }
            else if ((tileInfo & TileFlag.TurnSE) != 0)
            {
                self.Dir = Dir8.SouthEast;
            }
        }

        if (!AI_ChangeDir(self, self.Dir))
        {
            self.Dir = Dir8.NoDir;
        }
    }

    private void AI_Dodge(Entity self)
    {
        var dirtry = new Dir8[5];
        Dir8 turnaround;

        if ((self.Flags & EntityFlags.FirstAttack) != 0)
        {
            // Turning around is only OK the very first time after
            // noticing the player.
            turnaround = Dir8.NoDir;
            self.Flags &= ~EntityFlags.FirstAttack;
        }
        else
        {
            turnaround = WolfMath.Opposite8[(int)self.Dir];
        }

        var deltaX = WolfMath.Pos2Tile(Player.X) - WolfMath.Pos2Tile(self.X);
        var deltaY = WolfMath.Pos2Tile(Player.Y) - WolfMath.Pos2Tile(self.Y);

        // Arrange 5 direction choices in order of preference.
        if (deltaX > 0)
        {
            dirtry[1] = Dir8.East;
            dirtry[3] = Dir8.West;
        }
        else
        {
            dirtry[1] = Dir8.West;
            dirtry[3] = Dir8.East;
        }

        if (deltaY > 0)
        {
            dirtry[2] = Dir8.North;
            dirtry[4] = Dir8.South;
        }
        else
        {
            dirtry[2] = Dir8.South;
            dirtry[4] = Dir8.North;
        }

        // Randomize a bit for dodging.
        if (Math.Abs(deltaX) > Math.Abs(deltaY))
        {
            (dirtry[1], dirtry[2]) = (dirtry[2], dirtry[1]);
            (dirtry[3], dirtry[4]) = (dirtry[4], dirtry[3]);
        }

        if (Rng.Next() < 128)
        {
            (dirtry[1], dirtry[2]) = (dirtry[2], dirtry[1]);
            (dirtry[3], dirtry[4]) = (dirtry[4], dirtry[3]);
        }

        dirtry[0] = WolfMath.Diagonal(dirtry[1], dirtry[2]);

        for (var i = 0; i < 5; i++)
        {
            if (dirtry[i] == Dir8.NoDir || dirtry[i] == turnaround)
            {
                continue;
            }

            if (AI_ChangeDir(self, dirtry[i]))
            {
                return;
            }
        }

        if (turnaround != Dir8.NoDir && AI_ChangeDir(self, turnaround))
        {
            return;
        }

        self.Dir = Dir8.NoDir;
    }

    private void AI_Chase(Entity self)
    {
        var olddir = self.Dir;
        var turnaround = WolfMath.Opposite8[(int)olddir];
        var d0 = Dir8.NoDir;
        var d1 = Dir8.NoDir;

        var deltaX = WolfMath.Pos2Tile(Player.X) - WolfMath.Pos2Tile(self.X);
        var deltaY = WolfMath.Pos2Tile(Player.Y) - WolfMath.Pos2Tile(self.Y);

        if (deltaX > 0)
        {
            d0 = Dir8.East;
        }
        else if (deltaX < 0)
        {
            d0 = Dir8.West;
        }

        if (deltaY > 0)
        {
            d1 = Dir8.North;
        }
        else if (deltaY < 0)
        {
            d1 = Dir8.South;
        }

        if (Math.Abs(deltaY) > Math.Abs(deltaX))
        {
            (d0, d1) = (d1, d0);
        }

        if (d0 == turnaround)
        {
            d0 = Dir8.NoDir;
        }

        if (d1 == turnaround)
        {
            d1 = Dir8.NoDir;
        }

        if (d0 != Dir8.NoDir && AI_ChangeDir(self, d0))
        {
            return;
        }

        if (d1 != Dir8.NoDir && AI_ChangeDir(self, d1))
        {
            return;
        }

        // No direct path to the player; pick another direction.
        if (olddir != Dir8.NoDir && AI_ChangeDir(self, olddir))
        {
            return;
        }

        if (Rng.Next() > 128)
        {
            for (var tdir = (int)Dir8.East; tdir <= (int)Dir8.South; tdir += 2)
            {
                if ((Dir8)tdir != turnaround && AI_ChangeDir(self, (Dir8)tdir))
                {
                    return;
                }
            }
        }
        else
        {
            for (var tdir = (int)Dir8.South; tdir >= (int)Dir8.East; tdir -= 2)
            {
                if ((Dir8)tdir != turnaround && AI_ChangeDir(self, (Dir8)tdir))
                {
                    return;
                }
            }
        }

        if (turnaround != Dir8.NoDir && AI_ChangeDir(self, turnaround))
        {
            return;
        }

        self.Dir = Dir8.NoDir;
    }

    // ---------------------------------------------------------------
    // Sight and target acquisition
    // ---------------------------------------------------------------

    private bool AI_CheckSight(Entity self)
    {
        const int minSight = 0x18000;

        // Don't bother tracing a line if the area isn't connected to
        // the player's.
        if ((self.Flags & EntityFlags.Ambush) == 0 && !logic.DoorLogic.AreaByPlayer[self.AreaNumber])
        {
            return false;
        }

        // If the player is real close, sight is automatic.
        var deltaX = Player.X - self.X;
        var deltaY = Player.Y - self.Y;
        if (Math.Abs(deltaX) < minSight && Math.Abs(deltaY) < minSight)
        {
            return true;
        }

        // See if they are looking in the right direction.
        switch (self.Dir)
        {
            case Dir8.North:
                if (deltaY < 0)
                {
                    return false;
                }

                break;

            case Dir8.East:
                if (deltaX < 0)
                {
                    return false;
                }

                break;

            case Dir8.South:
                if (deltaY > 0)
                {
                    return false;
                }

                break;

            case Dir8.West:
                if (deltaX > 0)
                {
                    return false;
                }

                break;
        }

        return logic.CheckLine(self.X, self.Y, Player.X, Player.Y);
    }

    private bool AI_FindTarget(Entity self, int tics)
    {
        if (self.Temp2 != 0)
        {
            // Count down the reaction time.
            self.Temp2 -= tics;
            if (self.Temp2 > 0)
            {
                return false;
            }

            self.Temp2 = 0;
        }
        else
        {
            if ((self.Flags & EntityFlags.Ambush) == 0 && !logic.DoorLogic.AreaByPlayer[self.AreaNumber])
            {
                return false;
            }

            if (!AI_CheckSight(self))
            {
                if ((self.Flags & EntityFlags.Ambush) != 0 || !Player.MadeNoise)
                {
                    return false;
                }
            }

            self.Flags &= ~EntityFlags.Ambush;

            // We see/hear the player: set the reaction delay.
            self.Temp2 = self.Kind switch
            {
                EnemyKind.Guard => 1 + Rng.Next() / 4,
                EnemyKind.Officer => 2,
                EnemyKind.Mutant => 1 + Rng.Next() / 6,
                EnemyKind.SS => 1 + Rng.Next() / 6,
                EnemyKind.Dog => 1 + Rng.Next() / 8,
                _ => 1,
            };

            return false; // Amazed and waiting to understand what to do.
        }

        A_FirstSighting(self);
        return true;
    }

    // ---------------------------------------------------------------
    // Movement
    // ---------------------------------------------------------------

    private void T_Move(Entity self, int dist)
    {
        if (self.Dir == Dir8.NoDir || dist == 0)
        {
            return;
        }

        self.X += dist * WolfMath.Dx8Dir[(int)self.Dir];
        self.Y += dist * WolfMath.Dy8Dir[(int)self.Dir];

        // Never move on top of the player.
        if (Math.Abs(self.X - Player.X) <= MinActorDist &&
            Math.Abs(self.Y - Player.Y) <= MinActorDist)
        {
            self.X -= dist * WolfMath.Dx8Dir[(int)self.Dir];
            self.Y -= dist * WolfMath.Dy8Dir[(int)self.Dir];
            return;
        }

        self.Distance -= dist;
        if (self.Distance < 0)
        {
            self.Distance = 0;
        }
    }

    private void T_Advance(Entity self, Action<Entity> think, int tics)
    {
        var move = self.Speed * tics;
        while (move > 0)
        {
            // Waiting for a door to open.
            if (self.WaitForDoorX != 0)
            {
                var door = Level.DoorMap[self.WaitForDoorX, self.WaitForDoorY];
                DoorLogic.OpenDoor(door);
                if (door.Action != DoorAction.Open)
                {
                    return;
                }

                self.WaitForDoorX = self.WaitForDoorY = 0;
            }

            if (move < self.Distance)
            {
                T_Move(self, move);
                break;
            }

            // Fix the position to account for round-off during moving.
            self.X = WolfMath.Tile2Pos(self.TileX);
            self.Y = WolfMath.Tile2Pos(self.TileY);

            move -= self.Distance;

            think(self);
            self.Angle = WolfMath.Dir8Angle[(int)self.Dir];
            if (self.Dir == Dir8.NoDir)
            {
                return;
            }
        }
    }

    // ---------------------------------------------------------------
    // Think functions
    // ---------------------------------------------------------------

    private void T_Stand(Entity self, int tics) => AI_FindTarget(self, tics);

    private void T_Path(Entity self, int tics)
    {
        if (AI_FindTarget(self, tics))
        {
            return;
        }

        if (self.Speed == 0)
        {
            return;
        }

        if (self.Dir == Dir8.NoDir)
        {
            AI_Path(self);
            if (self.Dir == Dir8.NoDir)
            {
                return;
            }
        }

        T_Advance(self, AI_Path, tics);
    }

    private void T_Chase(Entity self, int tics)
    {
        var dodge = false;
        if (logic.CheckLine(self.X, self.Y, Player.X, Player.Y))
        {
            // Got a shot at the player?
            var dx = Math.Abs(WolfMath.Pos2Tile(self.X) - WolfMath.Pos2Tile(Player.X));
            var dy = Math.Abs(WolfMath.Pos2Tile(self.Y) - WolfMath.Pos2Tile(Player.Y));
            var dist = Math.Max(dx, dy);
            int chance;
            if (dist == 0 || (dist == 1 && self.Distance < 16))
            {
                chance = 300;
            }
            else
            {
                chance = (tics << 4) / dist;
            }

            if (Rng.Next() < chance)
            {
                StateChange(self, EnState.Shoot1);
                return;
            }

            dodge = true;
        }

        if (self.Dir == Dir8.NoDir)
        {
            if (dodge)
            {
                AI_Dodge(self);
            }
            else
            {
                AI_Chase(self);
            }

            if (self.Dir == Dir8.NoDir)
            {
                return;
            }

            self.Angle = WolfMath.Dir8Angle[(int)self.Dir];
        }

        T_Advance(self, dodge ? AI_Dodge : AI_Chase, tics);
    }

    private void T_DogChase(Entity self, int tics)
    {
        if (self.Dir == Dir8.NoDir)
        {
            AI_Dodge(self);
            self.Angle = WolfMath.Dir8Angle[(int)self.Dir];
            if (self.Dir == Dir8.NoDir)
            {
                return;
            }
        }

        // Check for bite range.
        var dx = Math.Abs(Player.X - self.X) - WolfMath.TileGlobal / 2;
        if (dx <= MinActorDist)
        {
            var dy = Math.Abs(Player.Y - self.Y) - WolfMath.TileGlobal / 2;
            if (dy <= MinActorDist)
            {
                StateChange(self, EnState.Shoot1);
                return; // Bite the player!
            }
        }

        T_Advance(self, AI_Dodge, tics);
    }

    private void T_Bite(Entity self)
    {
        logic.PlayDigitizedSound(WolfDigitizedSounds.DogBark);

        var dx = Math.Abs(Player.X - self.X) - WolfMath.TileGlobal;
        if (dx <= MinActorDist)
        {
            var dy = Math.Abs(Player.Y - self.Y) - WolfMath.TileGlobal;
            if (dy <= MinActorDist && Rng.Next() < 180)
            {
                logic.PlayerLogic.Damage(self, Rng.Next() >> 4);
            }
        }
    }

    private void T_Shoot(Entity self, int tics)
    {
        if (!logic.DoorLogic.AreaByPlayer[self.AreaNumber])
        {
            return;
        }

        if (!logic.CheckLine(self.X, self.Y, Player.X, Player.Y))
        {
            return; // The player is behind a wall.
        }

        var dx = Math.Abs(WolfMath.Pos2Tile(self.X) - WolfMath.Pos2Tile(Player.X));
        var dy = Math.Abs(WolfMath.Pos2Tile(self.Y) - WolfMath.Pos2Tile(Player.Y));
        var dist = Math.Max(dx, dy);
        if (self.Kind == EnemyKind.SS || self.Kind == EnemyKind.Boss)
        {
            dist = dist * 2 / 3; // SS are better shots.
        }

        var hitchance = Player.Speed >= PlayerState.RunSpeed ? 160 : 256;

        // If the player can see the attacker, they can dodge.
        var attackerAngle = WolfMath.TransformPoint(self.X, self.Y, Player.X, Player.Y);
        if (AngleDiff(attackerAngle, WolfMath.Fine2Rad(Player.Angle)) < Math.PI / 3.0)
        {
            hitchance -= dist * 16;
        }
        else
        {
            hitchance -= dist * 8;
        }

        if (Rng.Next() < hitchance)
        {
            int damage;
            if (dist < 2)
            {
                damage = Rng.Next() >> 2;
            }
            else if (dist < 4)
            {
                damage = Rng.Next() >> 3;
            }
            else
            {
                damage = Rng.Next() >> 4;
            }

            logic.PlayerLogic.Damage(self, damage);
        }

        switch (self.Kind)
        {
            case EnemyKind.SS:
                logic.PlayDigitizedSound(WolfDigitizedSounds.SSFire);
                break;
            case EnemyKind.Boss:
                logic.PlayDigitizedSound(WolfDigitizedSounds.BossFire);
                break;
            default:
                logic.PlayDigitizedSound(WolfDigitizedSounds.GuardFire);
                break;
        }
    }

    // ---------------------------------------------------------------
    // Sighting, damage and death (wolf_actor_ai.c)
    // ---------------------------------------------------------------

    private void A_DeathScream(Entity self)
    {
        switch (self.Kind)
        {
            case EnemyKind.Guard:
                logic.PlayDigitizedSound(
                    WolfDigitizedSounds.GuardDeathScreams[
                        Rng.Next() % WolfDigitizedSounds.GuardDeathScreams.Length]);
                break;
            case EnemyKind.Officer:
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibOfficerDeath);
                break;
            case EnemyKind.SS:
                logic.PlayDigitizedSound(WolfDigitizedSounds.SSDeath);
                break;
            case EnemyKind.Dog:
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibDogDeath);
                break;
            case EnemyKind.Boss:
                logic.PlayDigitizedSound(WolfDigitizedSounds.HansDeath);
                break;
        }
    }

    /// <summary>
    /// Puts an actor into attack mode, possibly reversing direction if
    /// the player is behind it (A_FirstSighting).
    /// </summary>
    public void A_FirstSighting(Entity self)
    {
        switch (self.Kind)
        {
            case EnemyKind.Guard:
                logic.PlayDigitizedSound(WolfDigitizedSounds.GuardAlert);
                self.Speed *= 3;
                break;

            case EnemyKind.Officer:
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibOfficerAlert);
                self.Speed *= 5;
                break;

            case EnemyKind.Mutant:
                self.Speed *= 3;
                break;

            case EnemyKind.SS:
                logic.PlayDigitizedSound(WolfDigitizedSounds.SSAlert);
                self.Speed *= 4;
                break;

            case EnemyKind.Dog:
                logic.PlayDigitizedSound(WolfDigitizedSounds.DogBark);
                self.Speed *= 2;
                break;

            case EnemyKind.Boss:
                logic.PlayDigitizedSound(WolfDigitizedSounds.HansAlert);
                self.Speed = ActorTables.SpeedPatrol * 3;
                break;

            default:
                return;
        }

        StateChange(self, EnState.Chase1);
        if (self.WaitForDoorX != 0)
        {
            self.WaitForDoorX = self.WaitForDoorY = 0;
        }

        self.Dir = Dir8.NoDir;
        self.Flags |= EntityFlags.AttackMode | EntityFlags.FirstAttack;
    }

    private void A_KillActor(Entity self)
    {
        var tileX = self.TileX = WolfMath.Pos2Tile(self.X);
        var tileY = self.TileY = WolfMath.Pos2Tile(self.Y);

        switch (self.Kind)
        {
            case EnemyKind.Guard:
                logic.PlayerLogic.GivePoints(100);
                logic.PlayerLogic.SpawnPowerup(tileX, tileY, PowerupKind.Clip2);
                break;

            case EnemyKind.Officer:
                logic.PlayerLogic.GivePoints(400);
                logic.PlayerLogic.SpawnPowerup(tileX, tileY, PowerupKind.Clip2);
                break;

            case EnemyKind.Mutant:
                logic.PlayerLogic.GivePoints(700);
                logic.PlayerLogic.SpawnPowerup(tileX, tileY, PowerupKind.Clip2);
                break;

            case EnemyKind.SS:
                logic.PlayerLogic.GivePoints(500);
                logic.PlayerLogic.SpawnPowerup(
                    tileX, tileY,
                    (Player.Items & PlayerItems.Weapon3) != 0 ? PowerupKind.Clip2 : PowerupKind.MachineGun);
                break;

            case EnemyKind.Dog:
                logic.PlayerLogic.GivePoints(200);
                break;

            case EnemyKind.Boss:
                logic.PlayerLogic.GivePoints(5000);
                logic.PlayerLogic.SpawnPowerup(tileX, tileY, PowerupKind.Key1);
                break;
        }

        StateChange(self, EnState.Die1);
        Level.KilledMonsters++;
        self.Flags &= ~EntityFlags.Shootable;
        self.Flags |= EntityFlags.NonMark;
    }

    /// <summary>
    /// Damages an enemy, stunning or killing it (A_DamageActor).
    /// </summary>
    public void DamageActor(Entity self, int damage)
    {
        Player.MadeNoise = true;

        // Double damage when shooting a non-attack-mode actor.
        if ((self.Flags & EntityFlags.AttackMode) == 0)
        {
            damage <<= 1;
        }

        self.Health -= damage;

        if (self.Health <= 0)
        {
            A_KillActor(self);
        }
        else
        {
            if ((self.Flags & EntityFlags.AttackMode) == 0)
            {
                A_FirstSighting(self); // Put into combat mode.
            }

            switch (self.Kind)
            {
                case EnemyKind.Guard:
                case EnemyKind.Officer:
                case EnemyKind.Mutant:
                case EnemyKind.SS:
                    StateChange(self, (self.Health & 1) != 0 ? EnState.Pain : EnState.Pain1);
                    break;
            }
        }
    }

    private static double AngleDiff(double angle1, double angle2)
    {
        var d = angle1 > angle2 ? angle1 - angle2 : angle2 - angle1;
        return d > Math.PI ? 2.0 * Math.PI - d : d;
    }

    private static double AngleWise(double angle1, double angle2) =>
        angle1 > angle2 ? angle1 - angle2 : angle1 + 2.0 * Math.PI - angle2;
}
