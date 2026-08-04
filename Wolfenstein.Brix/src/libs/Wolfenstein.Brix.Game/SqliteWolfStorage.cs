// Wolfenstein.Brix — GPLv2 (see the repo LICENSE).
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
using System.Globalization;
using System.Text;
using Wolfenstein.Brix.GameEngine.Logic;
using Wolfenstein.Brix.Settings;

namespace Wolfenstein.Brix.Game;

/// <summary>
/// The <see cref="IWolfStorage"/> implementation the application injects
/// at boot: it persists the game config, all eight save slots and the
/// high-score table as strings in the app's single portable
/// <c>settings.sqlite</c> (via <see cref="SettingsService"/>), so a
/// shipped game writes no files of its own. Save payloads are stored
/// base64-encoded with each slot's menu name in its own key; high
/// scores are one line per entry (name, score, level, tab-separated).
/// </summary>
/// <remarks>
/// This class talks only to the <see cref="SettingsService"/> facade;
/// <c>SettingsStore</c> stays the single owner of the sqlite handle.
/// The whole of the app's state (assets-folder choice, config, saves,
/// high scores) therefore lives in <c>settings.sqlite</c> and rides the
/// existing auto-backup, pruning and corrupt-file quarantine machinery
/// for free.
/// </remarks>
public sealed class SqliteWolfStorage : IWolfStorage
{
    /// <summary>The settings key holding the serialized "wolfenstein-brix.cfg" text.</summary>
    public const string ConfigKey = "Wolfenstein.Brix.Game.Config";

    /// <summary>The settings key holding the high-score table.</summary>
    public const string HighScoresKey = "Wolfenstein.Brix.Game.HighScores";

    /// <summary>The settings key name for a slot's base64 save data.</summary>
    public static string SlotDataKey(int slot) => "Wolfenstein.Brix.Game.SaveSlot" + slot + ".Data";

    /// <summary>The settings key name for a slot's menu name.</summary>
    public static string SlotDescriptionKey(int slot) => "Wolfenstein.Brix.Game.SaveSlot" + slot + ".Description";

    /// <inheritdoc />
    public string LoadConfigText() => SettingsService.Get<string>(ConfigKey, null) ?? string.Empty;

    /// <inheritdoc />
    public void SaveConfigText(string text) => SettingsService.Set(ConfigKey, text ?? string.Empty);

    /// <inheritdoc />
    public byte[] LoadSaveSlot(int slot)
    {
        var base64 = SettingsService.Get<string>(SlotDataKey(slot), null);
        return string.IsNullOrEmpty(base64) ? null : Convert.FromBase64String(base64);
    }

    /// <inheritdoc />
    public void SaveSaveSlot(int slot, byte[] data, string description)
    {
        SettingsService.Set(SlotDataKey(slot), Convert.ToBase64String(data));
        SettingsService.Set(SlotDescriptionKey(slot), description ?? string.Empty);
    }

    /// <inheritdoc />
    public string[] GetSaveSlotDescriptions()
    {
        var descriptions = new string[IWolfStorage.SaveSlotCount];
        for (var i = 0; i < descriptions.Length; i++)
        {
            // A slot is present only when its data key exists; the
            // description key alone never conjures a phantom slot.
            descriptions[i] = SettingsService.HasValue(SlotDataKey(i))
                ? SettingsService.Get<string>(SlotDescriptionKey(i), string.Empty)
                : null;
        }

        return descriptions;
    }

    /// <inheritdoc />
    public HighScore[] LoadHighScores()
    {
        var text = SettingsService.Get<string>(HighScoresKey, null);
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<HighScore>();
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var scores = new HighScore[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split('\t');
            scores[i] = new HighScore(
                parts.Length > 0 ? parts[0] : string.Empty,
                parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) ? score : 0,
                parts.Length > 2 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ? level : 1);
        }

        return scores;
    }

    /// <inheritdoc />
    public void SaveHighScores(HighScore[] scores)
    {
        var builder = new StringBuilder();
        foreach (var entry in scores ?? Array.Empty<HighScore>())
        {
            builder.Append(entry.Name.Replace('\t', ' ').Replace('\n', ' '));
            builder.Append('\t');
            builder.Append(entry.Score.ToString(CultureInfo.InvariantCulture));
            builder.Append('\t');
            builder.Append(entry.Level.ToString(CultureInfo.InvariantCulture));
            builder.Append('\n');
        }

        SettingsService.Set(HighScoresKey, builder.ToString());
    }
}
