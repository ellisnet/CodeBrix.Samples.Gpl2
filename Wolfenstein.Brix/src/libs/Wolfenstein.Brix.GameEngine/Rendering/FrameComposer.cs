//
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// The screen layout (320x200: play border, centered 3D view, 40-pixel
// status bar with floor/score/lives/face/health/ammo/keys/weapon)
// recreates the original Wolfenstein 3-D HUD using the shareware
// VGAGRAPH pictures; box positions were measured from STATUSBARPIC
// itself.
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
using Wolfenstein.Brix.GameEngine.Logic;

namespace Wolfenstein.Brix.GameEngine.Rendering;

/// <summary>
/// Composes the complete 320x200 game screen into one row-major RGBA
/// buffer: the play border, the 3D view, the weapon overlay and the
/// status bar. Menus, text screens and the fizzle fade draw over the
/// same buffer in later phases.
/// </summary>
public sealed class FrameComposer
{
    /// <summary>The screen width in pixels.</summary>
    public const int ScreenWidth = 320;

    /// <summary>The screen height in pixels.</summary>
    public const int ScreenHeight = 200;

    /// <summary>The status bar height (the bottom band of the screen).</summary>
    public const int StatusBarHeight = 40;

    /// <summary>The selectable 3D view sizes (width, height).</summary>
    public static readonly (int Width, int Height)[] ViewSizes =
    {
        (256, 128),
        (288, 144),
        (304, 152),
    };

    /// <summary>The default view-size index (the classic 304x152).</summary>
    public const int DefaultViewSize = 2;

    private int viewSizeIndex = DefaultViewSize;
    private int viewWidth = ViewSizes[DefaultViewSize].Width;
    private int viewHeight = ViewSizes[DefaultViewSize].Height;
    private int viewOffsetX = (ScreenWidth - ViewSizes[DefaultViewSize].Width) / 2;
    private int viewOffsetY = (ScreenHeight - StatusBarHeight - ViewSizes[DefaultViewSize].Height) / 2;

    // VGAGRAPH chunk numbers (WL1).
    private const int StatusBarPicChunk = 98;
    private const int KnifePicChunk = 103;
    private const int GoldKeyPicChunk = 108;
    private const int SilverKeyPicChunk = 109;
    private const int BlankNumberPicChunk = 110;
    private const int ZeroNumberPicChunk = 111;
    private const int Face1APicChunk = 121;

    private const byte BorderColor = 0x1D;

    private readonly WolfAssets assets;
    private readonly uint[] viewBuffer;
    private readonly Texture statusBarPic;
    private SceneRenderer sceneRenderer;

    /// <summary>Creates a composer (and its scene renderer) for the standard screen.</summary>
    public FrameComposer(WolfAssets assets)
    {
        this.assets = assets;
        sceneRenderer = new SceneRenderer(assets, viewWidth, viewHeight);
        viewBuffer = new uint[ScreenWidth * (ScreenHeight - StatusBarHeight)];
        statusBarPic = assets.Graphics.Pics[StatusBarPicChunk - VgaGraphFile.StartPicsChunk];
    }

    /// <summary>Switches the 3D view size (an index into <see cref="ViewSizes"/>).</summary>
    public void SetViewSize(int index)
    {
        index = Math.Clamp(index, 0, ViewSizes.Length - 1);
        if (index == viewSizeIndex)
        {
            return;
        }

        viewSizeIndex = index;
        (viewWidth, viewHeight) = ViewSizes[index];
        viewOffsetX = (ScreenWidth - viewWidth) / 2;
        viewOffsetY = (ScreenHeight - StatusBarHeight - viewHeight) / 2;
        sceneRenderer = new SceneRenderer(assets, viewWidth, viewHeight);
    }

    private Texture Pic(int chunk) => assets.Graphics.Pics[chunk - VgaGraphFile.StartPicsChunk];

    /// <summary>
    /// Composes one frame of gameplay into <paramref name="frame"/>
    /// (ScreenWidth * ScreenHeight packed RGBA pixels, row-major).
    /// </summary>
    public void ComposeGameplay(Span<uint> frame, RenderWorld world)
    {
        if (frame.Length < ScreenWidth * ScreenHeight)
        {
            throw new ArgumentException("The frame buffer is smaller than the screen.", nameof(frame));
        }

        var logic = world.Logic;
        world.Refresh();

        // The play border above the status bar.
        frame.Slice(0, ScreenWidth * (ScreenHeight - StatusBarHeight)).Fill(WolfPalette.Rgba[BorderColor]);

        // The 3D view, centered in the play area.
        sceneRenderer.Render(viewBuffer, world, LevelAmbience.CeilingColorFor(logic.Level.LevelIndex));
        DrawWeaponOverlay(logic.Player);
        for (var y = 0; y < viewHeight; y++)
        {
            viewBuffer.AsSpan(y * viewWidth, viewWidth)
                .CopyTo(frame.Slice((viewOffsetY + y) * ScreenWidth + viewOffsetX, viewWidth));
        }

        // The status bar and its live contents.
        BlitPic(frame, statusBarPic, 0, ScreenHeight - StatusBarHeight);
        DrawStatusBar(frame, logic);
    }

