//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), files
// wolf3d/code/wolf/wolf_player.c (movement, use, the attack pump,
// damage and the give functions), wolf_weapon.c (fire_hit/fire_lead)
// and wolf_powerups.c (spawn/give/pick up). iOS-only changes are
// reverted to the DOS originals: score and extra lives are back,
// starting/reborn ammo is 8, doors block until fully open, and
// closing a door with 'use' works.
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

/// <summary>The player systems: movement, use, weapons, damage and pickups.</summary>
public sealed class PlayerLogic
{
    private const int MaxMove = (WolfMath.MinDist * 2) - 1;

    // The powerup sprite for each kind (the original Pow_Texture).
    private static readonly int[] PowTexture =
    {
        Spr.SPR_STAT_34, // Gibs
        Spr.SPR_STAT_38, // Gibs2
        Spr.SPR_STAT_6,  // Alpo
        Spr.SPR_STAT_25, // FirstAid
        Spr.SPR_STAT_20, // Key1
        Spr.SPR_STAT_21, // Key2
        Spr.SPR_STAT_20, // Key3 (unused)
        Spr.SPR_STAT_20, // Key4 (unused)
        Spr.SPR_STAT_29, // Cross
        Spr.SPR_STAT_30, // Chalice
        Spr.SPR_STAT_31, // Bible
        Spr.SPR_STAT_32, // Crown
        Spr.SPR_STAT_26, // Clip
        Spr.SPR_STAT_26, // Clip2
        Spr.SPR_STAT_27, // MachineGun
        Spr.SPR_STAT_28, // ChainGun
        Spr.SPR_STAT_24, // Food
        Spr.SPR_STAT_33, // FullHeal
    };

    private readonly WolfLogic logic;

    /// <summary>Creates the player logic bound to its game context.</summary>
    public PlayerLogic(WolfLogic logic)
    {
        this.logic = logic;
    }

    private PlayerState Player => logic.Player;

    private LevelState Level => logic.Level;

    private WolfRandom Rng => logic.Rng;

    /// <summary>Returns the sprite texture of a powerup kind.</summary>
    public static int PowerupTexture(PowerupKind kind) => PowTexture[(int)kind];

    /// <summary>Places the player at the level's spawn point (PL_Spawn).</summary>
    public void Spawn()
    {
        Player.X = Level.SpawnX;
        Player.Y = Level.SpawnY;
        Player.Angle = Level.SpawnAngle;
        Player.TileX = WolfMath.Pos2Tile(Player.X);
        Player.TileY = WolfMath.Pos2Tile(Player.Y);
        Player.AreaNumber = Level.Areas[Player.TileX, Player.TileY];
        if (Player.AreaNumber < 0)
        {
            Player.AreaNumber = 36;
        }

        logic.DoorLogic.InitAreas(Player.AreaNumber);
        logic.DoorLogic.ConnectAreas(Player.AreaNumber);
    }

    /// <summary>Runs one tic of player processing (PL_Process).</summary>
    public void Process(int tics)
    {
        Player.MadeNoise = false;

        // Turning happens before movement so this tic moves along the
        // new facing (keyboard input; the original applied touch angle
        // directly).
        if (Player.Cmd.AngleTurn != 0)
        {
            Player.Angle = WolfMath.NormalizeAngle(Player.Angle + Player.Cmd.AngleTurn);
        }

        ControlMovement(tics);

        if (Player.Attacking)
        {
            PlayerAttack(Player.Cmd.Attack, tics);
        }
        else
        {
            if (Player.Cmd.Use)
            {
                if (!Player.UseHeld && Use())
                {
                    Player.UseHeld = true;
                }
            }
            else
            {
                Player.UseHeld = false;
            }

            if (Player.Cmd.Attack)
            {
                Player.Attacking = true;
                Player.AttackFrame = 0;
                Player.AttackCount = PlayerState.AttackInfo[(int)Player.CurrentWeapon][0].Tics;
                Player.WeaponFrame = PlayerState.AttackInfo[(int)Player.CurrentWeapon][0].Frame;
            }
            else if (Player.Cmd.WeaponSlot > 0)
            {
                ChangeWeapon(Player.Cmd.WeaponSlot - 1);
            }
        }
    }

