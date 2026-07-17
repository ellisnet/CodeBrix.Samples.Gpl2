// Doom.Brix — GPLv2 (see the repo LICENSE).
//
// A new addition to the vendored managed-doom core (not upstream): the
// single storage seam through which the game persists its config and its
// six save slots. The default implementation (FileDoomStorage) preserves
// managed-doom's original file behavior, so the headless engine and its
// tests keep working with zero setup; the hosting application injects a
// settings.sqlite-backed implementation before boot (exactly like
// ConfigUtilities.DataDirectory), so a shipped game writes no files of
// its own.
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

namespace ManagedDoom
{
    /// <summary>
    /// The persistence seam for the game's config and save slots. The three
    /// original file-I/O call sites (config load/save, save-game load/save,
    /// and the load/save-menu slot descriptions) all route through the active
    /// <see cref="ConfigUtilities.Storage"/> implementation instead of touching
    /// the disk directly.
    /// </summary>
    public interface IDoomStorage
    {
        /// <summary>
        /// Returns the persisted config text (the "key = value" lines the game
        /// serializes), or null/empty when nothing has been stored yet — in
        /// which case the caller keeps the built-in defaults.
        /// </summary>
        string LoadConfigText();

        /// <summary>Persists the serialized config text.</summary>
        void SaveConfigText(string text);

        /// <summary>
        /// Returns the raw (vanilla-format) save bytes for the given slot, or
        /// null when the slot is empty.
        /// </summary>
        byte[] LoadSaveSlot(int slot);

        /// <summary>
        /// Persists the raw save bytes for the given slot along with the slot's
        /// menu description (the 24-byte description prefix is already embedded
        /// in <paramref name="data"/>; it is stored alongside as well so the
        /// load/save menus can list slot names without decoding the payload).
        /// </summary>
        void SaveSaveSlot(int slot, string description, byte[] data);

        /// <summary>
        /// Returns the six slot descriptions for the load/save menus; entries
        /// for empty slots are null. The array length is always
        /// <see cref="SaveSlots.SlotCount"/>.
        /// </summary>
        string[] ReadSlotDescriptions();
    }
}
