//
// Copyright (C) 2004-2005 Michael Liebscher
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/wolfextractor/wolf/wolf_pm.c (the VSWAP page manager and the
// wall/sprite/sound page decoders).
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
using System.IO;

namespace Wolfenstein.Brix.GameEngine.Assets;

/// <summary>
/// The decoded contents of a VSWAP page file: wall textures, sprites
/// and digitized sound effects. The shareware VSWAP.WL1 is sparse -
/// pages for registered-only content have zero length and decode to
/// <see cref="Texture.Empty"/> slots, so all indices line up with the
/// full registered numbering.
/// </summary>
public sealed class VswapFile
{
    private const int PageTextureSize = 64;

    private VswapFile()
    {
    }

    /// <summary>The total number of pages in the file.</summary>
    public int ChunkCount { get; private set; }

    /// <summary>The page index of the first sprite (pages before it are walls).</summary>
    public int SpriteStart { get; private set; }

    /// <summary>The page index of the first digitized-sound page.</summary>
    public int SoundStart { get; private set; }

    /// <summary>
    /// The decoded 64x64 wall textures, indexed by wall picture number.
    /// Sparse shareware slots hold <see cref="Texture.Empty"/>.
    /// </summary>
    public Texture[] Walls { get; private set; }

    /// <summary>
    /// The decoded 64x64 sprite textures, indexed by sprite number in
    /// the full registered numbering. Sparse shareware slots hold
    /// <see cref="Texture.Empty"/>.
    /// </summary>
    public Texture[] Sprites { get; private set; }

    /// <summary>
    /// The digitized sound effects: raw 8-bit unsigned mono PCM at
    /// roughly 7000 Hz, indexed by digitized sound number.
    /// </summary>
    public byte[][] DigitizedSounds { get; private set; }

    /// <summary>Loads and fully decodes a VSWAP file.</summary>
    public static VswapFile Load(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 6)
        {
            throw new InvalidWolfDataException("VSWAP file is too short to hold its header.");
        }

        var chunkCount = BitConverter.ToUInt16(data, 0);
        var spriteStart = BitConverter.ToUInt16(data, 2);
        var soundStart = BitConverter.ToUInt16(data, 4);
        if (spriteStart > soundStart || soundStart > chunkCount)
        {
            throw new InvalidWolfDataException(
                $"VSWAP header is inconsistent: {chunkCount} chunks, sprites at {spriteStart}, sounds at {soundStart}.");
        }

        var headerSize = 6 + chunkCount * 4 + chunkCount * 2;
        if (data.Length < headerSize)
        {
            throw new InvalidWolfDataException("VSWAP file is too short to hold its page tables.");
        }

        var offsets = new int[chunkCount];
        var lengths = new int[chunkCount];
        for (var i = 0; i < chunkCount; i++)
        {
            offsets[i] = (int)BitConverter.ToUInt32(data, 6 + i * 4);
            lengths[i] = BitConverter.ToUInt16(data, 6 + chunkCount * 4 + i * 2);
        }

        var file = new VswapFile
        {
            ChunkCount = chunkCount,
            SpriteStart = spriteStart,
            SoundStart = soundStart,
            Walls = new Texture[spriteStart],
            Sprites = new Texture[soundStart - spriteStart],
        };

        for (var page = 0; page < spriteStart; page++)
        {
            file.Walls[page] = IsPresent(offsets, lengths, page)
                ? DecodeWall(GetPage(data, offsets, lengths, page))
                : Texture.Empty;
        }

        for (var page = spriteStart; page < soundStart; page++)
        {
            file.Sprites[page - spriteStart] = IsPresent(offsets, lengths, page)
                ? DecodeSprite(GetPage(data, offsets, lengths, page))
                : Texture.Empty;
        }

        file.DigitizedSounds = DecodeDigitizedSounds(data, offsets, lengths, soundStart, chunkCount);
        return file;
    }

    private static bool IsPresent(int[] offsets, int[] lengths, int page) =>
        offsets[page] > 0 && lengths[page] > 0;

    private static byte[] GetPage(byte[] data, int[] offsets, int[] lengths, int page)
    {
        if (offsets[page] + lengths[page] > data.Length)
        {
            throw new InvalidWolfDataException($"VSWAP page {page} extends past the end of the file.");
        }

        var pageData = new byte[lengths[page]];
        Array.Copy(data, offsets[page], pageData, 0, lengths[page]);
        return pageData;
    }

    /// <summary>
    /// Decodes a wall page: 4096 palette indices stored column-major
    /// (source index (x &lt;&lt; 6) + y), returned as a row-major texture.
    /// </summary>
    private static Texture DecodeWall(byte[] page)
    {
        if (page.Length < PageTextureSize * PageTextureSize)
        {
            throw new InvalidWolfDataException("VSWAP wall page is shorter than 64x64 bytes.");
        }

        var pixels = new uint[PageTextureSize * PageTextureSize];
        for (var x = 0; x < PageTextureSize; x++)
        {
            for (var y = 0; y < PageTextureSize; y++)
            {
                pixels[y * PageTextureSize + x] = WolfPalette.Rgba[page[(x << 6) + y]];
            }
        }

        return new Texture(pixels, PageTextureSize, PageTextureSize);
    }

    /// <summary>
    /// Decodes a sprite page (the original t_compshape record): a
    /// left/right column range, per-column offsets to span commands,
    /// and vertical spans of palette indices. Uncovered pixels stay
    /// fully transparent.
    /// </summary>
    private static Texture DecodeSprite(byte[] page)
    {
        if (page.Length < 4)
        {
            throw new InvalidWolfDataException("VSWAP sprite page is shorter than its header.");
        }

        var leftPix = BitConverter.ToUInt16(page, 0);
        var rightPix = BitConverter.ToUInt16(page, 2);
        if (leftPix >= PageTextureSize || rightPix >= PageTextureSize || leftPix > rightPix)
        {
            throw new InvalidWolfDataException(
                $"VSWAP sprite page has an invalid column range {leftPix}..{rightPix}.");
        }

        var pixels = new uint[PageTextureSize * PageTextureSize];
        var commandOffsetBase = 4;
        for (var x = leftPix; x <= rightPix; x++)
        {
            var commandOffset = BitConverter.ToUInt16(page, commandOffsetBase + (x - leftPix) * 2);
            while (true)
            {
                var endRowTimes2 = BitConverter.ToInt16(page, commandOffset);
                if (endRowTimes2 == 0)
                {
                    break;
                }

                var poolAdjust = BitConverter.ToInt16(page, commandOffset + 2);
                var startRowTimes2 = BitConverter.ToInt16(page, commandOffset + 4);
                var sourceIndex = startRowTimes2 / 2 + poolAdjust;
                for (var y = startRowTimes2 / 2; y < endRowTimes2 / 2; y++, sourceIndex++)
                {
                    pixels[y * PageTextureSize + x] = WolfPalette.Rgba[page[sourceIndex]];
                }

                commandOffset += 6;
            }
        }

        return new Texture(pixels, PageTextureSize, PageTextureSize);
    }

    /// <summary>
    /// Decodes the digitized sounds. The last VSWAP page is a table of
    /// (start page, byte length) word pairs, one per sound; each sound's
    /// PCM data spans one or more 4096-byte pages from the sound area.
    /// </summary>
    private static byte[][] DecodeDigitizedSounds(
        byte[] data, int[] offsets, int[] lengths, int soundStart, int chunkCount)
    {
        var tablePage = chunkCount - 1;
        if (!IsPresent(offsets, lengths, tablePage))
        {
            throw new InvalidWolfDataException("VSWAP is missing its digitized-sound table page.");
        }

        var table = GetPage(data, offsets, lengths, tablePage);
        var soundCount = table.Length / 4;
        var sounds = new List<byte[]>(soundCount);
        for (var i = 0; i < soundCount; i++)
        {
            var startPage = BitConverter.ToUInt16(table, i * 4);
            var byteLength = (int)BitConverter.ToUInt16(table, i * 4 + 2);
            if (!IsPresent(offsets, lengths, soundStart + startPage))
            {
                // A registered-only sound absent from the sparse
                // shareware file: keep the slot so numbering lines up.
                sounds.Add(Array.Empty<byte>());
                continue;
            }

            var samples = new byte[byteLength];
            var copied = 0;
            var page = soundStart + startPage;
            while (copied < byteLength && page < tablePage)
            {
                var take = Math.Min(byteLength - copied, lengths[page]);
                Array.Copy(data, offsets[page], samples, copied, take);
                copied += take;
                page++;
            }

            if (copied < byteLength)
            {
                throw new InvalidWolfDataException(
                    $"VSWAP digitized sound {i} is truncated ({copied} of {byteLength} bytes).");
            }

            sounds.Add(samples);
        }

        return sounds.ToArray();
    }
}
