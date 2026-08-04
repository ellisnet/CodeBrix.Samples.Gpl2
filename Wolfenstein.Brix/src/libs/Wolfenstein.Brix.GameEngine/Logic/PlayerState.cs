//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), files
// wolf3d/code/wolf/wolf_player.h (player_t, items, weapons) and
// wolf_player.c (the attackinfo table).
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

/// <summary>The player's inventory flags (keys and weapons).</summary>
[Flags]
public enum PlayerItems
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>The gold key.</summary>
    Key1 = 1,

    /// <summary>The silver key.</summary>
    Key2 = 2,

    /// <summary>Unused key slot.</summary>
    Key3 = 4,

    /// <summary>Unused key slot.</summary>
    Key4 = 8,

    /// <summary>The knife.</summary>
    Weapon1 = 16,

    /// <summary>The pistol.</summary>
    Weapon2 = 32,

    /// <summary>The machine gun.</summary>
    Weapon3 = 64,

    /// <summary>The chain gun.</summary>
    Weapon4 = 128,
}

/// <summary>The weapons, in slot order.</summary>
public enum Weapon
{
    /// <summary>The knife.</summary>
    Knife,

    /// <summary>The pistol.</summary>
    Pistol,

    /// <summary>The machine gun.</summary>
    MachineGun,

    /// <summary>The chain gun.</summary>
    ChainGun,
}

/// <summary>The play states (the original state_t).</summary>
public enum PlayState
{
    /// <summary>Not in a game.</summary>
    NotInGame,

    /// <summary>Playing a level.</summary>
    Playing,

    /// <summary>The player died.</summary>
    Dead,

    /// <summary>The level was exited by the secret elevator.</summary>
    SecretLevel,

    /// <summary>The episode's victory walk was triggered.</summary>
    Victory,

    /// <summary>The level was completed.</summary>
    Complete,
}

/// <summary>The player's per-tic input (the original usercmd_t subset).</summary>
public struct PlayerTicCommand
{
    /// <summary>Forward/backward movement in fixed-point units per tic (positive = forward).</summary>
    public int ForwardMove;

    /// <summary>Strafe movement in fixed-point units per tic (positive = right).</summary>
    public int SideMove;

    /// <summary>The turn applied this tic in FINE angle units (positive = counter-clockwise).</summary>
    public int AngleTurn;

    /// <summary>The attack button.</summary>
    public bool Attack;

    /// <summary>The use button.</summary>
    public bool Use;

    /// <summary>Weapon slot to switch to (1-4), or 0 for no change.</summary>
    public int WeaponSlot;
}

/// <summary>One frame of a weapon's attack sequence.</summary>
public readonly struct AttackFrame
{
    /// <summary>Creates an attack-sequence frame.</summary>
    public AttackFrame(int tics, int attack, int frame)
    {
        Tics = tics;
        Attack = attack;
        Frame = frame;
    }

    /// <summary>Tics this frame lasts.</summary>
    public int Tics { get; }

    /// <summary>The frame's action: -1 end, 0 none, 1 fire, 2 knife, 3 auto-loop, 4 chain-loop.</summary>
    public int Attack { get; }

    /// <summary>The weapon sprite frame shown (0-4 within the weapon's block).</summary>
    public int Frame { get; }
}

/// <summary>The player: position, stats, weapon state.</summary>
public sealed class PlayerState
{
    /// <summary>The player's collision half-size in fixed-point units.</summary>
    public const int PlayerSize = WolfMath.MinDist;

    /// <summary>Combined move speed at/above which enemies find the player harder to hit.</summary>
    public const int RunSpeed = 6000;

    /// <summary>Points per extra life.</summary>
    public const int ExtraPoints = 40000;

    /// <summary>The attack sequences per weapon (the original attackinfo table).</summary>
    public static readonly AttackFrame[][] AttackInfo =
    {
        new[] { new AttackFrame(6, 0, 1), new AttackFrame(6, 2, 2), new AttackFrame(6, 0, 3), new AttackFrame(6, -1, 0) },
        new[] { new AttackFrame(6, 0, 1), new AttackFrame(6, 1, 2), new AttackFrame(6, 0, 3), new AttackFrame(6, -1, 0) },
        new[] { new AttackFrame(6, 0, 1), new AttackFrame(6, 1, 2), new AttackFrame(6, 3, 3), new AttackFrame(6, -1, 0) },
        new[] { new AttackFrame(6, 0, 1), new AttackFrame(6, 1, 2), new AttackFrame(6, 4, 3), new AttackFrame(6, -1, 0) },
    };

