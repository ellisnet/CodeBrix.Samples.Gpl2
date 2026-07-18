//
// Copyright (c) 2022 James Randall
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// CarmackDecode/RlewDecode are vendored for Wolfenstein.Brix from
// csharp-wolfenstein (github.com/JamesRandall/csharp-wolfenstein,
// commit accf9db9, MIT License), file
// CSharpWolfenstein/Assets/Level.cs; HuffmanExpand is translated from
// the Wolf3D iOS v2.1 GPL source (github.com/id-Software/Wolf3D-iOS,
// commit d7fff51d), file wolf3d/wolfextractor/wolf/wolf_gfx.c
// (CAL_HuffExpand).
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

namespace Wolfenstein.Brix.GameEngine.Assets;

/// <summary>
/// The three decompression schemes used by the Wolfenstein 3-D data
/// files: Carmack and RLEW (map planes in GAMEMAPS) and Huffman
/// (graphics chunks in VGAGRAPH).
/// </summary>
public static class Compression
{
    private static ushort GetUint16(byte[] bytes, int offset) =>
        BitConverter.ToUInt16(bytes, offset);

    private static void SetUint16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value & 0xFF);
        bytes[offset + 1] = (byte)(value >> 8);
    }

    /// <summary>
    /// Expands one Carmack-compressed block. The first word of
    /// <paramref name="source"/> is the expanded length in bytes;
    /// near (0xA7) and far (0xA8) pointers copy already-expanded words.
    /// </summary>
    public static byte[] CarmackDecode(byte[] source)
    {
        const byte nearPointer = 0xA7;
        const byte farPointer = 0xA8;

        var size = GetUint16(source, 0);
        var output = new byte[size];
        var inOffset = 2;
        var outOffset = 0;

        while (inOffset < source.Length && outOffset < size)
        {
            var pointerCandidate = source[inOffset + 1];
            if (pointerCandidate == nearPointer || pointerCandidate == farPointer)
            {
                var secondCandidate = source[inOffset];
                if (secondCandidate == 0)
                {
                    // Not a pointer: an escaped literal word whose high
                    // byte happens to be the near/far marker value.
                    output[outOffset] = source[inOffset + 2];
                    output[outOffset + 1] = pointerCandidate;
                    inOffset += 3;
                    outOffset += 2;
                }
                else if (pointerCandidate == nearPointer)
                {
                    var pointerOffset = 2 * source[inOffset + 2];
                    for (var i = 0; i < secondCandidate; i++)
                    {
                        SetUint16(output, outOffset, GetUint16(output, outOffset - pointerOffset));
                        outOffset += 2;
                    }

                    inOffset += 3;
                }
                else
                {
                    var pointerOffset = 2 * GetUint16(source, inOffset + 2);
                    for (var i = 0; i < secondCandidate; i++)
                    {
                        SetUint16(output, outOffset, GetUint16(output, pointerOffset + 2 * i));
                        outOffset += 2;
                    }

                    inOffset += 4;
                }
            }
            else
            {
                SetUint16(output, outOffset, GetUint16(source, inOffset));
                inOffset += 2;
                outOffset += 2;
            }
        }

        return output;
    }

    /// <summary>
    /// Expands one RLEW-compressed block. The first word of
    /// <paramref name="source"/> is the expanded length in bytes; runs
    /// are marked with <paramref name="rlewTag"/> (0xABCD for the
    /// retail map files, read from the first word of MAPHEAD).
    /// </summary>
    public static byte[] RlewDecode(byte[] source, ushort rlewTag)
    {
        var size = GetUint16(source, 0);
        var output = new byte[size];
        var inOffset = 2;
        var outOffset = 0;

        while (inOffset < source.Length && outOffset < size)
        {
            var word = GetUint16(source, inOffset);
            inOffset += 2;
            if (word == rlewTag)
            {
                var length = GetUint16(source, inOffset);
                var value = GetUint16(source, inOffset + 2);
                inOffset += 4;
                for (var i = 0; i < length; i++)
                {
                    SetUint16(output, outOffset, value);
                    outOffset += 2;
                }
            }
            else
            {
                SetUint16(output, outOffset, word);
                outOffset += 2;
            }
        }

        return output;
    }

    /// <summary>
    /// Expands Huffman-compressed data using the 255-node dictionary
    /// from VGADICT. Node values 0-255 emit a byte; values 256 and up
    /// walk to node (value - 256). The head node is 254.
    /// </summary>
    /// <param name="source">The compressed bit stream.</param>
    /// <param name="expandedLength">The number of bytes to produce.</param>
    /// <param name="dictionary">
    /// The 255 (bit0, bit1) node pairs, flattened to 510 ushorts in
    /// file order: node0.bit0, node0.bit1, node1.bit0, ...
    /// </param>
    public static byte[] HuffmanExpand(ReadOnlySpan<byte> source, int expandedLength, ushort[] dictionary)
    {
        var output = new byte[expandedLength];
        var written = 0;
        var node = 254;
        var sourceIndex = 0;
        var exhausted = false;

        var bits = (int)source[sourceIndex++];
        var mask = 1;

        while (written < expandedLength)
        {
            if (exhausted)
            {
                throw new InvalidWolfDataException(
                    "Huffman-compressed data ended before the expected expanded length was produced.");
            }

            var next = (bits & mask) != 0
                ? dictionary[node * 2 + 1]
                : dictionary[node * 2];

            mask <<= 1;
            if (mask == 0x100)
            {
                if (sourceIndex < source.Length)
                {
                    bits = source[sourceIndex++];
                }
                else
                {
                    exhausted = true;
                }

                mask = 1;
            }

            if (next < 256)
            {
                output[written++] = (byte)next;
                node = 254;
            }
            else
            {
                node = next - 256;
            }
        }

        return output;
    }
}
