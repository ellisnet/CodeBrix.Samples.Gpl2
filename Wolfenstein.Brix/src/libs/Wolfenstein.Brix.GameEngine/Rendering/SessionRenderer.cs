//
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// Recreates the original Wolfenstein 3-D screens (title, menus,
// get-psyched, intermission, victory, high scores) from the shareware
// VGAGRAPH pictures and fonts, plus the classic 17-bit LFSR
// fizzle-fade transition.
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
/// Draws whatever screen the <see cref="GameSession"/> is showing into
/// the 320x200 frame, including gameplay (via <see cref="FrameComposer"/>)
/// and fizzle-fade transitions between screens.
/// </summary>
public sealed class SessionRenderer
{
    private const int Width = FrameComposer.ScreenWidth;
    private const int Height = FrameComposer.ScreenHeight;

    // VGAGRAPH chunk numbers (WL1).
    private const int OptionsPicChunk = 22;
    private const int Cursor1PicChunk = 23;
    private const int Cursor2PicChunk = 24;
    private const int BabyModePicChunk = 31;
    private const int LoadSaveDiskPicChunk = 35;
    private const int LoadGamePicChunk = 40;
    private const int SaveGamePicChunk = 41;
    private const int GuyPicChunk = 55;
    private const int LNum0PicChunk = 57;
    private const int LPercentPicChunk = 67;
    private const int LColonPicChunk = 56;
    private const int Guy2PicChunk = 96;
    private const int BjWinsPicChunk = 97;
    private const int TitlePicChunk = 99;
    private const int HighScoresPicChunk = 102;
    private const int GetPsychedPicChunk = 146;

    // Text colors for the recreated screens.
    private const uint TextColor = 0xFF8C8C8C;
    private const uint SelectedColor = 0xFFF0F0F0;
    private const uint DisabledColor = 0xFF4C0000;
    private const uint HeadlineColor = 0xFFFCFC48;

    private readonly WolfAssets assets;
    private readonly FrameComposer composer;
    private readonly RenderWorld renderWorld;
    private readonly WolfFont smallFont;
    private readonly WolfFont bigFont;

    // The authentic backgrounds, sampled from pics that carry them
    // baked in (so blitted pics merge seamlessly).
    private readonly uint menuBackground;
    private readonly uint intermissionBackground;

    private readonly uint[] previousFrame = new uint[Width * Height];
    private readonly uint[] workFrame = new uint[Width * Height];
    private bool fizzleActive;
    private uint fizzleState = 1;

    /// <summary>Creates the renderer over the session's assets.</summary>
    public SessionRenderer(WolfAssets assets, GameSession session)
    {
        this.assets = assets;
        Session = session;
        composer = new FrameComposer(assets);
        renderWorld = new RenderWorld(session.Logic);
        smallFont = new WolfFont(assets.Graphics.Fonts[0]);
        bigFont = new WolfFont(assets.Graphics.Fonts[1]);

        menuBackground = SampleCorner(Pic(OptionsPicChunk), WolfPalette.Rgba[0x2A]);
        intermissionBackground = SampleCorner(Pic(LNum0PicChunk), WolfPalette.Rgba[0x7D]);
    }

    private static uint SampleCorner(Texture pic, uint fallback) =>
        pic.IsEmpty ? fallback : pic.Pixels[0];

    /// <summary>The session being drawn.</summary>
    public GameSession Session { get; }

    private Texture Pic(int chunk) => assets.Graphics.Pics[chunk - VgaGraphFile.StartPicsChunk];

    /// <summary>Renders the current screen into the presentation frame.</summary>
    public void Render(Span<uint> frame)
    {
        var target = workFrame.AsSpan();
        ComposeScreen(target);

        if (Session.TransitionPending)
        {
            Session.TransitionPending = false;
            fizzleActive = true;
            fizzleState = 1;
        }

        if (fizzleActive)
        {
            // Reveal the new screen through the previous one with the
            // classic 17-bit LFSR dissolve, ~3 frames' worth per tic.
            var revealed = 0;
            while (revealed < 12000)
            {
                var y = (int)(fizzleState & 0xFF) - 1;
                var x = (int)((fizzleState >> 8) & 0x1FF);
                var lsb = fizzleState & 1;
                fizzleState >>= 1;
                if (lsb != 0)
                {
                    fizzleState ^= 0x00012000;
                }

                if (x < Width && y >= 0 && y < Height)
                {
                    previousFrame[y * Width + x] = workFrame[y * Width + x];
                }

                revealed++;
                if (fizzleState == 1)
                {
                    fizzleActive = false;
                    break;
                }
            }

            previousFrame.CopyTo(frame);
            return;
        }

        target.CopyTo(frame);
        workFrame.CopyTo(previousFrame, 0);
    }

