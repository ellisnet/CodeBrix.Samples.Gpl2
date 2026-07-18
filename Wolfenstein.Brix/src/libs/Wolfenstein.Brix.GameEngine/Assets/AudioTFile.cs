//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/wolfextractor/wolf/wolf_aud.c (AUDIOHED offset table and
// AUDIOT chunk slicing).
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
using System.IO;

namespace Wolfenstein.Brix.GameEngine.Assets;

/// <summary>
/// The chunked contents of an AUDIOHED/AUDIOT file pair. Chunks are
/// stored raw here; the AdLib sound-effect and IMF music decoding
/// belongs to the audio drivers that consume them.
/// </summary>
public sealed class AudioTFile
{
    // The v1.4 audio chunk layout (verified against the real
    // AUDIOHED.WL1: the music chunks sit at 261, and 288 chunks =
    // 261 + 27 exactly): 87 PC-speaker effects, the same 87 as AdLib
    // data, 87 digitized placeholders, then the music. Shareware
    // v1.4 shares the registered layout; only the ancient 1.0
    // shareware used a 69-sound table.

    /// <summary>The number of sound effects of each kind.</summary>
    public const int SoundCount = 87;

    /// <summary>The chunk index of the first AdLib sound effect.</summary>
    public const int StartAdlibSounds = 87;

    /// <summary>The chunk index of the first digitized-sound placeholder.</summary>
    public const int StartDigitizedSounds = 174;

    /// <summary>The chunk index of the first IMF music track.</summary>
    public const int StartMusic = 261;

    /// <summary>The number of music tracks.</summary>
    public const int MusicTrackCount = 27;

    private AudioTFile(byte[][] chunks)
    {
        Chunks = chunks;
    }

    /// <summary>Every audio chunk, indexed by chunk number; absent chunks hold an empty array.</summary>
    public byte[][] Chunks { get; }

    /// <summary>The number of music chunks present after <see cref="StartMusic"/>.</summary>
    public int MusicCount =>
        Math.Min(MusicTrackCount, Chunks.Length > StartMusic ? Chunks.Length - StartMusic : 0);

    /// <summary>Returns an AdLib sound-effect chunk by sound number.</summary>
    public byte[] GetAdlibSound(int soundNumber) => Chunks[StartAdlibSounds + soundNumber];

    /// <summary>Returns an IMF music chunk by track number.</summary>
    public byte[] GetMusic(int trackNumber) => Chunks[StartMusic + trackNumber];

    /// <summary>Loads an AUDIOHED/AUDIOT pair and slices AUDIOT into chunks.</summary>
    public static AudioTFile Load(string audioHedPath, string audioTPath)
    {
        var head = File.ReadAllBytes(audioHedPath);
        var audio = File.ReadAllBytes(audioTPath);
        var offsetCount = head.Length / 4;
        if (offsetCount < 2)
        {
            throw new InvalidWolfDataException("AUDIOHED file holds no chunk offsets.");
        }

        var chunkCount = offsetCount - 1;
        var chunks = new byte[chunkCount][];
        for (var i = 0; i < chunkCount; i++)
        {
            var start = BitConverter.ToInt32(head, i * 4);
            var end = BitConverter.ToInt32(head, (i + 1) * 4);
            if (start < 0 || end < start || end > audio.Length)
            {
                throw new InvalidWolfDataException(
                    $"AUDIOHED chunk {i} has an invalid extent {start}..{end} for {audio.Length} bytes.");
            }

            var chunk = new byte[end - start];
            Array.Copy(audio, start, chunk, 0, chunk.Length);
            chunks[i] = chunk;
        }

        return new AudioTFile(chunks);
    }
}
