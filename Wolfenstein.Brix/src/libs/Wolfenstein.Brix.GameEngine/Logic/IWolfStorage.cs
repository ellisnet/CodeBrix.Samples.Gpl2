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

namespace Wolfenstein.Brix.GameEngine.Logic;

/// <summary>One high-score table entry.</summary>
public readonly struct HighScore
{
    /// <summary>Creates an entry.</summary>
    public HighScore(string name, int score, int level)
    {
        Name = name;
        Score = score;
        Level = level;
    }

    /// <summary>The player name.</summary>
    public string Name { get; }

    /// <summary>The score.</summary>
    public int Score { get; }

    /// <summary>The one-based floor reached.</summary>
    public int Level { get; }
}

/// <summary>
/// The engine's one storage seam: game configuration, save slots and
/// the high-score table all persist through this interface. The
/// application injects the settings.sqlite-backed implementation; the
/// game itself never writes files.
/// </summary>
public interface IWolfStorage
{
    /// <summary>The number of save slots.</summary>
    const int SaveSlotCount = 8;

    /// <summary>Loads the config text (empty string when absent).</summary>
    string LoadConfigText();

    /// <summary>Stores the config text.</summary>
    void SaveConfigText(string text);

    /// <summary>Loads a save slot's bytes, or null when the slot is empty.</summary>
    byte[] LoadSaveSlot(int slot);

    /// <summary>Stores a save slot with its user-visible name.</summary>
    void SaveSaveSlot(int slot, byte[] data, string description);

    /// <summary>The user-visible slot names; null entries are empty slots.</summary>
    string[] GetSaveSlotDescriptions();

    /// <summary>Loads the high-score table (empty array when unset).</summary>
    HighScore[] LoadHighScores();

    /// <summary>Stores the high-score table.</summary>
    void SaveHighScores(HighScore[] scores);
}

/// <summary>
/// The in-memory fallback storage used when the application injects
/// nothing (tests, headless tools). Holds data for the process
/// lifetime only.
/// </summary>
public sealed class MemoryWolfStorage : IWolfStorage
{
    private readonly byte[][] slots = new byte[IWolfStorage.SaveSlotCount][];
    private readonly string[] descriptions = new string[IWolfStorage.SaveSlotCount];
    private string configText = string.Empty;
    private HighScore[] highScores = Array.Empty<HighScore>();

    /// <inheritdoc />
    public string LoadConfigText() => configText;

    /// <inheritdoc />
    public void SaveConfigText(string text) => configText = text ?? string.Empty;

    /// <inheritdoc />
    public byte[] LoadSaveSlot(int slot) => slots[slot];

    /// <inheritdoc />
    public void SaveSaveSlot(int slot, byte[] data, string description)
    {
        slots[slot] = data;
        descriptions[slot] = description;
    }

    /// <inheritdoc />
    public string[] GetSaveSlotDescriptions() => (string[])descriptions.Clone();

    /// <inheritdoc />
    public HighScore[] LoadHighScores() => highScores;

    /// <inheritdoc />
    public void SaveHighScores(HighScore[] scores) => highScores = scores ?? Array.Empty<HighScore>();
}