    private bool ChangeWeapon(int weapon)
    {
        var itemFlag = (PlayerItems)((int)PlayerItems.Weapon1 << weapon);
        if (Player.Ammo == 0 && (Weapon)weapon != Weapon.Knife)
        {
            return false;
        }

        if ((Player.Items & itemFlag) == 0)
        {
            return false;
        }

        Player.CurrentWeapon = Player.PendingWeapon = (Weapon)weapon;
        Player.AttackFrame = Player.AttackCount = Player.WeaponFrame = 0;
        return true;
    }

    /// <summary>The use action: doors, pushwalls and elevators (PL_Use).</summary>
    private bool Use()
    {
        var dir = WolfMath.Get4dir(WolfMath.Fine2Rad(Player.Angle));
        var x = Player.TileX + WolfMath.Dx4Dir[(int)dir];
        var y = Player.TileY + WolfMath.Dy4Dir[(int)dir];

        if ((Level.TileMap[x, y] & TileFlag.Door) != 0)
        {
            logic.DoorLogic.TryUse(Level.DoorMap[x, y], Player.Items);
            return true;
        }

        if ((Level.TileMap[x, y] & TileFlag.Secret) != 0)
        {
            return logic.PushWallLogic.Push(x, y, dir);
        }

        if ((Level.TileMap[x, y] & TileFlag.Elevator) != 0)
        {
            switch (dir)
            {
                case Dir4.East:
                case Dir4.West:
                    Level.WallTexX[x, y] += 2; // Flip the switch up.
                    break;

                default:
                    return false; // Don't allow pressing elevator rails.
            }

            Player.State = (Level.TileMap[Player.TileX, Player.TileY] & TileFlag.SecretLevel) != 0
                ? PlayState.SecretLevel
                : PlayState.Complete;
            logic.PlayAdlibSound(WolfDigitizedSounds.AdlibElevator);
            return true;
        }

        return false;
    }