    private void ComposeScreen(Span<uint> frame)
    {
        switch (Session.Screen)
        {
            case SessionScreen.Title:
                FrameComposer.BlitPic(frame, Pic(TitlePicChunk), 0, 0);
                break;

            case SessionScreen.MainMenu:
                DrawMainMenu(frame);
                break;

            case SessionScreen.SoundMenu:
                DrawSoundMenu(frame);
                break;

            case SessionScreen.QuitConfirm:
                DrawQuitConfirm(frame);
                break;

            case SessionScreen.DifficultySelect:
                DrawDifficultySelect(frame);
                break;

            case SessionScreen.LoadMenu:
                DrawSlotMenu(frame, LoadGamePicChunk);
                break;

            case SessionScreen.SaveMenu:
                DrawSlotMenu(frame, SaveGamePicChunk);
                break;

            case SessionScreen.SaveNameEntry:
                DrawSlotMenu(frame, SaveGamePicChunk, nameEntry: true);
                break;

            case SessionScreen.GetPsyched:
                DrawGetPsyched(frame);
                break;

            case SessionScreen.Playing:
                composer.SetViewSize(Session.ViewSize);
                composer.ComposeGameplay(frame, renderWorld);
                break;

            case SessionScreen.Intermission:
                DrawIntermission(frame);
                break;

            case SessionScreen.Victory:
                DrawVictory(frame);
                break;

            case SessionScreen.HighScores:
                DrawHighScores(frame, nameEntry: false);
                break;

            case SessionScreen.HighScoreNameEntry:
                DrawHighScores(frame, nameEntry: true);
                break;
        }
    }

    private void FillMenuBackground(Span<uint> frame)
        => frame.Slice(0, Width * Height).Fill(menuBackground);

    private void DrawMainMenu(Span<uint> frame)
    {
        FillMenuBackground(frame);
        var header = Pic(OptionsPicChunk);
        FrameComposer.BlitPic(frame, header, (Width - header.Width) / 2, 0);

        var y = 60;
        for (var i = 0; i < GameSession.MainMenuItems.Length; i++)
        {
            var item = GameSession.MainMenuItems[i];
            var enabled = item switch
            {
                "Save Game" or "Back To Game" or "End Game" => Session.Logic.Player.State == PlayState.Playing,
                _ => true,
            };
            var color = i == Session.MenuIndex
                ? SelectedColor
                : enabled ? TextColor : DisabledColor;
            bigFont.Draw(frame, Width, 100, y, item, color);
            if (i == Session.MenuIndex)
            {
                DrawCursor(frame, 72, y - 2);
            }

            y += bigFont.Height + 2;
        }

        smallFont.Draw(frame, Width, 70, Height - 14,
            "Arrows move, Enter selects, Esc backs out", TextColor);
    }

    private void DrawCursor(Span<uint> frame, int x, int y)
    {
        var cursor = Pic((Session.ScreenTics / 8) % 2 == 0 ? Cursor1PicChunk : Cursor2PicChunk);
        BlitTransparent(frame, cursor, x, y);
    }

    private static readonly string[] ViewSizeNames = { "Small", "Medium", "Normal" };

    private void DrawSoundMenu(Span<uint> frame)
    {
        FillMenuBackground(frame);
        bigFont.Draw(frame, Width, 116, 24, "Options", HeadlineColor);

        var items = new[]
        {
            $"Sound Effects:  {Session.SfxVolume}",
            $"Music:  {Session.MusicVolume}",
            $"View Size:  {ViewSizeNames[Session.ViewSize]}",
        };
        var y = 76;
        for (var i = 0; i < items.Length; i++)
        {
            var color = i == Session.MenuIndex ? SelectedColor : TextColor;
            bigFont.Draw(frame, Width, 84, y, items[i], color);
            if (i == Session.MenuIndex)
            {
                DrawCursor(frame, 56, y - 2);
            }

            y += bigFont.Height + 6;
        }

        smallFont.Draw(frame, Width, 70, Height - 14,
            "Enter steps a setting, Esc backs out", TextColor);
    }

