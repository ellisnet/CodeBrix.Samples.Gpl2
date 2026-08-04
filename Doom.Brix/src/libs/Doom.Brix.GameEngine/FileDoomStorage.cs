// Doom.Brix — GPLv2 (see the repo LICENSE).
//
// A new addition to the vendored managed-doom core (not upstream): the
// default IDoomStorage implementation, which preserves managed-doom's
// original file behavior. Config and save slots are read and written as
// managed-doom.cfg and doomsav{n}.dsg under ConfigUtilities.DataDirectory,
// exactly as before, so the headless engine and its tests keep working
// with no settings store. The application replaces this with a
// settings.sqlite-backed implementation at boot.
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

using System.IO;

namespace ManagedDoom
{
    /// <summary>
    /// The default, file-based <see cref="IDoomStorage"/>: managed-doom's
    /// original persistence, with every path resolved against
    /// <see cref="ConfigUtilities.DataDirectory"/>.
    /// </summary>
    public sealed class FileDoomStorage : IDoomStorage
    {
        public string LoadConfigText()
        {
            var path = ConfigUtilities.GetConfigPath();
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public void SaveConfigText(string text)
        {
            try
            {
                File.WriteAllText(ConfigUtilities.GetConfigPath(), text);
            }
            catch
            {
            }
        }

        public byte[] LoadSaveSlot(int slot)
        {
            var path = GetSlotPath(slot);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public void SaveSaveSlot(int slot, string description, byte[] data)
        {
            using (var writer = new FileStream(GetSlotPath(slot), FileMode.Create, FileAccess.Write))
            {
                writer.Write(data, 0, data.Length);
            }
        }

        public string[] ReadSlotDescriptions()
        {
            var descriptions = new string[SaveSlots.SlotCount];
            var buffer = new byte[SaveAndLoad.DescriptionSize];
            for (var i = 0; i < descriptions.Length; i++)
            {
                var path = GetSlotPath(i);
                if (File.Exists(path))
                {
                    using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        reader.ReadExactly(buffer);
                        descriptions[i] = DoomInterop.ToString(buffer, 0, buffer.Length);
                    }
                }
            }

            return descriptions;
        }

        private static string GetSlotPath(int slot)
        {
            return Path.Combine(ConfigUtilities.DataDirectory, "doomsav" + slot + ".dsg");
        }
    }
}
