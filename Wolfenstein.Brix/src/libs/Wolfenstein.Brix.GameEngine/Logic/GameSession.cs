//
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// The screen flow (title -> menu -> difficulty -> get psyched ->
// play -> intermission -> victory -> high scores) recreates the
// original Wolfenstein 3-D game shell. The par times and the
// intermission bonus rules (500 points per second under par, 10000
// per perfect kill/secret/treasure ratio) are the original game's
// documented values.
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
using System.Linq;
using Wolfenstein.Brix.GameEngine.Assets;

namespace Wolfenstein.Brix.GameEngine.Logic;

/// <summary>The screens the game shell can show.</summary>
public enum SessionScreen
{
    /// <summary>The title picture.</summary>
    Title,

    /// <summary>The main menu.</summary>
    MainMenu,

    /// <summary>The options (sound volumes and view size) menu.</summary>
    SoundMenu,

    /// <summary>The "are you sure you want to quit?" prompt.</summary>
    QuitConfirm,

    /// <summary>The difficulty ("How tough are you?") menu.</summary>
    DifficultySelect,

    /// <summary>The load-game slot list.</summary>
    LoadMenu,

    /// <summary>The save-game slot list.</summary>
    SaveMenu,

    /// <summary>Typing a save-slot name.</summary>
    SaveNameEntry,

    /// <summary>The "Get Psyched!" interstitial.</summary>
    GetPsyched,

    /// <summary>Gameplay.</summary>
    Playing,

    /// <summary>The end-of-level stats screen.</summary>
    Intermission,

    /// <summary>The episode-complete screen.</summary>
    Victory,

    /// <summary>The high-score table.</summary>
    HighScores,

    /// <summary>Typing a high-score name.</summary>
    HighScoreNameEntry,
}

/// <summary>The per-tic input for the whole shell (game + menu edges).</summary>
public struct SessionInput
{
    /// <summary>The gameplay command for this tic.</summary>
    public PlayerTicCommand Game;

    /// <summary>Menu cursor up (edge).</summary>
    public bool MenuUp;

    /// <summary>Menu cursor down (edge).</summary>
    public bool MenuDown;

    /// <summary>Menu activate / continue (edge; Enter or Space).</summary>
    public bool MenuActivate;

    /// <summary>Menu back / open menu (edge; Escape).</summary>
    public bool MenuBack;

    /// <summary>A typed character for name entry (0 when none).</summary>
    public char TypedChar;

    /// <summary>Backspace (edge) during name entry.</summary>
    public bool Backspace;
}

/// <summary>The stats snapshot the intermission screen shows.</summary>
public sealed class IntermissionStats
{
    /// <summary>The one-based floor number completed.</summary>
    public int Floor { get; init; }

    /// <summary>Level time in whole seconds.</summary>
    public int TimeSeconds { get; init; }

    /// <summary>Par time in whole seconds (0 = no par).</summary>
    public int ParSeconds { get; init; }

    /// <summary>Kill percentage 0-100.</summary>
    public int KillRatio { get; init; }

    /// <summary>Secret percentage 0-100.</summary>
    public int SecretRatio { get; init; }

    /// <summary>Treasure percentage 0-100.</summary>
    public int TreasureRatio { get; init; }

    /// <summary>The bonus awarded.</summary>
    public int Bonus { get; init; }
}

/// <summary>
/// The game shell: owns the <see cref="WolfLogic"/> simulation and the
/// screen flow around it (title, menus, saves, intermissions, victory
/// and high scores). Fully headless; the session renderer draws it.
/// </summary>
public sealed class GameSession
{
    /// <summary>The menu items of the main menu, in order.</summary>
    public static readonly string[] MainMenuItems =
    {
        "New Game",
        "Options",
        "Load Game",
        "Save Game",
        "View Scores",
        "Back To Game",
        "End Game",
        "Quit",
    };

    /// <summary>The difficulty menu items, in order.</summary>
    public static readonly string[] DifficultyItems =
    {
        "Can I play, Daddy?",
        "Don't hurt me.",
        "Bring 'em on!",
        "I am Death incarnate!",
    };

