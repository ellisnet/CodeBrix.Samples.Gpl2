//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/code/wolf/wolf_act_stat.h (the objstate state tables for the
// shareware enemy set and the starthitpoints matrix). Values are
// verbatim; think/action function pointers become Think enum values
// dispatched by ActorLogic.
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

/// <summary>The think/action functions a state can reference.</summary>
public enum Think
{
    /// <summary>No function.</summary>
    None,

    /// <summary>T_Stand: watch for the player.</summary>
    Stand,

    /// <summary>T_Path: follow the patrol path.</summary>
    Path,

    /// <summary>T_Chase: hunt the player, occasionally opening fire.</summary>
    Chase,

    /// <summary>T_DogChase: hunt the player, biting when adjacent.</summary>
    DogChase,

    /// <summary>T_Shoot: fire at the player.</summary>
    Shoot,

    /// <summary>T_Bite: the dog bite attack.</summary>
    Bite,

    /// <summary>A_DeathScream: the death sound.</summary>
    DeathScream,
}

/// <summary>One entry of the actor state table.</summary>
public sealed class StateInfo
{
    /// <summary>Creates a state entry.</summary>
    public StateInfo(bool rotate, int texture, int timeout, Think think, Think action, EnState nextState)
    {
        Rotate = rotate;
        Texture = texture;
        Timeout = timeout;
        ThinkFunc = think;
        ActionFunc = action;
        NextState = nextState;
    }

    /// <summary>True when the sprite has eight view rotations.</summary>
    public bool Rotate { get; }

    /// <summary>The base sprite texture of the state.</summary>
    public int Texture { get; }

    /// <summary>Tics before moving to <see cref="NextState"/> (0 = never).</summary>
    public int Timeout { get; }

    /// <summary>What to do every tic while in the state.</summary>
    public Think ThinkFunc { get; }

    /// <summary>What to do once when the state's timeout expires.</summary>
    public Think ActionFunc { get; }

    /// <summary>The state entered when the timeout expires.</summary>
    public EnState NextState { get; }
}

/// <summary>The translated actor data tables.</summary>
public static class ActorTables
{
    /// <summary>The number of enemy kinds in the hit-point table.</summary>
    public const int NumEnemies = 31;

    /// <summary>Patrol/walk speed in fixed-point units.</summary>
    public const int SpeedPatrol = 512;

    /// <summary>Dog run speed in fixed-point units.</summary>
    public const int SpeedDog = 1500;

    /// <summary>
    /// The state tables, indexed [enemy kind][state]. Rows exist for
    /// the shareware enemy set; higher-episode enemies are null.
    /// </summary>
    public static readonly StateInfo[][] ObjState = BuildObjState();

    /// <summary>
    /// Starting hit points, indexed [difficulty][enemy kind] - the
    /// full original matrix (higher-episode columns included).
    /// </summary>
    public static readonly int[][] StartHitPoints =
    {
        new[] { 25, 50, 100, 1, 850, 850, 200, 800, 500, 45, 25, 25, 25, 25, 850, 850, 850, 0, 0, 0, 0, 100, 0, 0, 0, 5, 1450, 850, 1050, 950, 1250 },
        new[] { 25, 50, 100, 1, 950, 950, 300, 950, 700, 55, 25, 25, 25, 25, 950, 950, 950, 0, 0, 0, 0, 100, 0, 0, 0, 10, 1550, 950, 1150, 1050, 1350 },
        new[] { 25, 50, 100, 1, 1050, 1550, 400, 1050, 800, 55, 25, 25, 25, 25, 1050, 1050, 1050, 0, 0, 0, 0, 100, 0, 0, 0, 15, 1650, 1050, 1250, 1150, 1450 },
        new[] { 25, 50, 100, 1, 1200, 2400, 500, 1200, 900, 65, 25, 25, 25, 25, 1200, 1200, 1200, 0, 0, 0, 0, 100, 0, 0, 0, 25, 2000, 1200, 1400, 1300, 1600 },
    };

    /// <summary>Returns the state entry for an actor kind and state.</summary>
    public static StateInfo Get(EnemyKind kind, EnState state) => ObjState[(int)kind][(int)state];

