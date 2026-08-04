//
// Copyright (C) 1993-1996 Id Software, Inc.
// Copyright (C) 2019-2020 Nobuaki Tanaka
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
    public sealed class SaveSlots
    {
        // The number of save-game slots (doomsav0..doomsav5 in vanilla Doom).
        public const int SlotCount = 6;

        private string[] slots;

        private void ReadSlots()
        {
            // The slot descriptions now come from the active storage seam:
            // one .dsg file per slot by default, settings.sqlite in the app.
            slots = ConfigUtilities.Storage.ReadSlotDescriptions();
        }

        public string this[int number]
        {
            get
            {
                if (slots == null)
                {
                    ReadSlots();
                }

                return slots[number];
            }

            set
            {
                slots[number] = value;
            }
        }

        public int Count => slots.Length;
    }
}
