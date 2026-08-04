//
// Copyright (c) 2022 James Randall
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Vendored for Wolfenstein.Brix from csharp-wolfenstein
// (github.com/JamesRandall/csharp-wolfenstein, commit accf9db9,
// MIT License), file CSharpWolfenstein/Assets/Texture.cs; adapted to a
// class with an explicit Empty singleton for the sparse shareware
// VSWAP slots.
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
/// A decoded image: packed RGBA pixels (R in the low byte) in row-major
/// order. Pixels with an alpha below 255 are treated as transparent by
/// the renderers.
/// </summary>
public sealed class Texture
{
    /// <summary>The shared empty texture used for sparse (not present) asset slots.</summary>
    public static readonly Texture Empty = new Texture(Array.Empty<uint>(), 0, 0);

    /// <summary>Creates a texture over an existing pixel buffer.</summary>
    public Texture(uint[] pixels, int width, int height)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
    }

    /// <summary>The packed RGBA pixels, row-major, Width * Height entries.</summary>
    public uint[] Pixels { get; }

    /// <summary>The width in pixels.</summary>
    public int Width { get; }

    /// <summary>The height in pixels.</summary>
    public int Height { get; }

    /// <summary>True when this is a sparse placeholder with no pixel data.</summary>
    public bool IsEmpty => Pixels.Length == 0;

    /// <summary>Returns the packed RGBA pixel at (x, y).</summary>
    public uint Get(int x, int y) => Pixels[y * Width + x];

    /// <summary>True when a packed pixel should not be drawn.</summary>
    public static bool IsTransparent(uint packedPixel) => (packedPixel >> 24) < 0xFF;
}