    /// <summary>
    /// Draws the player's weapon sprite over the 3D view, scaled so the
    /// 64-pixel sprite fills the view height, bottom-centered.
    /// </summary>
    private void DrawWeaponOverlay(PlayerState player)
    {
        var spriteIndex = Spr.SPR_KNIFEREADY + ((int)player.CurrentWeapon * 5) + player.WeaponFrame;
        if (spriteIndex < 0 || spriteIndex >= assets.Vswap.Sprites.Length)
        {
            return;
        }

        var sprite = assets.Vswap.Sprites[spriteIndex];
        if (sprite.IsEmpty)
        {
            return;
        }

        var scale = (double)viewHeight / sprite.Height;
        var drawWidth = (int)(sprite.Width * scale);
        var offsetX = (viewWidth - drawWidth) / 2;
        for (var y = 0; y < viewHeight; y++)
        {
            var srcY = (int)(y / scale);
            for (var x = 0; x < drawWidth; x++)
            {
                var srcX = (int)(x / scale);
                var color = sprite.Pixels[srcY * sprite.Width + srcX];
                if (!Texture.IsTransparent(color))
                {
                    viewBuffer[y * viewWidth + offsetX + x] = color;
                }
            }
        }
    }

    private void DrawStatusBar(Span<uint> frame, WolfLogic logic)
    {
        var player = logic.Player;
        var y = ScreenHeight - StatusBarHeight + 16;

        // Box positions measured from STATUSBARPIC (right-aligned).
        DrawNumber(frame, logic.Level.LevelIndex + 1, 38, y, 2);
        DrawNumber(frame, player.Score, 94, y, 6);
        DrawNumber(frame, player.Lives, 123, y, 1);
        DrawFace(frame, player);
        DrawNumber(frame, player.Health, 193, y, 3);
        DrawNumber(frame, player.Ammo, 232, y, 2);
        DrawKeys(frame, player);
        DrawWeaponIcon(frame, player);
    }

    private void DrawNumber(Span<uint> frame, int value, int rightX, int y, int width)
    {
        value = Math.Max(0, value);
        for (var i = 0; i < width; i++)
        {
            var digitPicChunk = value == 0 && i > 0
                ? BlankNumberPicChunk
                : ZeroNumberPicChunk + (value % 10);
            BlitPic(frame, Pic(digitPicChunk), rightX - ((i + 1) * 8), y);
            value /= 10;
        }
    }

    private void DrawFace(Span<uint> frame, PlayerState player)
    {
        // Seven health bands of three faces, plus grimace at zero.
        int chunk;
        if (player.Health <= 0)
        {
            chunk = Face1APicChunk + 21; // The dead face.
        }
        else
        {
            var band = Math.Min(6, (100 - player.Health) / 15);
            chunk = Face1APicChunk + (band * 3);
        }

        BlitPic(frame, Pic(chunk), 136, ScreenHeight - StatusBarHeight + 5);
    }

    private void DrawKeys(Span<uint> frame, PlayerState player)
    {
        var barTop = ScreenHeight - StatusBarHeight;
        if ((player.Items & PlayerItems.Key1) != 0)
        {
            BlitPic(frame, Pic(GoldKeyPicChunk), 240, barTop + 4);
        }

        if ((player.Items & PlayerItems.Key2) != 0)
        {
            BlitPic(frame, Pic(SilverKeyPicChunk), 240, barTop + 20);
        }
    }

    private void DrawWeaponIcon(Span<uint> frame, PlayerState player)
    {
        BlitPic(frame, Pic(KnifePicChunk + (int)player.CurrentWeapon), 256, ScreenHeight - StatusBarHeight + 8);
    }

    /// <summary>Draws a decoded picture into the frame at a screen position.</summary>
    public static void BlitPic(Span<uint> frame, Texture pic, int x, int y)
    {
        if (pic.IsEmpty)
        {
            return;
        }

        var width = Math.Min(pic.Width, ScreenWidth - x);
        var height = Math.Min(pic.Height, ScreenHeight - y);
        for (var row = 0; row < height; row++)
        {
            pic.Pixels.AsSpan(row * pic.Width, width)
                .CopyTo(frame.Slice((y + row) * ScreenWidth + x, width));
        }
    }
}
