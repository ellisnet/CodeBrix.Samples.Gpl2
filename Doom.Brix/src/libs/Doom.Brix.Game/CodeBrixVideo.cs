// Doom.Brix — GPLv2 (see the repo LICENSE); the CodeBrix.Platform.GameEngine
// replacement for managed-doom's Silk video backend.
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
using CodeBrix.Platform.GameEngine.Drawing;
using CodeBrix.Platform.GameEngine.Rendering;
using ManagedDoom;
using ManagedDoom.Video;

namespace Doom.Brix.Game;

/// <summary>
/// The game core's <see cref="IVideo"/> backend: managed-doom's software
/// <see cref="Renderer"/> fills a column-major RGBA buffer, and the host's
/// <see cref="PixelFramePresenter"/> — configured here with
/// <see cref="FrameOrientation.Rotate90"/> — draws that buffer directly with no CPU
/// transpose, nearest-neighbor scaled into the letterboxed canvas.
/// </summary>
internal sealed class CodeBrixVideo : IVideo
{
    private readonly Renderer renderer;
    private readonly byte[] renderBuffer;

    public CodeBrixVideo(Config config, GameContent content, PixelFramePresenter presenter)
    {
        renderer = new Renderer(config, content);
        renderBuffer = new byte[4 * renderer.Width * renderer.Height];

        presenter.Configure(
            renderer.Width,
            renderer.Height,
            PixelBufferFormat.Rgba8888,
            FrameOrientation.Rotate90,
            PixelFrameScaleMode.Fit,
            ImageFilterQuality.None);
    }

    /// <summary>
    /// Renders the frame into the host's presentation buffer: the software renderer
    /// fills this backend's column-major buffer, which is then copied into the span the
    /// host presents (256 KB at 35 Hz — negligible).
    /// </summary>
    public void RenderInto(ManagedDoom.Doom doom, Span<byte> frame, Fixed frameFrac)
    {
        renderer.Render(doom, renderBuffer, frameFrac);
        renderBuffer.CopyTo(frame);
    }

    /// <summary>
    /// The app-layer focus probe (wired by the host); Doom pauses single-player games
    /// while focus is away. Defaults to focused when unset.
    /// </summary>
    public Func<bool> FocusProbe { get; set; }

    void IVideo.Render(ManagedDoom.Doom doom, Fixed frameFrac)
    {
        // The game core never drives rendering; the host's OnRenderFrame calls
        // RenderInto with the presentation buffer instead.
        throw new NotSupportedException("Rendering is driven by DoomGameHost.OnRenderFrame.");
    }

    public void InitializeWipe() => renderer.InitializeWipe();

    public bool HasFocus() => FocusProbe == null || FocusProbe();

    public int MaxWindowSize => renderer.MaxWindowSize;

    public int WindowSize
    {
        get => renderer.WindowSize;
        set => renderer.WindowSize = value;
    }

    public bool DisplayMessage
    {
        get => renderer.DisplayMessage;
        set => renderer.DisplayMessage = value;
    }

    public int MaxGammaCorrectionLevel => renderer.MaxGammaCorrectionLevel;

    public int GammaCorrectionLevel
    {
        get => renderer.GammaCorrectionLevel;
        set => renderer.GammaCorrectionLevel = value;
    }

    public int WipeBandCount => renderer.WipeBandCount;

    public int WipeHeight => renderer.WipeHeight;
}
