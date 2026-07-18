//
// Copyright (c) 2022 James Randall
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Composition adapted for Wolfenstein.Brix from csharp-wolfenstein
// (github.com/JamesRandall/csharp-wolfenstein, commit accf9db9,
// MIT License), file CSharpWolfenstein/Engine/ViewportRenderer.cs;
// the solid ceiling/floor split follows the original renderer.
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
using Wolfenstein.Brix.GameEngine.Assets;

namespace Wolfenstein.Brix.GameEngine.Rendering;

/// <summary>
/// Renders the complete 3D view (solid ceiling and floor, textured
/// walls and doors, depth-clipped sprites) into a caller-supplied
/// row-major RGBA buffer.
/// </summary>
public sealed class SceneRenderer
{
    /// <summary>The palette index of the floor color (dark gray in every retail level).</summary>
    public const byte FloorColor = 0x19;

    private readonly WolfAssets assets;
    private readonly (int Width, int Height) viewportSize;
    private readonly WallRenderingResult wallResult;

    /// <summary>Creates a renderer for a fixed viewport size.</summary>
    public SceneRenderer(WolfAssets assets, int viewportWidth, int viewportHeight)
    {
        this.assets = assets;
        viewportSize = (viewportWidth, viewportHeight);
        wallResult = new WallRenderingResult(viewportWidth);
    }

    /// <summary>The viewport width in pixels.</summary>
    public int Width => viewportSize.Width;

    /// <summary>The viewport height in pixels.</summary>
    public int Height => viewportSize.Height;

    /// <summary>
    /// Renders one frame of the 3D view into <paramref name="buffer"/>
    /// (viewport Width * Height packed RGBA pixels, row-major). The
    /// caller must call <see cref="RenderWorld.Refresh"/> first.
    /// </summary>
    public void Render(uint[] buffer, RenderWorld world, byte ceilingColor)
    {
        if (buffer.Length < viewportSize.Width * viewportSize.Height)
        {
            throw new ArgumentException("The frame buffer is smaller than the viewport.", nameof(buffer));
        }

        var ceiling = WolfPalette.Rgba[ceilingColor];
        var floor = WolfPalette.Rgba[FloorColor];
        var halfPixelCount = viewportSize.Width * (viewportSize.Height / 2);
        Array.Fill(buffer, ceiling, 0, halfPixelCount);
        Array.Fill(buffer, floor, halfPixelCount, viewportSize.Width * viewportSize.Height - halfPixelCount);

        WallRenderer.RenderWalls(buffer, assets, world, viewportSize, wallResult);
        SpriteRenderer.RenderSpriteObjects(buffer, viewportSize, assets, world, wallResult);
    }
}
