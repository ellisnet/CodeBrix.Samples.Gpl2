//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 2000-2002 by DarkOne the Hacker
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// The per-tic orchestration translates the Wolf3D iOS v2.1 GPL
// source's game loop body (github.com/id-Software/Wolf3D-iOS, commit
// d7fff51d; wolf3d/code/iphone/iphone_wolf.c: PL_Process,
// ProcessGuards, Door_ProcessDoors_e, PushWall_Process per frame) and
// wolf_level.c's Level_CheckLine. Level flow (next level, death,
// restart) follows the original DOS rules: full episode progression,
// lives, and score persistence.
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
using Wolfenstein.Brix.GameEngine.Assets;

namespace Wolfenstein.Brix.GameEngine.Logic;

/// <summary>The game difficulty levels, in menu order.</summary>
public enum DifficultyLevel
{
    /// <summary>"Can I play, Daddy?"</summary>
    CanIPlayDaddy,

    /// <summary>"Don't hurt me."</summary>
    DontHurtMe,

    /// <summary>"Bring 'em on!"</summary>
    BringEmOn,

    /// <summary>"I am Death incarnate!"</summary>
    IAmDeathIncarnate,
}

/// <summary>
/// The complete game simulation: owns the level, player, actors,
/// doors, areas and pushwall, and advances everything one 70 Hz tic
/// at a time. Fully headless; rendering and audio observe it.
/// </summary>
public sealed class WolfLogic
{
    /// <summary>The fixed simulation rate: Wolfenstein 3-D's native 70 Hz.</summary>
    public const int TicsPerSecond = 70;

    private readonly WolfAssets assets;

    /// <summary>Creates the game logic over a loaded asset set.</summary>
    public WolfLogic(WolfAssets assets, int rngSeedIndex = 0)
    {
        this.assets = assets;
        Rng = new WolfRandom(rngSeedIndex);
        Player = new PlayerState();
        DoorLogic = new DoorLogic(this);
        ActorLogic = new ActorLogic(this);
        PlayerLogic = new PlayerLogic(this);
        PushWallLogic = new PushWallLogic(this);
    }

    /// <summary>The chosen difficulty (set by StartNewGame).</summary>
    public DifficultyLevel Difficulty { get; private set; }

    /// <summary>The table-driven random stream.</summary>
    public WolfRandom Rng { get; }

    /// <summary>The player.</summary>
    public PlayerState Player { get; }

    /// <summary>The loaded level (null before the first StartNewGame).</summary>
    public LevelState Level { get; private set; }

    /// <summary>The live actors.</summary>
    public List<Entity> Guards { get; } = new List<Entity>();

    /// <summary>The door and area system.</summary>
    public DoorLogic DoorLogic { get; }

    /// <summary>The actor system.</summary>
    public ActorLogic ActorLogic { get; }

    /// <summary>The player system.</summary>
    public PlayerLogic PlayerLogic { get; }

    /// <summary>The pushwall system.</summary>
    public PushWallLogic PushWallLogic { get; }

    /// <summary>The number of tics run so far in this level.</summary>
    public long TicCount { get; private set; }

    /// <summary>True when the whole episode has been completed.</summary>
    public bool EpisodeComplete { get; private set; }

    /// <summary>True when the player is out of lives.</summary>
    public bool GameOver { get; private set; }

    /// <summary>Raised with a digitized-sound number to play (Phase 5 hooks this).</summary>
    public event Action<int> DigitizedSoundRequested;

    /// <summary>Raised with an AdLib effect number to play (Phase 6 hooks this).</summary>
    public event Action<int> AdlibSoundRequested;

    /// <summary>Raised with user-facing notifications ("You need a gold key").</summary>
    public event Action<string> Notification;

    /// <summary>Raised when the player takes damage (for the red flash and face).</summary>
    public event Action<int> PlayerDamaged;

    /// <summary>Raised when a level is exited, before the next one loads.</summary>
    public event Action LevelCompleted;

    internal void PlayDigitizedSound(int number) => DigitizedSoundRequested?.Invoke(number);

    internal void PlayAdlibSound(int number) => AdlibSoundRequested?.Invoke(number);

    internal void Notify(string message) => Notification?.Invoke(message);

    internal void NotifyDamage(int points, Entity attacker) => PlayerDamaged?.Invoke(points);

    internal void AttachLevel(LevelState level) => Level = level;

    /// <summary>Starts a new game at the first level.</summary>
    public void StartNewGame(DifficultyLevel difficulty = DifficultyLevel.BringEmOn)
    {
        Difficulty = difficulty;
        Player.NewGame();
        EpisodeComplete = false;
        GameOver = false;
        StartLevel(0);
    }

    /// <summary>Loads and enters a level.</summary>
    public void StartLevel(int levelIndex)
    {
        Guards.Clear();
        PushWallLogic.Reset();
        LevelLoader.Load(this, assets.Maps.Maps[levelIndex], levelIndex);
        PlayerLogic.Spawn();
        Player.State = PlayState.Playing;
        TicCount = 0;
    }