    private static StateInfo[][] BuildObjState()
    {
        var table = new StateInfo[NumEnemies][];
        table[(int)EnemyKind.Guard] = new StateInfo[]
        {
            new StateInfo(true, Spr.SPR_GRD_S_1, 0, Think.Stand, Think.None, EnState.Stand), // Stand
            new StateInfo(true, Spr.SPR_GRD_W1_1, 20, Think.Path, Think.None, EnState.Path1s), // Path1
            new StateInfo(true, Spr.SPR_GRD_W1_1, 5, Think.None, Think.None, EnState.Path2), // Path1s
            new StateInfo(true, Spr.SPR_GRD_W2_1, 15, Think.Path, Think.None, EnState.Path3), // Path2
            new StateInfo(true, Spr.SPR_GRD_W3_1, 20, Think.Path, Think.None, EnState.Path3s), // Path3
            new StateInfo(true, Spr.SPR_GRD_W3_1, 5, Think.None, Think.None, EnState.Path4), // Path3s
            new StateInfo(true, Spr.SPR_GRD_W4_1, 15, Think.Path, Think.None, EnState.Path1), // Path4
            new StateInfo(false, Spr.SPR_GRD_PAIN_1, 10, Think.None, Think.None, EnState.Chase1), // Pain
            new StateInfo(false, Spr.SPR_GRD_PAIN_2, 10, Think.None, Think.None, EnState.Chase1), // Pain1
            new StateInfo(false, Spr.SPR_GRD_SHOOT1, 20, Think.None, Think.None, EnState.Shoot2), // Shoot1
            new StateInfo(false, Spr.SPR_GRD_SHOOT2, 20, Think.None, Think.Shoot, EnState.Shoot3), // Shoot2
            new StateInfo(false, Spr.SPR_GRD_SHOOT3, 20, Think.None, Think.None, EnState.Chase1), // Shoot3
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot9
            new StateInfo(true, Spr.SPR_GRD_W1_1, 10, Think.Chase, Think.None, EnState.Chase1s), // Chase1
            new StateInfo(true, Spr.SPR_GRD_W1_1, 3, Think.None, Think.None, EnState.Chase2), // Chase1s
            new StateInfo(true, Spr.SPR_GRD_W2_1, 8, Think.Chase, Think.None, EnState.Chase3), // Chase2
            new StateInfo(true, Spr.SPR_GRD_W3_1, 10, Think.Chase, Think.None, EnState.Chase3s), // Chase3
            new StateInfo(true, Spr.SPR_GRD_W3_1, 3, Think.None, Think.None, EnState.Chase4), // Chase3s
            new StateInfo(true, Spr.SPR_GRD_W4_1, 8, Think.Chase, Think.None, EnState.Chase1), // Chase4
            new StateInfo(false, Spr.SPR_GRD_DIE_1, 15, Think.None, Think.DeathScream, EnState.Die2), // Die1
            new StateInfo(false, Spr.SPR_GRD_DIE_2, 15, Think.None, Think.None, EnState.Die3), // Die2
            new StateInfo(false, Spr.SPR_GRD_DIE_3, 15, Think.None, Think.None, EnState.Dead), // Die3
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die9
            new StateInfo(false, Spr.SPR_GRD_DEAD, 0, Think.None, Think.None, EnState.Dead), // Dead
        };

        table[(int)EnemyKind.Officer] = new StateInfo[]
        {
            new StateInfo(true, Spr.SPR_OFC_S_1, 0, Think.Stand, Think.None, EnState.Stand), // Stand
            new StateInfo(true, Spr.SPR_OFC_W1_1, 20, Think.Path, Think.None, EnState.Path1s), // Path1
            new StateInfo(true, Spr.SPR_OFC_W1_1, 5, Think.None, Think.None, EnState.Path2), // Path1s
            new StateInfo(true, Spr.SPR_OFC_W2_1, 15, Think.Path, Think.None, EnState.Path3), // Path2
            new StateInfo(true, Spr.SPR_OFC_W3_1, 20, Think.Path, Think.None, EnState.Path3s), // Path3
            new StateInfo(true, Spr.SPR_OFC_W3_1, 5, Think.None, Think.None, EnState.Path4), // Path3s
            new StateInfo(true, Spr.SPR_OFC_W4_1, 15, Think.Path, Think.None, EnState.Path1), // Path4
            new StateInfo(false, Spr.SPR_OFC_PAIN_1, 10, Think.None, Think.None, EnState.Chase1), // Pain
            new StateInfo(false, Spr.SPR_OFC_PAIN_2, 10, Think.None, Think.None, EnState.Chase1), // Pain1
            new StateInfo(false, Spr.SPR_OFC_SHOOT1, 6, Think.None, Think.None, EnState.Shoot2), // Shoot1
            new StateInfo(false, Spr.SPR_OFC_SHOOT2, 20, Think.None, Think.Shoot, EnState.Shoot3), // Shoot2
            new StateInfo(false, Spr.SPR_OFC_SHOOT3, 10, Think.None, Think.None, EnState.Chase1), // Shoot3
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot9
            new StateInfo(true, Spr.SPR_OFC_W1_1, 10, Think.Chase, Think.None, EnState.Chase1s), // Chase1
            new StateInfo(true, Spr.SPR_OFC_W1_1, 3, Think.None, Think.None, EnState.Chase2), // Chase1s
            new StateInfo(true, Spr.SPR_OFC_W2_1, 8, Think.Chase, Think.None, EnState.Chase3), // Chase2
            new StateInfo(true, Spr.SPR_OFC_W3_1, 10, Think.Chase, Think.None, EnState.Chase3s), // Chase3
            new StateInfo(true, Spr.SPR_OFC_W3_1, 3, Think.None, Think.None, EnState.Chase4), // Chase3s
            new StateInfo(true, Spr.SPR_OFC_W4_1, 8, Think.Chase, Think.None, EnState.Chase1), // Chase4
            new StateInfo(false, Spr.SPR_OFC_DIE_1, 11, Think.None, Think.DeathScream, EnState.Die2), // Die1
            new StateInfo(false, Spr.SPR_OFC_DIE_2, 11, Think.None, Think.None, EnState.Die3), // Die2
            new StateInfo(false, Spr.SPR_OFC_DIE_3, 11, Think.None, Think.None, EnState.Dead), // Die3
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die9
            new StateInfo(false, Spr.SPR_OFC_DEAD, 0, Think.None, Think.None, EnState.Dead), // Dead
        };

        table[(int)EnemyKind.SS] = new StateInfo[]
        {
            new StateInfo(true, Spr.SPR_SS_S_1, 0, Think.Stand, Think.None, EnState.Stand), // Stand
            new StateInfo(true, Spr.SPR_SS_W1_1, 20, Think.Path, Think.None, EnState.Path1s), // Path1
            new StateInfo(true, Spr.SPR_SS_W1_1, 5, Think.None, Think.None, EnState.Path2), // Path1s
            new StateInfo(true, Spr.SPR_SS_W2_1, 15, Think.Path, Think.None, EnState.Path3), // Path2
            new StateInfo(true, Spr.SPR_SS_W3_1, 20, Think.Path, Think.None, EnState.Path3s), // Path3
            new StateInfo(true, Spr.SPR_SS_W3_1, 5, Think.None, Think.None, EnState.Path4), // Path3s
            new StateInfo(true, Spr.SPR_SS_W4_1, 15, Think.Path, Think.None, EnState.Path1), // Path4
            new StateInfo(false, Spr.SPR_SS_PAIN_1, 10, Think.None, Think.None, EnState.Chase1), // Pain
            new StateInfo(false, Spr.SPR_SS_PAIN_2, 10, Think.None, Think.None, EnState.Chase1), // Pain1
            new StateInfo(false, Spr.SPR_SS_SHOOT1, 20, Think.None, Think.None, EnState.Shoot2), // Shoot1
            new StateInfo(false, Spr.SPR_SS_SHOOT2, 20, Think.None, Think.Shoot, EnState.Shoot3), // Shoot2
            new StateInfo(false, Spr.SPR_SS_SHOOT3, 10, Think.None, Think.None, EnState.Shoot4), // Shoot3
            new StateInfo(false, Spr.SPR_SS_SHOOT2, 10, Think.None, Think.Shoot, EnState.Shoot5), // Shoot4
            new StateInfo(false, Spr.SPR_SS_SHOOT3, 10, Think.None, Think.None, EnState.Shoot6), // Shoot5
            new StateInfo(false, Spr.SPR_SS_SHOOT2, 10, Think.None, Think.Shoot, EnState.Shoot7), // Shoot6
            new StateInfo(false, Spr.SPR_SS_SHOOT3, 10, Think.None, Think.None, EnState.Shoot8), // Shoot7
            new StateInfo(false, Spr.SPR_SS_SHOOT2, 10, Think.None, Think.Shoot, EnState.Shoot9), // Shoot8
            new StateInfo(false, Spr.SPR_SS_SHOOT3, 10, Think.None, Think.None, EnState.Chase1), // Shoot9
            new StateInfo(true, Spr.SPR_SS_W1_1, 10, Think.Chase, Think.None, EnState.Chase1s), // Chase1
            new StateInfo(true, Spr.SPR_SS_W1_1, 3, Think.None, Think.None, EnState.Chase2), // Chase1s
            new StateInfo(true, Spr.SPR_SS_W2_1, 8, Think.Chase, Think.None, EnState.Chase3), // Chase2
            new StateInfo(true, Spr.SPR_SS_W3_1, 10, Think.Chase, Think.None, EnState.Chase3s), // Chase3
            new StateInfo(true, Spr.SPR_SS_W3_1, 3, Think.None, Think.None, EnState.Chase4), // Chase3s
            new StateInfo(true, Spr.SPR_SS_W4_1, 8, Think.Chase, Think.None, EnState.Chase1), // Chase4
            new StateInfo(false, Spr.SPR_SS_DIE_1, 15, Think.None, Think.DeathScream, EnState.Die2), // Die1
            new StateInfo(false, Spr.SPR_SS_DIE_2, 15, Think.None, Think.None, EnState.Die3), // Die2
            new StateInfo(false, Spr.SPR_SS_DIE_3, 15, Think.None, Think.None, EnState.Dead), // Die3
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die9
            new StateInfo(false, Spr.SPR_SS_DEAD, 0, Think.None, Think.None, EnState.Dead), // Dead
        };

        table[(int)EnemyKind.Dog] = new StateInfo[]
        {
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Stand), // Stand
            new StateInfo(true, Spr.SPR_DOG_W1_1, 20, Think.Path, Think.None, EnState.Path1s), // Path1
            new StateInfo(true, Spr.SPR_DOG_W1_1, 5, Think.None, Think.None, EnState.Path2), // Path1s
            new StateInfo(true, Spr.SPR_DOG_W2_1, 15, Think.Path, Think.None, EnState.Path3), // Path2
            new StateInfo(true, Spr.SPR_DOG_W3_1, 20, Think.Path, Think.None, EnState.Path3s), // Path3
            new StateInfo(true, Spr.SPR_DOG_W3_1, 5, Think.None, Think.None, EnState.Path4), // Path3s
            new StateInfo(true, Spr.SPR_DOG_W4_1, 15, Think.Path, Think.None, EnState.Path1), // Path4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Pain
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Pain1
            new StateInfo(false, Spr.SPR_DOG_JUMP1, 10, Think.None, Think.None, EnState.Shoot2), // Shoot1
            new StateInfo(false, Spr.SPR_DOG_JUMP2, 10, Think.None, Think.Bite, EnState.Shoot3), // Shoot2
            new StateInfo(false, Spr.SPR_DOG_JUMP3, 10, Think.None, Think.None, EnState.Shoot4), // Shoot3
            new StateInfo(false, Spr.SPR_DOG_JUMP1, 10, Think.None, Think.None, EnState.Shoot5), // Shoot4
            new StateInfo(false, Spr.SPR_DOG_W1_1, 10, Think.None, Think.None, EnState.Chase1), // Shoot5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot9
            new StateInfo(true, Spr.SPR_DOG_W1_1, 10, Think.DogChase, Think.None, EnState.Chase1s), // Chase1
            new StateInfo(true, Spr.SPR_DOG_W1_1, 3, Think.None, Think.None, EnState.Chase2), // Chase1s
            new StateInfo(true, Spr.SPR_DOG_W2_1, 8, Think.DogChase, Think.None, EnState.Chase3), // Chase2
            new StateInfo(true, Spr.SPR_DOG_W3_1, 10, Think.DogChase, Think.None, EnState.Chase3s), // Chase3
            new StateInfo(true, Spr.SPR_DOG_W3_1, 3, Think.None, Think.None, EnState.Chase4), // Chase3s
            new StateInfo(true, Spr.SPR_DOG_W4_1, 8, Think.DogChase, Think.None, EnState.Chase1), // Chase4
            new StateInfo(false, Spr.SPR_DOG_DIE_1, 15, Think.None, Think.DeathScream, EnState.Die2), // Die1
            new StateInfo(false, Spr.SPR_DOG_DIE_2, 15, Think.None, Think.None, EnState.Die3), // Die2
            new StateInfo(false, Spr.SPR_DOG_DIE_3, 15, Think.None, Think.None, EnState.Dead), // Die3
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die9
            new StateInfo(false, Spr.SPR_DOG_DEAD, 0, Think.None, Think.None, EnState.Dead), // Dead
        };