    private void DrawQuitConfirm(Span<uint> frame)
    {
        FillMenuBackground(frame);
        bigFont.Draw(frame, Width, 60, 80, "Are you sure you want", HeadlineColor);
        bigFont.Draw(frame, Width, 92, 100, "to quit? (Enter)", HeadlineColor);
        smallFont.Draw(frame, Width, 104, 132, "Esc goes back", TextColor);
    }

    private void DrawDifficultySelect(Span<uint> frame)
    {
        FillMenuBackground(frame);
        bigFont.Draw(frame, Width, 68, 24, "How tough are you?", HeadlineColor);

        var y = 68;
        for (var i = 0; i < GameSession.DifficultyItems.Length; i++)
        {
            var color = i == Session.MenuIndex ? SelectedColor : TextColor;
            bigFont.Draw(frame, Width, 90, y, GameSession.DifficultyItems[i], color);
            if (i == Session.MenuIndex)
            {
                DrawCursor(frame, 62, y - 2);
            }

            y += bigFont.Height + 4;
        }

        var face = Pic(BabyModePicChunk + Session.MenuIndex);
        FrameComposer.BlitPic(frame, face, 256, 68);
    }

    private void DrawSlotMenu(Span<uint> frame, int headerChunk, bool nameEntry = false)
    {
        FillMenuBackground(frame);
        var header = Pic(headerChunk);
        FrameComposer.BlitPic(frame, header, (Width - header.Width) / 2, 4);

        var names = Session.SaveSlotNames;
        var y = 40;
        for (var i = 0; i < names.Length; i++)
        {
            var disk = Pic(LoadSaveDiskPicChunk);
            BlitTransparent(frame, disk, 40, y);

            string text;
            uint color;
            if (nameEntry && i == Session.MenuIndex)
            {
                var blink = (Session.ScreenTics / 16) % 2 == 0 ? "_" : " ";
                text = Session.TypedName + blink;
                color = SelectedColor;
            }
            else
            {
                text = names[i] ?? "- empty -";
                color = i == Session.MenuIndex
                    ? SelectedColor
                    : names[i] != null ? TextColor : DisabledColor;
            }

            bigFont.Draw(frame, Width, 72, y + 2, text, color);
            if (i == Session.MenuIndex && !nameEntry)
            {
                DrawCursor(frame, 12, y - 2);
            }

            y += bigFont.Height + 4;
        }
    }

    private void DrawGetPsyched(Span<uint> frame)
    {
        FillMenuBackground(frame);
        var pic = Pic(GetPsychedPicChunk);
        var x = (Width - pic.Width) / 2;
        var y = (Height - pic.Height) / 2 - 12;
        FrameComposer.BlitPic(frame, pic, x, y);

        // The loading bar beneath, filling over the interstitial's second.
        var barWidth = pic.Width * Math.Min(70, Session.ScreenTics) / 70;
        var barTop = y + pic.Height + 6;
        for (var row = 0; row < 6; row++)
        {
            frame.Slice((barTop + row) * Width + x, barWidth).Fill(WolfPalette.Rgba[0x13]);
        }
    }

    private void DrawIntermission(Span<uint> frame)
    {
        frame.Slice(0, Width * Height).Fill(intermissionBackground);
        var stats = Session.Intermission;
        if (stats == null)
        {
            return;
        }

        // BJ breathes on the left.
        var guy = Pic((Session.ScreenTics / 35) % 2 == 0 ? GuyPicChunk : Guy2PicChunk);
        BlitTransparent(frame, guy, 8, 16);

        bigFont.Draw(frame, Width, 114, 16, $"FLOOR {stats.Floor}", HeadlineColor);
        bigFont.Draw(frame, Width, 114, 34, "COMPLETED", HeadlineColor);

        bigFont.Draw(frame, Width, 114, 60, "BONUS", TextColor);
        DrawLNumber(frame, stats.Bonus, 6, 216, 60);

        bigFont.Draw(frame, Width, 114, 84, "TIME", TextColor);
        DrawLTime(frame, stats.TimeSeconds, 176, 84);
        bigFont.Draw(frame, Width, 114, 104, "PAR", TextColor);
        DrawLTime(frame, stats.ParSeconds, 176, 104);

        DrawRatioLine(frame, 134, "KILL RATIO", stats.KillRatio);
        DrawRatioLine(frame, 154, "SECRET RATIO", stats.SecretRatio);
        DrawRatioLine(frame, 174, "TREASURE RATIO", stats.TreasureRatio);

        smallFont.Draw(frame, Width, 96, Height - 8, "Press Enter to continue", TextColor);
    }

