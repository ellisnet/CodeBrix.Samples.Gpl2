//
// Copyright (c) 2022 James Randall
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Vendored for Wolfenstein.Brix from csharp-wolfenstein
// (github.com/JamesRandall/csharp-wolfenstein, commit accf9db9,
// MIT License), file CSharpWolfenstein/Engine/ObjectRenderer.cs;
// adapted to the RenderWorld view - sprite selection (rotation,
// animation) now happens in the game logic, so this pass only draws.
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

/// <summary>Draws the frame's billboard sprites, depth-clipped by the wall pass.</summary>
public static class SpriteRenderer
{
    private const double TextureSize = 64.0;

    /// <summary>Draws every sprite in the world's (farthest-first) list.</summary>
    public static void RenderSpriteObjects(
        uint[] buffer,
        (int Width, int Height) viewportSize,
        WolfAssets assets,
        RenderWorld world,
        WallRenderingResult wallRenderingResult)
    {
        foreach (var sprite in world.Sprites)
        {
            RenderSprite(buffer, viewportSize, assets, world, sprite, wallRenderingResult.ZIndexes);
        }
    }

    private static void RenderSprite(
        uint[] buffer,
        (int Width, int Height) viewportSize,
        WolfAssets assets,
        RenderWorld world,
        in RenderSprite sprite,
        double[] zIndexes)
    {
        if (sprite.Texture <= 0 || sprite.Texture >= assets.Vswap.Sprites.Length)
        {
            return;
        }

        var spriteTexture = assets.Vswap.Sprites[sprite.Texture];
        if (spriteTexture.IsEmpty)
        {
            return;
        }

        var spriteX = sprite.X - world.CamX;
        var spriteY = sprite.Y - world.CamY;
        var invDet = 1.0 / (world.PlaneX * world.DirY - world.PlaneY * world.DirX);
        var transformX = invDet * (world.DirY * spriteX - world.DirX * spriteY);
        var transformY = invDet * (-world.PlaneY * spriteX + world.PlaneX * spriteY);
        if (transformY <= 0.0)
        {
            return;
        }

        var spriteScreenX = viewportSize.Width / 2.0 * (1.0 + transformX / transformY);
        var spriteWidth = (int)Math.Abs(viewportSize.Height / transformY);
        var drawStartX = (int)Math.Max(0, -spriteWidth / 2.0 + spriteScreenX);
        var drawEndX = (int)Math.Min(viewportSize.Width - 1, spriteWidth / 2.0 + spriteScreenX);
        if (drawStartX >= drawEndX)
        {
            return;
        }

        var spriteHeight = (int)Math.Abs(viewportSize.Height / transformY);
        var drawStartY = Math.Max(0, -spriteHeight / 2 + viewportSize.Height / 2);
        var drawEndY = Math.Min(viewportSize.Height - 1, spriteHeight / 2 + viewportSize.Height / 2);
        var lineHeight = viewportSize.Height / transformY;
        var step = TextureSize / lineHeight;

        var pixels = spriteTexture.Pixels;
        var textureWidth = spriteTexture.Width;
        for (var stripe = drawStartX; stripe < drawEndX; stripe++)
        {
            if (stripe <= 0 || stripe >= viewportSize.Width || transformY >= zIndexes[stripe])
            {
                continue;
            }

            var textureX =
                (int)(256.0 * (stripe - (-spriteWidth / 2.0 + spriteScreenX)) * TextureSize / spriteWidth) / 256;
            if (textureX < 0 || textureX >= textureWidth)
            {
                continue;
            }

            for (var y = drawStartY; y < drawEndY; y++)
            {
                var textureY = (int)((y - viewportSize.Height / 2.0 + lineHeight / 2.0) * step);
                if (textureY < 0 || textureY >= spriteTexture.Height)
                {
                    continue;
                }

                var color = pixels[textureY * textureWidth + textureX];
                if (!Texture.IsTransparent(color))
                {
                    buffer[y * viewportSize.Width + stripe] = color;
                }
            }
        }
    }
}
