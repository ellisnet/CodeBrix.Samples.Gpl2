//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/code/wolf/wolf_actors.h (entity_t, enemy_t, en_state and the
// actor flags).
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

/// <summary>
/// The enemy families, in the original order (the state and hit-point
/// tables index by this value). Only the first ten appear in the
/// shareware episode's data.
/// </summary>
public enum EnemyKind
{
    /// <summary>A brown-uniformed guard.</summary>
    Guard,

    /// <summary>A white-uniformed officer.</summary>
    Officer,

    /// <summary>A blue-uniformed SS trooper.</summary>
    SS,

    /// <summary>An attack dog.</summary>
    Dog,

    /// <summary>Hans Groesse, the episode 1 boss.</summary>
    Boss,

    /// <summary>Dr. Schabbs (episode 2; not in shareware).</summary>
    Schabbs,

    /// <summary>Fake Hitler (episode 3; not in shareware).</summary>
    Fake,

    /// <summary>Mecha Hitler (episode 3; not in shareware).</summary>
    Mecha,

    /// <summary>Hitler (episode 3; not in shareware).</summary>
    Hitler,

    /// <summary>A mutant (episodes 2+; not in shareware).</summary>
    Mutant,
}

/// <summary>The actor states, in the original st_* order.</summary>
public enum EnState
{
    /// <summary>Standing still, watching for the player.</summary>
    Stand,

    /// <summary>Patrol walk frame 1.</summary>
    Path1,

    /// <summary>Patrol pause after frame 1.</summary>
    Path1s,

    /// <summary>Patrol walk frame 2.</summary>
    Path2,

    /// <summary>Patrol walk frame 3.</summary>
    Path3,

    /// <summary>Patrol pause after frame 3.</summary>
    Path3s,

    /// <summary>Patrol walk frame 4.</summary>
    Path4,

    /// <summary>First pain flinch.</summary>
    Pain,

    /// <summary>Second pain flinch.</summary>
    Pain1,

    /// <summary>Attack frame 1.</summary>
    Shoot1,

    /// <summary>Attack frame 2.</summary>
    Shoot2,

    /// <summary>Attack frame 3.</summary>
    Shoot3,

    /// <summary>Attack frame 4.</summary>
    Shoot4,

    /// <summary>Attack frame 5.</summary>
    Shoot5,

    /// <summary>Attack frame 6.</summary>
    Shoot6,

    /// <summary>Attack frame 7.</summary>
    Shoot7,

    /// <summary>Attack frame 8.</summary>
    Shoot8,

    /// <summary>Attack frame 9.</summary>
    Shoot9,

    /// <summary>Chase run frame 1.</summary>
    Chase1,

    /// <summary>Chase pause after frame 1.</summary>
    Chase1s,

    /// <summary>Chase run frame 2.</summary>
    Chase2,

    /// <summary>Chase run frame 3.</summary>
    Chase3,

    /// <summary>Chase pause after frame 3.</summary>
    Chase3s,

    /// <summary>Chase run frame 4.</summary>
    Chase4,

    /// <summary>Death animation frame 1.</summary>
    Die1,

    /// <summary>Death animation frame 2.</summary>
    Die2,

    /// <summary>Death animation frame 3.</summary>
    Die3,

    /// <summary>Death animation frame 4.</summary>
    Die4,

    /// <summary>Death animation frame 5.</summary>
    Die5,

    /// <summary>Death animation frame 6.</summary>
    Die6,

    /// <summary>Death animation frame 7.</summary>
    Die7,

    /// <summary>Death animation frame 8.</summary>
    Die8,

    /// <summary>Death animation frame 9.</summary>
    Die9,

    /// <summary>A corpse.</summary>
    Dead,

    /// <summary>Remove the actor from play (projectiles and BJ only).</summary>
    Remove,
}

/// <summary>The actor behavior flags (FL_* in the original).</summary>
[Flags]
public enum EntityFlags : byte
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>Shots can hit this actor.</summary>
    Shootable = 1,

    /// <summary>Unused bonus flag (kept for table fidelity).</summary>
    Bonus = 2,

    /// <summary>Never mark this actor's tile as occupied.</summary>
    NeverMark = 4,

    /// <summary>The actor has been seen on screen.</summary>
    Visible = 8,

    /// <summary>The actor is in attack mode (has seen the player).</summary>
    AttackMode = 16,

    /// <summary>The first attack after sighting (reaction-time handling).</summary>
    FirstAttack = 32,

    /// <summary>An ambusher: deaf until the player is seen.</summary>
    Ambush = 64,

    /// <summary>Only mark the tile when unoccupied.</summary>
    NonMark = 128,
}

/// <summary>
/// One live actor, in the original entity_t layout: fixed-point
/// position, FINE angle, tic-counted state machine.
/// </summary>
public sealed class Entity
{
    /// <summary>The fixed-point x position (1 tile = 0x10000).</summary>
    public int X { get; set; }

    /// <summary>The fixed-point y position (y-up).</summary>
    public int Y { get; set; }

    /// <summary>The facing angle in FINE units.</summary>
    public int Angle { get; set; }

    /// <summary>The enemy family (indexes the state and hit-point tables).</summary>
    public EnemyKind Kind { get; set; }

    /// <summary>Hit points remaining.</summary>
    public int Health { get; set; }

    /// <summary>The movement speed in fixed-point units per tic-scaled step.</summary>
    public int Speed { get; set; }

    /// <summary>Tics remaining in the current state.</summary>
    public int TicCount { get; set; }

    /// <summary>General-purpose state counter (the original temp2).</summary>
    public int Temp2 { get; set; }

    /// <summary>Fixed-point distance to move before the next path/chase decision.</summary>
    public int Distance { get; set; }

    /// <summary>The tile x the actor occupies.</summary>
    public int TileX { get; set; }

    /// <summary>The tile y the actor occupies.</summary>
    public int TileY { get; set; }

    /// <summary>The area the actor is in (sound propagation).</summary>
    public int AreaNumber { get; set; }

    /// <summary>The tile x of a door the actor is waiting on (0 = none).</summary>
    public int WaitForDoorX { get; set; }

    /// <summary>The tile y of a door the actor is waiting on.</summary>
    public int WaitForDoorY { get; set; }

    /// <summary>The behavior flags.</summary>
    public EntityFlags Flags { get; set; }

    /// <summary>The current state.</summary>
    public EnState State { get; set; }

    /// <summary>The current movement direction.</summary>
    public Dir8 Dir { get; set; }

    /// <summary>The sprite texture chosen for this frame (set by the actor pass).</summary>
    public int SpriteTexture { get; set; }
}