    /// <summary>Advances the simulation by one tic.</summary>
    public void Tic(in PlayerTicCommand command)
    {
        if (Player.State == PlayState.NotInGame || EpisodeComplete || GameOver)
        {
            return;
        }

        const int tics = 1;
        Player.Cmd = command;

        if (Player.State == PlayState.Playing)
        {
            PlayerLogic.Process(tics);
        }

        ActorLogic.ProcessGuards(tics);
        DoorLogic.ProcessDoors(tics);
        PushWallLogic.Process(tics);
        Level.TimeTics++;
        TicCount++;

        switch (Player.State)
        {
            case PlayState.Complete:
            case PlayState.SecretLevel:
            case PlayState.Victory:
                // The level is over; the session layer shows the
                // intermission and calls AdvanceToNextLevel.
                LevelCompleted?.Invoke();
                break;

            case PlayState.Dead:
                // Linger briefly so the death is visible, then respawn
                // or end the game.
                if (++deathTics >= 70)
                {
                    deathTics = 0;
                    if (Player.Reborn())
                    {
                        StartLevel(Level.LevelIndex);
                    }
                    else
                    {
                        GameOver = true;
                    }
                }

                break;
        }
    }

    private int deathTics;

    /// <summary>
    /// Level progression, shareware episode 1: maps 1-8 in order,
    /// boss (index 8) after map 8; the secret elevator goes to the
    /// secret level (index 9), which returns to map 2; winning the
    /// boss level completes the episode. Called by the session layer
    /// after its intermission screen.
    /// </summary>
    public void AdvanceToNextLevel()
    {
        var exitState = Player.State;
        Player.NextLevel();

        if (exitState == PlayState.Victory || Level.LevelIndex == 8)
        {
            // The boss level's exit (or the victory trigger) ends the
            // episode.
            EpisodeComplete = true;
            Player.State = PlayState.NotInGame;
            return;
        }

        int next;
        if (exitState == PlayState.SecretLevel)
        {
            next = 9;
        }
        else if (Level.LevelIndex == 9)
        {
            // Returning from the secret level continues at map 2.
            next = 1;
        }
        else if (Level.LevelIndex == 7)
        {
            // Map 8 leads to the boss level.
            next = 8;
        }
        else
        {
            next = Level.LevelIndex + 1;
        }

        StartLevel(next);
    }

    /// <summary>
    /// Traces a sight/shot line between two fixed-point points,
    /// checking walls and door openings (Level_CheckLine).
    /// </summary>
    public bool CheckLine(int x1, int y1, int x2, int y2)
    {
        const int fracBits = 8;

        var xt1 = x1 >> WolfMath.TileShift;
        var yt1 = y1 >> WolfMath.TileShift;
        var xt2 = x2 >> WolfMath.TileShift;
        var yt2 = y2 >> WolfMath.TileShift;

        var xdist = Math.Abs(xt2 - xt1);
        var ydist = Math.Abs(yt2 - yt1);

        x1 >>= fracBits;
        y1 >>= fracBits;
        x2 >>= fracBits;
        y2 >>= fracBits;

        if (xdist != 0)
        {
            int partial, xstep;
            if (xt2 > xt1)
            {
                partial = 256 - (x1 & 0xFF);
                xstep = 1;
            }
            else
            {
                partial = x1 & 0xFF;
                xstep = -1;
            }

            var deltafrac = Math.Abs(x2 - x1);
            var ystep = ((y2 - y1) << fracBits) / deltafrac;
            var frac = y1 + ((ystep * partial) >> fracBits);

            var x = xt1 + xstep;
            var xEnd = xt2 + xstep;
            do
            {
                var y = frac >> fracBits;
                frac += ystep;

                if (x < 0 || x > 63 || y < 0 || y > 63)
                {
                    return false;
                }

                if ((Level.TileMap[x, y] & TileFlag.Wall) != 0)
                {
                    return false;
                }

                if ((Level.TileMap[x, y] & TileFlag.Door) != 0)
                {
                    var door = Level.DoorMap[x, y];
                    if (door.Action != DoorAction.Open)
                    {
                        if (door.Action == DoorAction.Closed)
                        {
                            return false;
                        }

                        var intercept = ((frac - ystep / 2) & 0xFF) >> 4;
                        if (intercept < 63 - door.TicCount)
                        {
                            return false;
                        }
                    }
                }

                x += xstep;
            }
            while (x != xEnd);
        }

        if (ydist != 0)
        {
            int partial, ystep;
            if (yt2 > yt1)
            {
                partial = 256 - (y1 & 0xFF);
                ystep = 1;
            }
            else
            {
                partial = y1 & 0xFF;
                ystep = -1;
            }

            var deltafrac = Math.Abs(y2 - y1);
            var xstep = ((x2 - x1) << fracBits) / deltafrac;
            var frac = x1 + ((xstep * partial) >> fracBits);

            var y = yt1 + ystep;
            var yEnd = yt2 + ystep;
            do
            {
                var x = frac >> fracBits;
                frac += xstep;

                if (x < 0 || x > 63 || y < 0 || y > 63)
                {
                    return false;
                }

                if ((Level.TileMap[x, y] & TileFlag.Wall) != 0)
                {
                    return false;
                }

                if ((Level.TileMap[x, y] & TileFlag.Door) != 0)
                {
                    var door = Level.DoorMap[x, y];
                    if (door.Action != DoorAction.Open)
                    {
                        if (door.Action == DoorAction.Closed)
                        {
                            return false;
                        }

                        var intercept = ((frac - xstep / 2) & 0xFF) >> 4;
                        if (intercept < door.TicCount)
                        {
                            return false;
                        }
                    }
                }

                y += ystep;
            }
            while (y != yEnd);
        }

        return true;
    }
}
