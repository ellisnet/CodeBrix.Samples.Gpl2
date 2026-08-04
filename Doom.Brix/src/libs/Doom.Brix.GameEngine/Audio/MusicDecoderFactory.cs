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

using System;

namespace ManagedDoom.Audio;

// The music-lump format detection from upstream's SilkMusic.ReadData,
// made public so any platform backend (or test) can turn a WAD music
// lump into the right decoder.
public static class MusicDecoderFactory
{
    public static IMusicDecoder Create(byte[] data, bool loop)
    {
        var isMus = true;
        for (var i = 0; i < MusDecoder.MusHeader.Length; i++)
        {
            if (data[i] != MusDecoder.MusHeader[i])
            {
                isMus = false;
            }
        }

        if (isMus)
        {
            return new MusDecoder(data, loop);
        }

        var isMidi = true;
        for (var i = 0; i < MidiDecoder.MidiHeader.Length; i++)
        {
            if (data[i] != MidiDecoder.MidiHeader[i])
            {
                isMidi = false;
            }
        }

        if (isMidi)
        {
            return new MidiDecoder(data, loop);
        }

        throw new Exception("Unknown format!");
    }
}