    // Episode 1 par times in seconds, in map order (boss and secret
    // levels have no par).
    private static readonly int[] ParTimes = { 90, 120, 120, 210, 180, 180, 150, 150, 0, 0 };

    private const int HighScoreCount = 7;

    private readonly IWolfStorage storage;
    private bool gameInProgress;
    private int getPsychedTics;
    private int pendingHighScore = -1;

    /// <summary>Creates the shell over a loaded asset set.</summary>
    public GameSession(WolfAssets assets, IWolfStorage storage = null, int rngSeedIndex = 0)
    {
        this.storage = storage ?? new MemoryWolfStorage();
        Logic = new WolfLogic(assets, rngSeedIndex);
        HighScores = LoadOrDefaultHighScores();
        LoadConfig();
    }

    /// <summary>Digitized-effect volume, 0-10.</summary>
    public int SfxVolume { get; private set; } = 10;

    /// <summary>Music and AdLib-effect volume, 0-10.</summary>
    public int MusicVolume { get; private set; } = 7;

    /// <summary>The 3D view-size index (0-2; 2 is the classic default).</summary>
    public int ViewSize { get; private set; } = 2;

    private void LoadConfig()
    {
        foreach (var line in (storage.LoadConfigText() ?? string.Empty).Split('\n'))
        {
            var parts = line.Split('=');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var value))
            {
                continue;
            }

