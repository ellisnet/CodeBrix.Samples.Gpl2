// Wolfenstein.Brix — GPLv2 (see the repo LICENSE).
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
using System.Runtime.InteropServices;
using CodeBrix.Platform.GameEngine.Drawing;
using CodeBrix.Platform.GameEngine.Rendering;
using Wolfenstein.Brix.GameEngine.Assets;
using Wolfenstein.Brix.GameEngine.Logic;
using Wolfenstein.Brix.GameEngine.Rendering;

namespace Wolfenstein.Brix.Game;

/// <summary>
/// The video backend: the engine's <see cref="SessionRenderer"/> fills
/// one row-major 320x200 RGBA frame (whichever screen the session is
/// showing), and the host's <see cref="PixelFramePresenter"/> —
/// configured here with <see cref="FrameOrientation.Identity"/> — shows
/// it nearest-neighbor scaled into the letterboxed canvas.
/// </summary>
internal sealed class WolfVideo
{
    private readonly SessionRenderer renderer;

    public WolfVideo(WolfAssets assets, GameSession session, PixelFramePresenter presenter)
    {
        renderer = new SessionRenderer(assets, session);

        presenter.Configure(
            FrameComposer.ScreenWidth,
            FrameComposer.ScreenHeight,
            PixelBufferFormat.Rgba8888,
            FrameOrientation.Identity,
            PixelFrameScaleMode.Fit,
            ImageFilterQuality.None);
    }

    /// <summary>Composes the current screen into the host's presentation buffer.</summary>
    public void RenderInto(Span<byte> frame)
        => renderer.Render(MemoryMarshal.Cast<byte, uint>(frame));
}