        table[(int)EnemyKind.Boss] = new StateInfo[]
        {
            new StateInfo(false, Spr.SPR_BOSS_W1, 0, Think.Stand, Think.None, EnState.Stand), // Stand
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Path1s), // Path1
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Path2), // Path1s
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Path3), // Path2
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Path3s), // Path3
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Path4), // Path3s
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Path1), // Path4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Pain
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Pain1
            new StateInfo(false, Spr.SPR_BOSS_SHOOT1, 30, Think.None, Think.None, EnState.Shoot2), // Shoot1
            new StateInfo(false, Spr.SPR_BOSS_SHOOT2, 10, Think.None, Think.Shoot, EnState.Shoot3), // Shoot2
            new StateInfo(false, Spr.SPR_BOSS_SHOOT3, 10, Think.None, Think.Shoot, EnState.Shoot4), // Shoot3
            new StateInfo(false, Spr.SPR_BOSS_SHOOT2, 10, Think.None, Think.Shoot, EnState.Shoot5), // Shoot4
            new StateInfo(false, Spr.SPR_BOSS_SHOOT3, 10, Think.None, Think.Shoot, EnState.Shoot6), // Shoot5
            new StateInfo(false, Spr.SPR_BOSS_SHOOT2, 10, Think.None, Think.Shoot, EnState.Shoot7), // Shoot6
            new StateInfo(false, Spr.SPR_BOSS_SHOOT3, 10, Think.None, Think.Shoot, EnState.Shoot8), // Shoot7
            new StateInfo(false, Spr.SPR_BOSS_SHOOT1, 10, Think.None, Think.None, EnState.Chase1), // Shoot8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot9
            new StateInfo(false, Spr.SPR_BOSS_W1, 10, Think.Chase, Think.None, EnState.Chase1s), // Chase1
            new StateInfo(false, Spr.SPR_BOSS_W1, 3, Think.None, Think.None, EnState.Chase2), // Chase1s
            new StateInfo(false, Spr.SPR_BOSS_W2, 8, Think.Chase, Think.None, EnState.Chase3), // Chase2
            new StateInfo(false, Spr.SPR_BOSS_W3, 10, Think.Chase, Think.None, EnState.Chase3s), // Chase3
            new StateInfo(false, Spr.SPR_BOSS_W3, 3, Think.None, Think.None, EnState.Chase4), // Chase3s
            new StateInfo(false, Spr.SPR_BOSS_W4, 8, Think.Chase, Think.None, EnState.Chase1), // Chase4
            new StateInfo(false, Spr.SPR_BOSS_DIE1, 15, Think.None, Think.DeathScream, EnState.Die2), // Die1
            new StateInfo(false, Spr.SPR_BOSS_DIE2, 15, Think.None, Think.None, EnState.Die3), // Die2
            new StateInfo(false, Spr.SPR_BOSS_DIE3, 15, Think.None, Think.None, EnState.Dead), // Die3
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die9
            new StateInfo(false, Spr.SPR_BOSS_DEAD, 0, Think.None, Think.None, EnState.Dead), // Dead
        };

        table[(int)EnemyKind.Mutant] = new StateInfo[]
        {
            new StateInfo(true, Spr.SPR_MUT_S_1, 0, Think.Stand, Think.None, EnState.Stand), // Stand
            new StateInfo(true, Spr.SPR_MUT_W1_1, 20, Think.Path, Think.None, EnState.Path1s), // Path1
            new StateInfo(true, Spr.SPR_MUT_W1_1, 5, Think.None, Think.None, EnState.Path2), // Path1s
            new StateInfo(true, Spr.SPR_MUT_W2_1, 15, Think.Path, Think.None, EnState.Path3), // Path2
            new StateInfo(true, Spr.SPR_MUT_W3_1, 20, Think.Path, Think.None, EnState.Path3s), // Path3
            new StateInfo(true, Spr.SPR_MUT_W3_1, 5, Think.None, Think.None, EnState.Path4), // Path3s
            new StateInfo(true, Spr.SPR_MUT_W4_1, 15, Think.Path, Think.None, EnState.Path1), // Path4
            new StateInfo(false, Spr.SPR_MUT_PAIN_1, 10, Think.None, Think.None, EnState.Chase1), // Pain
            new StateInfo(false, Spr.SPR_MUT_PAIN_2, 10, Think.None, Think.None, EnState.Chase1), // Pain1
            new StateInfo(false, Spr.SPR_MUT_SHOOT1, 6, Think.None, Think.Shoot, EnState.Shoot2), // Shoot1
            new StateInfo(false, Spr.SPR_MUT_SHOOT2, 20, Think.None, Think.None, EnState.Shoot3), // Shoot2
            new StateInfo(false, Spr.SPR_MUT_SHOOT3, 10, Think.None, Think.Shoot, EnState.Shoot4), // Shoot3
            new StateInfo(false, Spr.SPR_MUT_SHOOT4, 20, Think.None, Think.None, EnState.Chase1), // Shoot4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Chase1), // Shoot9
            new StateInfo(true, Spr.SPR_MUT_W1_1, 10, Think.Chase, Think.None, EnState.Chase1s), // Chase1
            new StateInfo(true, Spr.SPR_MUT_W1_1, 3, Think.None, Think.None, EnState.Chase2), // Chase1s
            new StateInfo(true, Spr.SPR_MUT_W2_1, 8, Think.Chase, Think.None, EnState.Chase3), // Chase2
            new StateInfo(true, Spr.SPR_MUT_W3_1, 10, Think.Chase, Think.None, EnState.Chase3s), // Chase3
            new StateInfo(true, Spr.SPR_MUT_W3_1, 3, Think.None, Think.None, EnState.Chase4), // Chase3s
            new StateInfo(true, Spr.SPR_MUT_W4_1, 8, Think.Chase, Think.None, EnState.Chase1), // Chase4
            new StateInfo(false, Spr.SPR_MUT_DIE_1, 7, Think.None, Think.DeathScream, EnState.Die2), // Die1
            new StateInfo(false, Spr.SPR_MUT_DIE_2, 7, Think.None, Think.None, EnState.Die3), // Die2
            new StateInfo(false, Spr.SPR_MUT_DIE_3, 7, Think.None, Think.None, EnState.Die4), // Die3
            new StateInfo(false, Spr.SPR_MUT_DIE_4, 7, Think.None, Think.None, EnState.Dead), // Die4
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die5
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die6
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die7
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die8
            new StateInfo(false, Spr.SPR_DEMO, 0, Think.None, Think.None, EnState.Dead), // Die9
            new StateInfo(false, Spr.SPR_MUT_DEAD, 0, Think.None, Think.None, EnState.Dead), // Dead
        };

        return table;
    }
}