            value = Math.Clamp(value, 0, 10);
            switch (parts[0].Trim())
            {
                case "sfxvolume":
                    SfxVolume = value;
                    break;
                case "musicvolume":
                    MusicVolume = value;
                    break;
                case "viewsize":
                    ViewSize = Math.Clamp(value, 0, 2);
                    break;
            }
        }
    }

    private void SaveConfig() =>
        storage.SaveConfigText(
            $"sfxvolume={SfxVolume}\nmusicvolume={MusicVolume}\nviewsize={ViewSize}\n");

    /// <summary>The game simulation.</summary>
    public WolfLogic Logic { get; }

    /// <summary>The storage seam in use.</summary>
    public IWolfStorage Storage => storage;

    /// <summary>The screen currently showing.</summary>
    public SessionScreen Screen { get; private set; } = SessionScreen.Title;

    /// <summary>The selected index of the current menu.</summary>
    public int MenuIndex { get; private set; }

    /// <summary>The number of tics spent on the current screen.</summary>
    public int ScreenTics { get; private set; }

    /// <summary>The intermission stats being shown (when on that screen).</summary>
    public IntermissionStats Intermission { get; private set; }

    /// <summary>The high-score table, best first.</summary>
    public HighScore[] HighScores { get; private set; }

    /// <summary>The save-slot names (null entries are empty).</summary>
    public string[] SaveSlotNames => storage.GetSaveSlotDescriptions();

    /// <summary>The name being typed (save slot or high score).</summary>
    public string TypedName { get; private set; } = string.Empty;

    /// <summary>Raised when the player chooses Quit.</summary>
    public event Action QuitRequested;

    // The classic soundtrack assignments (AUDIOT music track numbers).
    private static readonly int[] LevelSongs = { 3, 11, 9, 12, 3, 11, 9, 12, 2, 0 };

    /// <summary>
    /// The music track for what is showing: the level songs while
    /// playing, and the classic menu/intermission/scores tracks
    /// elsewhere.
    /// </summary>
    public int MusicTrack => Screen switch
    {
        SessionScreen.Title => 7,                       // NAZI_NOR
        SessionScreen.MainMenu or SessionScreen.DifficultySelect or
        SessionScreen.LoadMenu or SessionScreen.SaveMenu or
        SessionScreen.SaveNameEntry => 14,              // WONDERIN
        SessionScreen.Intermission => 16,               // ENDLEVEL
        SessionScreen.Victory => 24,                    // URAHERO
        SessionScreen.HighScores or SessionScreen.HighScoreNameEntry => 23, // ROSTER
        _ => Logic.Level != null && Logic.Level.LevelIndex < LevelSongs.Length
            ? LevelSongs[Logic.Level.LevelIndex]
            : 3,
    };

    /// <summary>True when a fizzle-fade transition should start this tic.</summary>
    public bool TransitionPending { get; set; }

    /// <summary>
    /// Pauses gameplay by opening the menu (called when the window
    /// loses keyboard focus; game time stops on every non-playing
    /// screen).
    /// </summary>
    public void OnFocusLost()
    {
        if (Screen == SessionScreen.Playing)
        {
            ShowScreen(SessionScreen.MainMenu);
            MenuIndex = Array.IndexOf(MainMenuItems, "Back To Game");
        }
    }

    /// <summary>Advances the shell (and the game when playing) one tic.</summary>
    public void Tic(in SessionInput input)
    {
        ScreenTics++;
        switch (Screen)
        {
            case SessionScreen.Title:
                if (input.MenuActivate || input.MenuBack)
                {
                    ShowScreen(SessionScreen.MainMenu);
                }

                break;

            case SessionScreen.MainMenu:
                TicMainMenu(input);
                break;

            case SessionScreen.SoundMenu:
                TicSoundMenu(input);
                break;

            case SessionScreen.QuitConfirm:
                if (input.MenuActivate)
                {
                    QuitRequested?.Invoke();
                }
                else if (input.MenuBack)
                {
                    ShowScreen(SessionScreen.MainMenu);
                }

                break;

            case SessionScreen.DifficultySelect:
                TicDifficultySelect(input);
                break;

            case SessionScreen.LoadMenu:
                TicSlotMenu(input, loading: true);
                break;

            case SessionScreen.SaveMenu:
                TicSlotMenu(input, loading: false);
                break;

            case SessionScreen.SaveNameEntry:
                TicNameEntry(input, forSave: true);
                break;

            case SessionScreen.HighScoreNameEntry:
                TicNameEntry(input, forSave: false);
                break;

            case SessionScreen.GetPsyched:
                if (++getPsychedTics >= 70)
                {
                    ShowScreen(SessionScreen.Playing);
                }

                break;

            case SessionScreen.Playing:
                TicPlaying(input);
                break;

            case SessionScreen.Intermission:
                if (input.MenuActivate)
                {
                    FinishIntermission();
                }

                break;

            case SessionScreen.Victory:
                if (input.MenuActivate)
                {
                    EnterHighScores(fromGame: true);
                }

                break;

            case SessionScreen.HighScores:
                if (input.MenuActivate || input.MenuBack)
                {
                    ShowScreen(SessionScreen.Title);
                }

                break;
        }
    }

    private void ShowScreen(SessionScreen screen)
    {
        Screen = screen;
        ScreenTics = 0;
        MenuIndex = 0;
        TransitionPending = true;
    }

    private void TicMainMenu(in SessionInput input)
    {
        var enabled = MainMenuItems
            .Select((item, i) => IsMenuItemEnabled(i))
            .ToArray();

        MoveCursor(input, MainMenuItems.Length, enabled);

        if (input.MenuBack && gameInProgress)
        {
            ShowScreen(SessionScreen.Playing);
            return;
        }

        if (!input.MenuActivate)
        {
            return;
        }

        switch (MainMenuItems[MenuIndex])
        {
            case "New Game":
                ShowScreen(SessionScreen.DifficultySelect);
                break;

            case "Options":
                ShowScreen(SessionScreen.SoundMenu);
                break;

            case "Load Game":
                ShowScreen(SessionScreen.LoadMenu);
                break;

            case "Save Game":
                ShowScreen(SessionScreen.SaveMenu);
                break;

            case "View Scores":
                ShowScreen(SessionScreen.HighScores);
                break;

            case "Back To Game":
                ShowScreen(SessionScreen.Playing);
                break;

            case "End Game":
                gameInProgress = false;
                ShowScreen(SessionScreen.Title);
                break;

            case "Quit":
                ShowScreen(SessionScreen.QuitConfirm);
                break;
        }
    }

    private bool IsMenuItemEnabled(int index) => MainMenuItems[index] switch
    {
        "Save Game" => gameInProgress,
        "Back To Game" => gameInProgress,
        "End Game" => gameInProgress,
        _ => true,
    };

    private void MoveCursor(in SessionInput input, int count, bool[] enabled = null)
    {
        if (input.MenuUp)
        {
            do
            {
                MenuIndex = (MenuIndex + count - 1) % count;
            }
            while (enabled != null && !enabled[MenuIndex]);
        }

        if (input.MenuDown)
        {
            do
            {
                MenuIndex = (MenuIndex + 1) % count;
            }
            while (enabled != null && !enabled[MenuIndex]);
        }
    }

    private void TicSoundMenu(in SessionInput input)
    {
        MoveCursor(input, 3);

        if (input.MenuBack)
        {
            ShowScreen(SessionScreen.MainMenu);
            return;
        }

        if (input.MenuActivate)
        {
            // Enter steps the selected setting, wrapping around.
            switch (MenuIndex)
            {
                case 0:
                    SfxVolume = (SfxVolume + 1) % 11;
                    break;
                case 1:
                    MusicVolume = (MusicVolume + 1) % 11;
                    break;
                default:
                    ViewSize = (ViewSize + 1) % 3;
                    break;
            }

            SaveConfig();
        }
    }

    private void TicDifficultySelect(in SessionInput input)
    {
        MoveCursor(input, DifficultyItems.Length);

        if (input.MenuBack)
        {
            ShowScreen(SessionScreen.MainMenu);
            return;
        }

        if (input.MenuActivate)
        {
            Logic.StartNewGame((DifficultyLevel)MenuIndex);
            gameInProgress = true;
            getPsychedTics = 0;
            ShowScreen(SessionScreen.GetPsyched);
        }
    }

    private void TicSlotMenu(in SessionInput input, bool loading)
    {
        MoveCursor(input, IWolfStorage.SaveSlotCount);

        if (input.MenuBack)
        {
            ShowScreen(SessionScreen.MainMenu);
            return;
        }

        if (!input.MenuActivate)
        {
            return;
        }

        if (loading)
        {
            var data = storage.LoadSaveSlot(MenuIndex);
            if (data != null)
            {
                // A save from a different difficulty rebuilds the
                // logic's difficulty from the payload's stored value:
                // restore twice is harmless, so just try each way.
                if (RestoreAtAnyDifficulty(data))
                {
                    gameInProgress = true;
                    ShowScreen(SessionScreen.Playing);
                }
            }
        }
        else
        {
            TypedName = SaveSlotNames[MenuIndex] ?? string.Empty;
            ShowScreen(SessionScreen.SaveNameEntry);
        }
    }

    private bool RestoreAtAnyDifficulty(byte[] data)
    {
        if (SaveGameSerializer.Restore(Logic, data))
        {
            return true;
        }

        foreach (DifficultyLevel difficulty in Enum.GetValues<DifficultyLevel>())
        {
            Logic.StartNewGame(difficulty);
            if (SaveGameSerializer.Restore(Logic, data))
            {
                return true;
            }
        }

        return false;
    }

    private void TicNameEntry(in SessionInput input, bool forSave)
    {
        if (input.TypedChar != 0 && TypedName.Length < 16)
        {
            TypedName += input.TypedChar;
        }

        if (input.Backspace && TypedName.Length > 0)
        {
            TypedName = TypedName.Substring(0, TypedName.Length - 1);
        }

        if (input.MenuBack)
        {
            ShowScreen(forSave ? SessionScreen.SaveMenu : SessionScreen.HighScores);
            return;
        }

        if (!input.MenuActivate)
        {
            return;
        }

        if (forSave)
        {
            var name = TypedName.Length > 0 ? TypedName : $"Save {MenuIndex + 1}";
            storage.SaveSaveSlot(MenuIndex, SaveGameSerializer.Save(Logic), name);
            ShowScreen(SessionScreen.Playing);
        }
        else
        {
            CommitHighScore(TypedName.Length > 0 ? TypedName : "B.J.");
            ShowScreen(SessionScreen.HighScores);
        }
    }

    private void TicPlaying(in SessionInput input)
    {
        if (input.MenuBack)
        {
            ShowScreen(SessionScreen.MainMenu);
            MenuIndex = Array.IndexOf(MainMenuItems, "Back To Game");
            return;
        }

        Logic.Tic(input.Game);

        switch (Logic.Player.State)
        {
            case PlayState.Complete:
            case PlayState.SecretLevel:
            case PlayState.Victory:
                BuildIntermission();
                ShowScreen(SessionScreen.Intermission);
                break;
        }

        if (Logic.GameOver)
        {
            gameInProgress = false;
            EnterHighScores(fromGame: true);
        }
    }

    private void BuildIntermission()
    {
        var level = Logic.Level;
        var timeSeconds = (int)(level.TimeTics / WolfLogic.TicsPerSecond);
        var parSeconds = level.LevelIndex < ParTimes.Length ? ParTimes[level.LevelIndex] : 0;
        var killRatio = Ratio(level.KilledMonsters, level.TotalMonsters);
        var secretRatio = Ratio(level.FoundSecrets, level.TotalSecrets);
        var treasureRatio = Ratio(level.FoundTreasure, level.TotalTreasure);

        // The original's bonus rules: 500 points per full second under
        // par, and 10000 for each perfect ratio.
        var bonus = 0;
        if (parSeconds > 0 && timeSeconds < parSeconds)
        {
            bonus += (parSeconds - timeSeconds) * 500;
        }

        if (killRatio == 100)
        {
            bonus += 10000;
        }

        if (secretRatio == 100)
        {
            bonus += 10000;
        }

        if (treasureRatio == 100)
        {
            bonus += 10000;
        }

        Logic.PlayerLogic.GivePoints(bonus);

        Intermission = new IntermissionStats
        {
            Floor = level.LevelIndex + 1,
            TimeSeconds = timeSeconds,
            ParSeconds = parSeconds,
            KillRatio = killRatio,
            SecretRatio = secretRatio,
            TreasureRatio = treasureRatio,
            Bonus = bonus,
        };
    }

    private static int Ratio(int got, int total) =>
        total <= 0 ? 100 : (int)((long)got * 100 / total);

    private void FinishIntermission()
    {
        Logic.AdvanceToNextLevel();
        if (Logic.EpisodeComplete)
        {
            gameInProgress = false;
            ShowScreen(SessionScreen.Victory);
        }
        else
        {
            getPsychedTics = 0;
            ShowScreen(SessionScreen.GetPsyched);
        }
    }

    private void EnterHighScores(bool fromGame)
    {
        if (fromGame && QualifiesForHighScore(Logic.Player.Score))
        {
            pendingHighScore = Logic.Player.Score;
            TypedName = string.Empty;
            ShowScreen(SessionScreen.HighScoreNameEntry);
        }
        else
        {
            ShowScreen(SessionScreen.HighScores);
        }
    }

    private bool QualifiesForHighScore(int score) =>
        score > 0 &&
        (HighScores.Length < HighScoreCount || score > HighScores[^1].Score);

    private void CommitHighScore(string name)
    {
        var entry = new HighScore(name, pendingHighScore, Logic.Level.LevelIndex + 1);
        pendingHighScore = -1;
        HighScores = HighScores
            .Append(entry)
            .OrderByDescending(h => h.Score)
            .Take(HighScoreCount)
            .ToArray();
        storage.SaveHighScores(HighScores);
    }

    private HighScore[] LoadOrDefaultHighScores()
    {
        var scores = storage.LoadHighScores();
        if (scores != null && scores.Length > 0)
        {
            return scores.OrderByDescending(h => h.Score).Take(HighScoreCount).ToArray();
        }

        // The classic default table.
        return new[]
        {
            new HighScore("id software-'92", 10000, 1),
            new HighScore("Adrian Carmack", 10000, 1),
            new HighScore("John Carmack", 10000, 1),
            new HighScore("Kevin Cloud", 10000, 1),
            new HighScore("Tom Hall", 10000, 1),
            new HighScore("John Romero", 10000, 1),
            new HighScore("Jay Wilbur", 10000, 1),
        };
    }
}