    /// <summary>The current tic's input.</summary>
    public PlayerTicCommand Cmd;

    /// <summary>The fixed-point x position.</summary>
    public int X { get; set; }

    /// <summary>The fixed-point y position (y-up).</summary>
    public int Y { get; set; }

    /// <summary>The facing angle in FINE units.</summary>
    public int Angle { get; set; }

    /// <summary>Accumulated x movement this tic.</summary>
    public int MoveX { get; set; }

    /// <summary>Accumulated y movement this tic.</summary>
    public int MoveY { get; set; }

    /// <summary>The combined movement magnitude (enemy hit-chance check).</summary>
    public int Speed { get; set; }

    /// <summary>The tile x the player occupies.</summary>
    public int TileX { get; set; }

    /// <summary>The tile y the player occupies.</summary>
    public int TileY { get; set; }

    /// <summary>Hit points.</summary>
    public int Health { get; set; }

    /// <summary>Lives remaining.</summary>
    public int Lives { get; set; }

    /// <summary>Bullets carried.</summary>
    public int Ammo { get; set; }

    /// <summary>The score at the start of the level (restored on death).</summary>
    public int OldScore { get; set; }

    /// <summary>The score.</summary>
    public int Score { get; set; }

    /// <summary>The next score threshold that grants an extra life.</summary>
    public int NextExtra { get; set; }

    /// <summary>Keys and weapons held.</summary>
    public PlayerItems Items { get; set; }

    /// <summary>The weapon in hand.</summary>
    public Weapon CurrentWeapon { get; set; }

    /// <summary>The weapon to switch to when the attack ends.</summary>
    public Weapon PendingWeapon { get; set; }

    /// <summary>The current attack-sequence frame index.</summary>
    public int AttackFrame { get; set; }

    /// <summary>Tics remaining in the current attack frame.</summary>
    public int AttackCount { get; set; }

    /// <summary>The weapon sprite frame to draw (0 = ready).</summary>
    public int WeaponFrame { get; set; }

    /// <summary>True while the attack button is being processed.</summary>
    public bool Attacking { get; set; }

    /// <summary>True while use is held (one action per press).</summary>
    public bool UseHeld { get; set; }

    /// <summary>The area the player is in.</summary>
    public int AreaNumber { get; set; }

    /// <summary>True when the player fired this tic (enemies hear it).</summary>
    public bool MadeNoise { get; set; }

    /// <summary>The current play state.</summary>
    public PlayState State { get; set; } = PlayState.NotInGame;

    /// <summary>Sets up a brand-new game (the original PL_NewGame).</summary>
    public void NewGame()
    {
        Health = 100;
        Ammo = 8;
        Lives = 3;
        Score = 0;
        OldScore = 0;
        CurrentWeapon = PendingWeapon = Weapon.Pistol;
        Items = PlayerItems.Weapon1 | PlayerItems.Weapon2;
        NextExtra = ExtraPoints;
        Attacking = false;
        AttackFrame = AttackCount = WeaponFrame = 0;
        State = PlayState.Playing;
    }

    /// <summary>Carries stats into the next level (the original PL_NextLevel).</summary>
    public void NextLevel()
    {
        OldScore = Score;
        AttackCount = AttackFrame = WeaponFrame = 0;
        Attacking = false;
        UseHeld = false;
        Items &= ~(PlayerItems.Key1 | PlayerItems.Key2 | PlayerItems.Key3 | PlayerItems.Key4);
    }

    /// <summary>Respawns after death; false when no lives remain (the original PL_Reborn).</summary>
    public bool Reborn()
    {
        if (--Lives < 1)
        {
            return false;
        }

        Health = 100;
        Ammo = 8;
        Score = OldScore;
        AttackCount = AttackFrame = WeaponFrame = 0;
        Attacking = false;
        UseHeld = false;
        CurrentWeapon = PendingWeapon = Weapon.Pistol;
        Items = PlayerItems.Weapon1 | PlayerItems.Weapon2;
        State = PlayState.Playing;
        return true;
    }
}
