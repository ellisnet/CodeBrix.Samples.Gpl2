//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/wolfextractor/wolf/wolf_gfx.c (VGADICT/VGAHEAD/VGAGRAPH
// setup, chunk expansion and the VGA-mode pic de-planing loop).
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
/// The decoded contents of the VGADICT/VGAHEAD/VGAGRAPH trio: the pic
/// dimension table, every full-screen and menu picture decoded to a
/// texture, the two menu fonts (raw), and the remaining chunks (tile8
/// block and end-art text screens) as expanded bytes.
/// </summary>
public sealed class VgaGraphFile
{
    /// <summary>The chunk index of the pic dimension table.</summary>
    public const int StructPicChunk = 0;

    /// <summary>The chunk index of the first font.</summary>
    public const int StartFontChunk = 1;

    /// <summary>The number of fonts.</summary>
    public const int FontCount = 2;

    /// <summary>The chunk index of the first picture.</summary>
    public const int StartPicsChunk = 3;

    private VgaGraphFile()
    {
    }

    /// <summary>The total number of chunks in the file.</summary>
    public int ChunkCount { get; private set; }

    /// <summary>The (width, height) of each picture, indexed by pic number (chunk - StartPicsChunk).</summary>
    public (int Width, int Height)[] PicTable { get; private set; }

    /// <summary>The decoded pictures, indexed by pic number (chunk - StartPicsChunk).</summary>
    public Texture[] Pics { get; private set; }

    /// <summary>
    /// The two expanded font chunks in their original layout: a 16-bit
    /// glyph height, 256 16-bit glyph data offsets, then 256 8-bit
    /// glyph widths, followed by the glyph pixel bytes.
    /// </summary>
    public byte[][] Fonts { get; private set; }

    /// <summary>
    /// Every expanded chunk, indexed by chunk number, for consumers
    /// that need the raw data (tile8 block, end-art text screens).
    /// Sparse chunks hold an empty array.
    /// </summary>
    public byte[][] Chunks { get; private set; }

    /// <summary>Loads and fully decodes a VGADICT/VGAHEAD/VGAGRAPH trio.</summary>
    public static VgaGraphFile Load(string dictPath, string headPath, string graphPath)
    {
        var dictBytes = File.ReadAllBytes(dictPath);
        if (dictBytes.Length < 255 * 4)
        {
            throw new InvalidWolfDataException("VGADICT file is too short to hold the Huffman dictionary.");
        }

        var dictionary = new ushort[510];
        for (var i = 0; i < 510; i++)
        {
            dictionary[i] = BitConverter.ToUInt16(dictBytes, i * 2);
        }

        var headBytes = File.ReadAllBytes(headPath);
        var entryCount = headBytes.Length / 3;
        if (entryCount < 2)
        {
            throw new InvalidWolfDataException("VGAHEAD file holds no chunk offsets.");
        }

        var graph = File.ReadAllBytes(graphPath);
        var chunkCount = entryCount - 1;
        var offsets = new int[entryCount];
        for (var i = 0; i < entryCount; i++)
        {
            var value = headBytes[i * 3] | (headBytes[i * 3 + 1] << 8) | (headBytes[i * 3 + 2] << 16);
            offsets[i] = value == 0xFFFFFF ? -1 : value;
        }

        if (offsets[entryCount - 1] < 0)
        {
            offsets[entryCount - 1] = graph.Length;
        }

        var file = new VgaGraphFile
        {
            ChunkCount = chunkCount,
            Chunks = new byte[chunkCount][],
        };

        // Expand the pic dimension table first: its entry count defines
        // how many chunks are pictures.
        var structPic = ExpandChunk(graph, offsets, StructPicChunk, dictionary);
        file.Chunks[StructPicChunk] = structPic;
        var picCount = structPic.Length / 4;
        file.PicTable = new (int, int)[picCount];
        for (var i = 0; i < picCount; i++)
        {
            file.PicTable[i] = (
                BitConverter.ToUInt16(structPic, i * 4),
                BitConverter.ToUInt16(structPic, i * 4 + 2));
        }

        file.Fonts = new byte[FontCount][];
        for (var f = 0; f < FontCount; f++)
        {
            file.Fonts[f] = ExpandChunk(graph, offsets, StartFontChunk + f, dictionary);
            file.Chunks[StartFontChunk + f] = file.Fonts[f];
        }

        file.Pics = new Texture[picCount];
        for (var pic = 0; pic < picCount; pic++)
        {
            var chunk = StartPicsChunk + pic;
            if (chunk >= chunkCount || offsets[chunk] < 0)
            {
                file.Pics[pic] = Texture.Empty;
                continue;
            }

            var expanded = ExpandChunk(graph, offsets, chunk, dictionary);
            file.Chunks[chunk] = expanded;
            var (width, height) = file.PicTable[pic];
            file.Pics[pic] = DeplanePic(expanded, width, height);
        }

        // The remaining chunks (the tile8 block and the end-art text
        // screens) all carry an explicit expanded-length longword, with
        // the exception of the tile8 chunk whose size is implicit; keep
        // it compressed-as-stored if the explicit expansion fails.
        for (var chunk = StartPicsChunk + picCount; chunk < chunkCount; chunk++)
        {
            if (offsets[chunk] < 0)
            {
                file.Chunks[chunk] = Array.Empty<byte>();
                continue;
            }

            try
            {
                file.Chunks[chunk] = ExpandChunk(graph, offsets, chunk, dictionary);
            }
            catch (InvalidWolfDataException)
            {
                file.Chunks[chunk] = Array.Empty<byte>();
            }
        }

        return file;
    }

    private static byte[] ExpandChunk(byte[] graph, int[] offsets, int chunk, ushort[] dictionary)
    {
        var start = offsets[chunk];
        if (start < 0)
        {
            throw new InvalidWolfDataException($"VGAGRAPH chunk {chunk} is sparse.");
        }

        var end = -1;
        for (var next = chunk + 1; next < offsets.Length; next++)
        {
            if (offsets[next] >= 0)
            {
                end = offsets[next];
                break;
            }
        }

        if (end < 0 || end > graph.Length || end <= start + 4)
        {
            throw new InvalidWolfDataException($"VGAGRAPH chunk {chunk} has an invalid extent.");
        }

        var expandedLength = BitConverter.ToInt32(graph, start);
        if (expandedLength <= 0 || expandedLength > 512 * 1024)
        {
            throw new InvalidWolfDataException(
                $"VGAGRAPH chunk {chunk} declares an implausible expanded length {expandedLength}.");
        }

        return Compression.HuffmanExpand(
            new ReadOnlySpan<byte>(graph, start + 4, end - start - 4),
            expandedLength,
            dictionary);
    }

    /// <summary>
    /// Converts a pic from the four-plane VGA mode layout to a
    /// row-major texture via the palette.
    /// </summary>
    private static Texture DeplanePic(byte[] source, int width, int height)
    {
        var pixelCount = width * height;
        if (width <= 0 || height <= 0 || (width & 3) != 0 || source.Length < pixelCount)
        {
            throw new InvalidWolfDataException(
                $"VGAGRAPH pic has invalid dimensions {width}x{height} for {source.Length} bytes.");
        }

        var pixels = new uint[pixelCount];
        var lineWidth = width / 4;
        var planeSize = pixelCount / 4;
        for (var i = 0; i < pixelCount; i++)
        {
            var plane = i / planeSize;
            var x = ((i % lineWidth) * 4) + plane;
            var y = (i / lineWidth) % height;
            pixels[y * width + x] = WolfPalette.Rgba[source[i]];
        }

        return new Texture(pixels, width, height);
    }
}
