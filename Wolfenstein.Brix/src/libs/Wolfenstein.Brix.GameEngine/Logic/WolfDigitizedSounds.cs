//
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// The shareware VSWAP digitized-sound numbering (the "upload version"
// DigiMap): the indices below are the ones actually present in the
// shareware VSWAP.WL1 (verified against the file's sound table).
// Sounds the shareware lacks are marked None and fall back to their
// AdLib versions once Phase 6 lands.
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

/// <summary>The shareware VSWAP digitized-sound numbers the game logic requests.</summary>
public static class WolfDigitizedSounds
{
    /// <summary>No digitized version exists in the shareware set.</summary>
    public const int None = -1;

    /// <summary>Guard sighting shout ("Halt!").</summary>
    public const int GuardAlert = 0;

    /// <summary>Dog bark (sighting and bite).</summary>
    public const int DogBark = 1;

    /// <summary>Door closing.</summary>
    public const int DoorClose = 2;

    /// <summary>Door opening.</summary>
    public const int DoorOpen = 3;

    /// <summary>The player's machine-gun burst.</summary>
    public const int MachineGunFire = 4;

    /// <summary>The player's pistol shot.</summary>
    public const int PistolFire = 5;

    /// <summary>The player's chain-gun burst.</summary>
    public const int ChainGunFire = 6;

    /// <summary>SS sighting shout ("Schutzstaffel!").</summary>
    public const int SSAlert = 7;

    /// <summary>Hans Groesse sighting ("Guten Tag!").</summary>
    public const int HansAlert = 8;

    /// <summary>Hans Groesse death ("Mutti!").</summary>
    public const int HansDeath = 9;

    /// <summary>Boss chain-gun fire.</summary>
    public const int BossFire = 10;

    /// <summary>SS machine-gun fire.</summary>
    public const int SSFire = 11;

    /// <summary>
    /// Guard death screams: the shareware set rolls among its two
    /// digitized screams (the original maps its three scream slots to
    /// these indices).
    /// </summary>
    public static readonly int[] GuardDeathScreams = { 12, 13, 13 };

    /// <summary>The player takes damage.</summary>
    public const int TakeDamage = 14;

    /// <summary>Secret pushwall rumble.</summary>
    public const int PushWallActivate = 15;

    /// <summary>"Mein Leben!" (SS death, and the player's death).</summary>
    public const int MeinLeben = 20;

    /// <summary>Guard rifle fire.</summary>
    public const int GuardFire = 21;

    /// <summary>The gibs-pickup slurp.</summary>
    public const int Slurpie = 22;

    /// <summary>The extra-life "yeah!".</summary>
    public const int Yeah = 32;

    /// <summary>Officer sighting ("Spion!"): AdLib-only in shareware.</summary>
    public const int OfficerAlert = None;

    /// <summary>Officer death: AdLib-only in shareware.</summary>
    public const int OfficerDeath = None;

    /// <summary>Dog death yelp: AdLib-only in shareware.</summary>
    public const int DogDeath = None;

    /// <summary>SS death: "Mein Leben!".</summary>
    public const int SSDeath = MeinLeben;

    /// <summary>The player death scream.</summary>
    public const int PlayerDeath = MeinLeben;

    // -----------------------------------------------------------------
    // AdLib effect numbers (AUDIOT sound indices, the shareware
    // 69-sound enumeration).
    // -----------------------------------------------------------------

    /// <summary>The knife swing (AdLib effect 23).</summary>
    public const int AdlibKnife = 23;

    /// <summary>The elevator switch (AdLib effect 40).</summary>
    public const int AdlibElevator = 40;

    /// <summary>Key pickup jingle (AdLib effect 12).</summary>
    public const int AdlibKeyPickup = 12;

    /// <summary>Health pickup (AdLib effect 33).</summary>
    public const int AdlibHealth = 33;

    /// <summary>First-aid pickup (AdLib effect 34).</summary>
    public const int AdlibFirstAid = 34;

    /// <summary>Treasure cross pickup (AdLib effect 35).</summary>
    public const int AdlibCross = 35;

    /// <summary>Treasure chalice pickup (AdLib effect 36).</summary>
    public const int AdlibChalice = 36;

    /// <summary>Treasure chest pickup (AdLib effect 37).</summary>
    public const int AdlibChest = 37;

    /// <summary>Treasure crown pickup (AdLib effect 45).</summary>
    public const int AdlibCrown = 45;

    /// <summary>Ammo pickup (AdLib effect 31).</summary>
    public const int AdlibAmmoPickup = 31;

    /// <summary>Machine-gun pickup (AdLib effect 30).</summary>
    public const int AdlibMachineGunPickup = 30;

    /// <summary>Chain-gun pickup (AdLib effect 38).</summary>
    public const int AdlibChainGunPickup = 38;

    /// <summary>Officer sighting "Spion!" (AdLib effect 66).</summary>
    public const int AdlibOfficerAlert = 66;

    /// <summary>Officer death "Nein, so was!" (AdLib effect 67).</summary>
    public const int AdlibOfficerDeath = 67;

    /// <summary>Dog death yelp (AdLib effect 10).</summary>
    public const int AdlibDogDeath = 10;

    /// <summary>Bumping an unusable wall (AdLib effect 6).</summary>
    public const int AdlibNoWay = 6;
}
