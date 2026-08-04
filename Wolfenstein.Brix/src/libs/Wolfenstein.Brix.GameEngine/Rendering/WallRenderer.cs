//
// Copyright (c) 2022 James Randall
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Vendored for Wolfenstein.Brix from csharp-wolfenstein
// (github.com/JamesRandall/csharp-wolfenstein, commit accf9db9,
// MIT License), file CSharpWolfenstein/Engine/WallRenderer.cs;
// adapted to the RenderWorld view over the translated game logic.
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

/// <summary>The wall pass's per-column depth buffer (clips sprites).</summary>
public sealed class WallRenderingResult
{
    internal WallRenderingResult(int viewportWidth)
    {
        ZIndexes = new double[viewportWidth];
    }

    /// <summary>The perpendicular wall distance for each viewport column.</summary>
    public double[] ZIndexes { get; }
}

/// <summary>Draws the textured wall and door columns of the 3D view.</summary>
public static class WallRenderer
{
    private const double TextureSize = 64.0;

    /// <summary>
    /// Casts one ray per viewport column, drawing textured wall/door
    /// columns into <paramref name="buffer"/> (row-major RGBA of size
    /// viewportWidth * viewportHeight) and filling the result's
    /// z-buffer. The result object is reused across frames.
    /// </summary>
    public static void RenderWalls(
        uint[] buffer,
        WolfAssets assets,
        RenderWorld world,
        (int Width, int Height) viewportSize,
        WallRenderingResult result)
    {
        for (var viewportX = 0; viewportX < viewportSize.Width; viewportX++)
        {
            RenderColumn(assets, world, viewportSize, viewportX, buffer, result);
        }
    }

    private static void RenderColumn(
        WolfAssets assets,
        RenderWorld world,
        (int Width, int Height) viewportSize,
        int viewportX,
        uint[] buffer,
        WallRenderingResult result)
    {
        var cameraX = 2.0 * viewportX / viewportSize.Width - 1.0;
        var rayDirectionX = world.DirX + world.PlaneX * cameraX;
        var rayDirectionY = world.DirY + world.PlaneY * cameraX;
        var rayCastResult = RayCaster.Cast(world, world.CamX, world.CamY, rayDirectionX, rayDirectionY);

        var perpendicularWallDistance = double.MaxValue;
        if (rayCastResult.IsHit)
        {
            var cellKind = world.KindAt(rayCastResult.MapHit.X, rayCastResult.MapHit.Y);
            var doorDistanceModifier = cellKind == RenderCellKind.DoorCell ? 0.5 : 0.0;
            perpendicularWallDistance =
                (rayCastResult.Side == Side.NorthSouth
                    ? rayCastResult.TotalSideDistance.X - rayCastResult.DeltaDistance.X
                    : rayCastResult.TotalSideDistance.Y - rayCastResult.DeltaDistance.Y) +
                doorDistanceModifier;
            var lineHeight = viewportSize.Height / perpendicularWallDistance;
            var startY = Math.Max(-lineHeight / 2.0 + viewportSize.Height / 2.0, 0.0);
            var endY = Math.Min(lineHeight / 2.0 + viewportSize.Height / 2.0, viewportSize.Height - 1.0);

            var wallX =
                rayCastResult.Side == Side.NorthSouth
                    ? world.CamY + perpendicularWallDistance * rayDirectionY
                    : world.CamX + perpendicularWallDistance * rayDirectionX;
            var clampedWallX = wallX - Math.Floor(wallX);
            var rawTextureX = (int)(clampedWallX * TextureSize);

            if (cellKind == RenderCellKind.Wall)
            {
                var (textureX, textureIndex) =
                    rayCastResult.Side == Side.NorthSouth && rayDirectionX > 0.0
                        ? (TextureSize - rawTextureX - 1.0,
                           world.WallTexture(rayCastResult.MapHit.X, rayCastResult.MapHit.Y, Side.NorthSouth))
                        : rayCastResult.Side == Side.EastWest && rayDirectionY < 0.0
                            ? (TextureSize - rawTextureX - 1.0,
                               world.WallTexture(rayCastResult.MapHit.X, rayCastResult.MapHit.Y, Side.EastWest))
                            : ((double)rawTextureX,
                               world.WallTexture(rayCastResult.MapHit.X, rayCastResult.MapHit.Y, rayCastResult.Side));

                RenderTextureColumn(
                    assets, viewportSize, lineHeight, startY, textureIndex, endY, textureX, buffer, viewportX);
            }
            else if (cellKind == RenderCellKind.DoorCell)
            {
                var door = world.DoorAt(rayCastResult.MapHit.X, rayCastResult.MapHit.Y);
                var doorOffset = world.DoorOffset(rayCastResult.MapHit.X, rayCastResult.MapHit.Y);
                var textureX = Math.Max(0.0, TextureSize - rawTextureX - 1.0 - doorOffset);

                RenderTextureColumn(
                    assets, viewportSize, lineHeight, startY, door.Texture, endY, textureX, buffer, viewportX);
            }
        }

        result.ZIndexes[viewportX] = perpendicularWallDistance;
    }

    private static void RenderTextureColumn(
        WolfAssets assets,
        (int Width, int Height) viewportSize,
        double lineHeight,
        double startY,
        int textureIndex,
        double endY,
        double textureX,
        uint[] buffer,
        int viewportX)
    {
        if (textureIndex < 0 || textureIndex >= assets.Vswap.Walls.Length)
        {
            return;
        }

        var texture = assets.Vswap.Walls[textureIndex];
        if (texture.IsEmpty)
        {
            return;
        }

        var step = TextureSize / lineHeight;
        var texturePosition = (startY - viewportSize.Height / 2.0 + lineHeight / 2.0) * step;
        var textureColumn = (int)textureX;
        var pixels = texture.Pixels;
        var textureWidth = texture.Width;
        for (var drawY = 0; drawY < (int)(endY - startY); drawY++)
        {
            var textureY = (int)(texturePosition + step * drawY) & ((int)TextureSize - 1);
            buffer[(drawY + (int)startY) * viewportSize.Width + viewportX] =
                pixels[textureY * textureWidth + textureColumn];
        }
    }
}
