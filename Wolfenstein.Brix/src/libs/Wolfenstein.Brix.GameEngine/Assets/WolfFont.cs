//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Translated for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/wolfextractor/wolf/wolf_gfx.c (the fontstruct layout and
// glyph decoding in Fontline).
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
/// One VGAGRAPH font: a 16-bit glyph height, 256 16-bit glyph data
/// offsets and 256 8-bit widths, followed by row-major one-byte-per-
/// pixel glyph data (nonzero = on).
/// </summary>
public sealed class WolfFont
{
    private readonly byte[] chunk;
    private readonly ushort[] locations = new ushort[256];
    private readonly byte[] widths = new byte[256];

    /// <summary>Parses a font from its expanded VGAGRAPH chunk.</summary>
    public WolfFont(byte[] fontChunk)
    {
        chunk = fontChunk;
        Height = BitConverter.ToInt16(fontChunk, 0);
        for (var i = 0; i < 256; i++)
        {
            locations[i] = BitConverter.ToUInt16(fontChunk, 2 + i * 2);
            widths[i] = fontChunk[2 + 512 + i];
        }
    }

    /// <summary>The glyph height in pixels.</summary>
    public int Height { get; }

    /// <summary>The width of a character's glyph in pixels.</summary>
    public int WidthOf(char c) => c < 256 ? widths[c] : 0;

    /// <summary>The pixel width of a string.</summary>
    public int Measure(string text)
    {
        var width = 0;
        foreach (var c in text)
        {
            width += WidthOf(c);
        }

        return width;
    }

    /// <summary>
    /// Draws a string into a row-major RGBA frame of the given width,
    /// in the given packed color. Returns the x position after the
    /// last glyph.
    /// </summary>
    public int Draw(Span<uint> frame, int frameWidth, int x, int y, string text, uint color)
    {
        foreach (var c in text)
        {
            if (c >= 256 || widths[c] == 0)
            {
                continue;
            }

            var width = widths[c];
            var source = locations[c];
            for (var row = 0; row < Height; row++)
            {
                var frameRow = y + row;
                if (frameRow < 0)
                {
                    source += (ushort)width;
                    continue;
                }

                for (var col = 0; col < width; col++)
                {
                    if (chunk[source++] != 0)
                    {
                        var frameCol = x + col;
                        var index = frameRow * frameWidth + frameCol;
                        if (frameCol >= 0 && frameCol < frameWidth && index < frame.Length)
                        {
                            frame[index] = color;
                        }
                    }
                }
            }

            x += width;
        }

        return x;
    }
}