    private void DrawRatioLine(Span<uint> frame, int y, string label, int ratio)
    {
        bigFont.Draw(frame, Width, 24, y, label, TextColor);
        DrawLNumber(frame, ratio, 3, 262, y);
        BlitTransparent(frame, Pic(LPercentPicChunk), 264, y);
    }

    private void DrawLNumber(Span<uint> frame, int value, int maxDigits, int rightX, int y)
    {
        value = Math.Max(0, value);
        var digitWidth = Pic(LNum0PicChunk).Width;
        var i = 0;
        do
        {
            BlitTransparent(frame, Pic(LNum0PicChunk + (value % 10)), rightX - ((i + 1) * digitWidth), y);
            value /= 10;
            i++;
        }
        while (value > 0 && i < maxDigits);
    }

    private void DrawLTime(Span<uint> frame, int totalSeconds, int rightX, int y)
    {
        var minutes = Math.Min(99, totalSeconds / 60);
        var seconds = totalSeconds % 60;
        var digitWidth = Pic(LNum0PicChunk).Width;
        DrawLNumber(frame, seconds / 10 * 10 + seconds % 10, 2, rightX + 2 * digitWidth + 6, y);
        BlitTransparent(frame, Pic(LColonPicChunk), rightX, y);
        DrawLNumber(frame, minutes, 2, rightX, y);
    }

    private void DrawVictory(Span<uint> frame)
    {
        frame.Slice(0, Width * Height).Fill(intermissionBackground);
        var bj = Pic(BjWinsPicChunk);
        BlitTransparent(frame, bj, (Width - bj.Width) / 2, 32);
        bigFont.Draw(frame, Width, 116, 140, "YOU WIN!", HeadlineColor);
        bigFont.Draw(frame, Width, 92, 164, $"SCORE: {Session.Logic.Player.Score}", TextColor);
        smallFont.Draw(frame, Width, 96, Height - 10, "Press Enter to continue", TextColor);
    }

    private void DrawHighScores(Span<uint> frame, bool nameEntry)
    {
        // HIGHSCORESPIC is the 224x56 "high scores" banner.
        FillMenuBackground(frame);
        var banner = Pic(HighScoresPicChunk);
        FrameComposer.BlitPic(frame, banner, (Width - banner.Width) / 2, 8);

        var y = 76;
        foreach (var entry in Session.HighScores)
        {
            bigFont.Draw(frame, Width, 32, y, entry.Name, TextColor);
            bigFont.Draw(frame, Width, 180, y, entry.Level.ToString(), TextColor);
            var score = entry.Score.ToString();
            bigFont.Draw(frame, Width, 288 - bigFont.Measure(score), y, score, TextColor);
            y += bigFont.Height + 1;
        }

        if (nameEntry)
        {
            var blink = (Session.ScreenTics / 16) % 2 == 0 ? "_" : " ";
            bigFont.Draw(frame, Width, 32, Height - 24,
                $"Your name: {Session.TypedName}{blink}", SelectedColor);
        }
    }

    private static void BlitTransparent(Span<uint> frame, Texture pic, int x, int y)
    {
        if (pic.IsEmpty)
        {
            return;
        }

        for (var row = 0; row < pic.Height; row++)
        {
            var frameRow = y + row;
            if (frameRow < 0 || frameRow >= Height)
            {
                continue;
            }

            for (var col = 0; col < pic.Width; col++)
            {
                var frameCol = x + col;
                if (frameCol < 0 || frameCol >= Width)
                {
                    continue;
                }

                var color = pic.Pixels[row * pic.Width + col];
                if (!Texture.IsTransparent(color))
                {
                    frame[frameRow * Width + frameCol] = color;
                }
            }
        }
    }
}