    private bool TryMove()
    {
        var xl = WolfMath.Pos2Tile(Player.X - PlayerState.PlayerSize);
        var yl = WolfMath.Pos2Tile(Player.Y - PlayerState.PlayerSize);
        var xh = WolfMath.Pos2Tile(Player.X + PlayerState.PlayerSize);
        var yh = WolfMath.Pos2Tile(Player.Y + PlayerState.PlayerSize);

        for (var y = yl; y <= yh; y++)
        {
            for (var x = xl; x <= xh; x++)
            {
                if ((Level.TileMap[x, y] & TileFlag.SolidTile) != 0)
                {
                    return false;
                }

                if ((Level.TileMap[x, y] & TileFlag.Door) != 0 &&
                    DoorLogic.DoorOpened(Level, x, y) != DoorLogic.DoorFullOpen)
                {
                    // DOS behavior: a door blocks until fully open.
                    return false;
                }
            }
        }

        foreach (var guard in logic.Guards)
        {
            if (guard.State >= EnState.Die1)
            {
                continue;
            }

            var d = Player.X - guard.X;
            if (d < -ActorLogic.MinActorDist || d > ActorLogic.MinActorDist)
            {
                continue;
            }

            d = Player.Y - guard.Y;
            if (d < -ActorLogic.MinActorDist || d > ActorLogic.MinActorDist)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void ClipMove(int xmove, int ymove)
    {
        var baseX = Player.X;
        var baseY = Player.Y;

        Player.X += xmove;
        Player.Y += ymove;
        if (TryMove())
        {
            return;
        }

        if (xmove != 0)
        {
            Player.X = baseX + xmove;
            Player.Y = baseY;
            if (TryMove())
            {
                return;
            }
        }

        if (ymove != 0)
        {
            Player.X = baseX;
            Player.Y = baseY + ymove;
            if (TryMove())
            {
                return;
            }
        }

        Player.X = baseX;
        Player.Y = baseY;
    }

    private void ControlMovement(int tics)
    {
        var angle = Player.Angle;
        Player.MoveX = Player.MoveY = 0;

        if (Player.Cmd.ForwardMove != 0)
        {
            var speed = tics * Player.Cmd.ForwardMove;
            Player.MoveX += (int)(speed * WolfMath.CosFine(angle));
            Player.MoveY += (int)(speed * WolfMath.SinFine(angle));
        }

        if (Player.Cmd.SideMove != 0)
        {
            var speed = tics * Player.Cmd.SideMove;
            Player.MoveX += (int)(speed * WolfMath.SinFine(angle));
            Player.MoveY -= (int)(speed * WolfMath.CosFine(angle));
        }

        if (Player.MoveX == 0 && Player.MoveY == 0)
        {
            Player.Speed = 0;
            return;
        }

        Player.Speed = Player.MoveX + Player.MoveY;

        Player.MoveX = Math.Clamp(Player.MoveX, -MaxMove, MaxMove);
        Player.MoveY = Math.Clamp(Player.MoveY, -MaxMove, MaxMove);

        ClipMove(Player.MoveX, Player.MoveY);
        Player.TileX = WolfMath.Pos2Tile(Player.X);
        Player.TileY = WolfMath.Pos2Tile(Player.Y);

        // Pick up items on any touched tile, not just the midpoint.
        for (var x = -1; x <= 1; x += 2)
        {
            var tileX = WolfMath.Pos2Tile(Player.X + x * PlayerState.PlayerSize);
            for (var y = -1; y <= 1; y += 2)
            {
                var tileY = WolfMath.Pos2Tile(Player.Y + y * PlayerState.PlayerSize);
                PickUp(tileX, tileY);
            }
        }

        // Area change: ambush tiles and doors have negative values.
        if (Level.Areas[Player.TileX, Player.TileY] >= 0 &&
            Level.Areas[Player.TileX, Player.TileY] != Player.AreaNumber)
        {
            Player.AreaNumber = Level.Areas[Player.TileX, Player.TileY];
            logic.DoorLogic.ConnectAreas(Player.AreaNumber);
        }

        if ((Level.TileMap[Player.TileX, Player.TileY] & TileFlag.Exit) != 0)
        {
            Player.State = PlayState.Victory;
        }
    }

    private void PlayerAttack(bool reAttack, int tics)
    {
        Player.AttackCount -= tics;
        while (Player.AttackCount <= 0)
        {
            var cur = PlayerState.AttackInfo[(int)Player.CurrentWeapon][Player.AttackFrame];
            switch (cur.Attack)
            {
                case -1:
                    Player.Attacking = false;
                    if (Player.Ammo == 0)
                    {
                        Player.CurrentWeapon = Weapon.Knife;
                    }
                    else if (Player.CurrentWeapon != Player.PendingWeapon)
                    {
                        Player.CurrentWeapon = Player.PendingWeapon;
                    }

                    Player.AttackFrame = Player.WeaponFrame = 0;
                    return;

                case 4:
                    if (Player.Ammo == 0)
                    {
                        break;
                    }

                    if (reAttack)
                    {
                        Player.AttackFrame -= 2;
                    }

                    goto case 1;

                case 1:
                    if (Player.Ammo == 0)
                    {
                        // Can only happen with the chain gun.
                        Player.AttackFrame++;
                        break;
                    }

                    FireLead();
                    Player.Ammo--;
                    break;

                case 2:
                    FireHit();
                    break;

                case 3:
                    if (Player.Ammo != 0 && reAttack)
                    {
                        Player.AttackFrame -= 2;
                    }

                    break;
            }

            Player.AttackCount += cur.Tics;
            Player.AttackFrame++;
            Player.WeaponFrame =
                PlayerState.AttackInfo[(int)Player.CurrentWeapon][Player.AttackFrame].Frame;
        }
    }

    // ---------------------------------------------------------------
    // Weapons (wolf_weapon.c)
    // ---------------------------------------------------------------

    private Entity FindShotTarget(out int dist)
    {
        Entity closest = null;
        dist = int.MaxValue;
        foreach (var guard in logic.Guards)
        {
            if ((guard.Flags & EntityFlags.Shootable) == 0)
            {
                continue;
            }

            var shotDist = WolfMath.Point2LineDist(
                guard.X - Player.X, guard.Y - Player.Y, Player.Angle);
            if (shotDist > 2 * WolfMath.TileGlobal / 3)
            {
                continue; // Miss.
            }

            var d1 = WolfMath.LineLen2Point(
                guard.X - Player.X, guard.Y - Player.Y, Player.Angle);
            if (d1 < 0 || d1 > dist)
            {
                continue;
            }

            if (!logic.CheckLine(guard.X, guard.Y, Player.X, Player.Y))
            {
                continue; // Obscured.
            }

            dist = d1;
            closest = guard;
        }

        return closest;
    }

    private void FireHit()
    {
        logic.PlayAdlibSound(WolfDigitizedSounds.AdlibKnife);

        var closest = FindShotTarget(out var dist);
        if (closest == null || dist > WolfMath.Tile2Pos(1))
        {
            return; // Missed: knife reach is short.
        }

        logic.ActorLogic.DamageActor(closest, Rng.Next() >> 4);
    }

    private void FireLead()
    {
        switch (Player.CurrentWeapon)
        {
            case Weapon.Pistol:
                logic.PlayDigitizedSound(WolfDigitizedSounds.PistolFire);
                break;
            case Weapon.MachineGun:
                logic.PlayDigitizedSound(WolfDigitizedSounds.MachineGunFire);
                break;
            case Weapon.ChainGun:
                logic.PlayDigitizedSound(WolfDigitizedSounds.ChainGunFire);
                break;
        }

        Player.MadeNoise = true;

        var closest = FindShotTarget(out _);
        if (closest == null)
        {
            return; // Missed entirely.
        }

        var dx = Math.Abs(closest.TileX - Player.TileX);
        var dy = Math.Abs(closest.TileY - Player.TileY);
        var dist = Math.Max(dx, dy);

        int damage;
        if (dist < 2)
        {
            damage = Rng.Next() / 4;
        }
        else if (dist < 4)
        {
            damage = Rng.Next() / 6;
        }
        else
        {
            if (Rng.Next() / 12 < dist)
            {
                return; // Missed.
            }

            damage = Rng.Next() / 6;
        }

        logic.ActorLogic.DamageActor(closest, damage);
    }

    // ---------------------------------------------------------------
    // Damage and gives
    // ---------------------------------------------------------------

    /// <summary>Damages the player (PL_Damage).</summary>
    public void Damage(Entity attacker, int points)
    {
        if (Player.State == PlayState.Dead)
        {
            return;
        }

        if (logic.Difficulty == DifficultyLevel.CanIPlayDaddy)
        {
            points >>= 2;
        }

        Player.Health -= points;
        if (Player.Health <= 0)
        {
            Player.Health = 0;
            Player.State = PlayState.Dead;
            logic.PlayDigitizedSound(WolfDigitizedSounds.PlayerDeath);
        }
        else
        {
            logic.PlayDigitizedSound(WolfDigitizedSounds.TakeDamage);
        }

        logic.NotifyDamage(points, attacker);
    }

    /// <summary>Heals the player up to a limit (PL_GiveHealth).</summary>
    public bool GiveHealth(int points, int max)
    {
        if (max == 0)
        {
            max = 100;
        }

        if (Player.Health >= max)
        {
            return false;
        }

        Player.Health = Math.Min(Player.Health + points, max);
        return true;
    }

    /// <summary>Gives bullets (PL_GiveAmmo).</summary>
    public bool GiveAmmo(int ammo)
    {
        const int maxAmmo = 99;
        if (Player.Ammo >= maxAmmo)
        {
            return false;
        }

        if (Player.Ammo == 0 && Player.AttackFrame == 0)
        {
            // The knife was out because of no ammo.
            Player.CurrentWeapon = Player.PendingWeapon;
        }

        Player.Ammo = Math.Min(Player.Ammo + ammo, maxAmmo);
        return true;
    }

    /// <summary>Gives a weapon plus a little ammo (PL_GiveWeapon).</summary>
    public void GiveWeapon(Weapon weapon)
    {
        GiveAmmo(6);

        var itemFlag = (PlayerItems)((int)PlayerItems.Weapon1 << (int)weapon);
        if ((Player.Items & itemFlag) != 0)
        {
            return;
        }

        Player.Items |= itemFlag;
        if (Player.CurrentWeapon < weapon)
        {
            // Don't switch down from a better weapon.
            Player.CurrentWeapon = Player.PendingWeapon = weapon;
        }
    }

    /// <summary>Adds score, granting extra lives at each threshold (PL_GivePoints).</summary>
    public void GivePoints(int points)
    {
        Player.Score += points;
        while (Player.Score >= Player.NextExtra)
        {
            Player.NextExtra += PlayerState.ExtraPoints;
            Player.Lives++;
        }
    }

    /// <summary>Gives a key (PL_GiveKey).</summary>
    public void GiveKey(int key) => Player.Items |= (PlayerItems)((int)PlayerItems.Key1 << key);

    // ---------------------------------------------------------------
    // Powerups (wolf_powerups.c)
    // ---------------------------------------------------------------

    /// <summary>Places a pickup (Powerup_Spawn).</summary>
    public void SpawnPowerup(int x, int y, PowerupKind kind)
    {
        Level.TileMap[x, y] |= TileFlag.Powerup;
        Level.Powerups.Add(new Powerup(x, y, kind));
    }

    private void PickUp(int x, int y)
    {
        var anyLeft = false;
        for (var i = Level.Powerups.Count - 1; i >= 0; i--)
        {
            var pow = Level.Powerups[i];
            if (pow.TileX != x || pow.TileY != y)
            {
                continue;
            }

            if (Give(pow.Kind))
            {
                Level.Powerups.RemoveAt(i);
            }
            else
            {
                anyLeft = true;
            }
        }

        if (anyLeft)
        {
            Level.TileMap[x, y] |= TileFlag.Powerup;
        }
        else
        {
            Level.TileMap[x, y] &= ~TileFlag.Powerup;
        }
    }

    private bool Give(PowerupKind kind)
    {
        switch (kind)
        {
            case PowerupKind.Key1:
            case PowerupKind.Key2:
            case PowerupKind.Key3:
            case PowerupKind.Key4:
                GiveKey((int)kind - (int)PowerupKind.Key1);
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibKeyPickup);
                break;

            case PowerupKind.Cross:
                GivePoints(100);
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibCross);
                Level.FoundTreasure++;
                break;

            case PowerupKind.Chalice:
                GivePoints(500);
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibChalice);
                Level.FoundTreasure++;
                break;

            case PowerupKind.Bible:
                GivePoints(1000);
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibChest);
                Level.FoundTreasure++;
                break;

