// Doom.Brix — GPLv2 (see the repo LICENSE).
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
using Doom.Brix.Settings;
using ManagedDoom;

namespace Doom.Brix.Game;

/// <summary>
/// The <see cref="IDoomStorage"/> implementation the application injects at
/// boot: it persists the game config and all six save slots as strings in the
/// app's single portable <c>settings.sqlite</c> (via <see cref="SettingsService"/>),
/// so a shipped game writes no files of its own. The config is stored as its
/// serialized text; each save slot's raw vanilla <c>.dsg</c> bytes are stored
/// base64-encoded, with the slot's menu description in its own key so the
/// load/save menus can list names without decoding ~34&#160;KB of base64.
/// </summary>
/// <remarks>
/// This class talks only to the <see cref="SettingsService"/> facade;
/// <c>SettingsStore</c> stays the single owner of the sqlite handle. The whole
/// of the app's state (assets-folder choice, config, save games) therefore
/// lives in <c>settings.sqlite</c> and rides the existing auto-backup, pruning
/// and corrupt-file quarantine machinery for free.
/// </remarks>
public sealed class SqliteDoomStorage : IDoomStorage
{
    /// <summary>The settings key holding the serialized <c>doom-brix.cfg</c> text.</summary>
    public const string ConfigKey = "Doom.Brix.Game.Config";

    /// <summary>The base settings key name for a slot's base64 save data.</summary>
    public static string SlotDataKey(int slot) => "Doom.Brix.Game.SaveSlot" + slot + ".Data";

    /// <summary>The base settings key name for a slot's menu description.</summary>
    public static string SlotDescriptionKey(int slot) => "Doom.Brix.Game.SaveSlot" + slot + ".Description";

    /// <inheritdoc />
    public string LoadConfigText() => SettingsService.Get<string>(ConfigKey, null);

    /// <inheritdoc />
    public void SaveConfigText(string text) => SettingsService.Set(ConfigKey, text);

    /// <inheritdoc />
    public byte[] LoadSaveSlot(int slot)
    {
        var base64 = SettingsService.Get<string>(SlotDataKey(slot), null);
        return string.IsNullOrEmpty(base64) ? null : Convert.FromBase64String(base64);
    }

    /// <inheritdoc />
    public void SaveSaveSlot(int slot, string description, byte[] data)
    {
        SettingsService.Set(SlotDataKey(slot), Convert.ToBase64String(data));

        // Store the description exactly as the load/save menus decode it from a
        // .dsg — DoomInterop.ToString uppercases and null-terminates the 24-byte
        // prefix — so sqlite-backed slot names match vanilla file-based names
        // byte-for-byte after a restart. (The prefix is already the encoding of
        // the passed-in description, so this preserves it, only normalized.)
        SettingsService.Set(SlotDescriptionKey(slot),
            DoomInterop.ToString(data, 0, SaveAndLoad.DescriptionSize));
    }

    /// <inheritdoc />
    public string[] ReadSlotDescriptions()
    {
        var descriptions = new string[SaveSlots.SlotCount];
        for (var i = 0; i < descriptions.Length; i++)
        {
            // A slot is present only when its data key exists; the description
            // key alone (or a stale one) never conjures a phantom slot.
            descriptions[i] = SettingsService.HasValue(SlotDataKey(i))
                ? SettingsService.Get<string>(SlotDescriptionKey(i), null)
                : null;
        }

        return descriptions;
    }
}