            case PowerupKind.Crown:
                GivePoints(5000);
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibCrown);
                Level.FoundTreasure++;
                break;

            case PowerupKind.Gibs:
            case PowerupKind.Gibs2:
                if (!GiveHealth(1, 11))
                {
                    return false;
                }

                logic.PlayDigitizedSound(WolfDigitizedSounds.Slurpie);
                break;

            case PowerupKind.Alpo:
                if (!GiveHealth(4, 0))
                {
                    return false;
                }

                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibHealth);
                break;

            case PowerupKind.Food:
                if (!GiveHealth(10, 0))
                {
                    return false;
                }

                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibHealth);
                break;

            case PowerupKind.FirstAid:
                if (!GiveHealth(25, 0))
                {
                    return false;
                }

                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibFirstAid);
                break;

            case PowerupKind.Clip:
                if (!GiveAmmo(8))
                {
                    return false;
                }

                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibAmmoPickup);
                break;

            case PowerupKind.Clip2:
                if (!GiveAmmo(4))
                {
                    return false;
                }

                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibAmmoPickup);
                break;

            case PowerupKind.MachineGun:
                GiveWeapon(Weapon.MachineGun);
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibMachineGunPickup);
                break;

            case PowerupKind.ChainGun:
                GiveWeapon(Weapon.ChainGun);
                logic.PlayAdlibSound(WolfDigitizedSounds.AdlibChainGunPickup);
                break;

            case PowerupKind.FullHeal:
                // The original extra-life item: full health, ammo and a life.
                GiveHealth(99, 99);
                GiveAmmo(25);
                Player.Lives++;
                Level.FoundTreasure++;
                logic.PlayDigitizedSound(WolfDigitizedSounds.Yeah);
                break;

            default:
                return false;
        }

        return true;
    }
}
