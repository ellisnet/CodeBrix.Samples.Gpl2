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
using CodeBrix.Platform.GameEngine;
using CodeBrix.Platform.GameEngine.Audio;
using CodeBrix.Platform.GameEngine.Host.Hosting;
using CodeBrix.Platform.GameEngine.Host.Rendering;
using CodeBrix.Platform.GameEngine.Sdl2;
using CodeBrix.Platform.GameEngine.Sdl2.Gamepad;
using Wolfenstein.Brix.GameEngine.Assets;
using Wolfenstein.Brix.GameEngine.Logic;
using Wolfenstein.Brix.Settings;

namespace Wolfenstein.Brix.Game;

/// <summary>
/// Hosts the Wolfenstein.Brix game shell on the CodeBrix.Platform.GameEngine's
/// software-rendered (framebuffer) stack: a dedicated 70 Hz game loop runs
/// the <see cref="GameSession"/> (title, menus, gameplay, intermissions),
/// the engine composes a 320x200 row-major RGBA frame, and the canvas's
/// <c>PixelFramePresenter</c> shows it. Construct with the canvas and the
/// verified assets folder, then call <c>Initialize()</c> once, on the UI
/// thread, from the canvas's <c>FirstStarted</c> handler.
/// </summary>
public sealed class WolfGameHost : SoftwareRenderedGameHostBase
{
    private readonly string dataDirectory;
    private readonly IWolfStorage storage;

    private WolfAssets assets;
    private GameSession session;
    private WolfVideo video;
    private WolfUserInput userInput;
    private WolfSound sound;
    private WolfMusic music;
    private SdlGamepadManager gamepads;

    /// <summary>
    /// Creates the host.
    /// </summary>
    /// <param name="canvas">The canvas the game presents to.</param>
    /// <param name="dataDirectoryPath">
    /// The game-data folder: the verified assets folder holding the
    /// shareware .WL1 files.
    /// </param>
    /// <param name="storage">
    /// The persistence seam for config, saves and high scores (the
    /// settings.sqlite-backed implementation); null keeps everything
    /// in memory for the process lifetime.
    /// </param>
    public WolfGameHost(GameSurfaceCanvas canvas, string dataDirectoryPath, IWolfStorage storage = null)
        : base(canvas, WolfLogic.TicsPerSecond)
    {
        if (string.IsNullOrWhiteSpace(dataDirectoryPath))
        {
            throw new ArgumentException(
                "The data directory (the folder holding the .WL1 files) is required",
                nameof(dataDirectoryPath));
        }

        dataDirectory = dataDirectoryPath;
        this.storage = storage;
    }

    /// <summary>
    /// Raised (on the game-loop thread) when the player chooses Quit
    /// from the main menu; the app layer closes the window in response.
    /// </summary>
    public event Action GameExited;

    private volatile bool focusLostPending;

    /// <summary>
    /// Tells the game the canvas lost keyboard focus (callable from
    /// the UI thread): gameplay pauses into the menu on the next tic.
    /// </summary>
    public void NotifyFocusLost() => focusLostPending = true;

    /// <inheritdoc />
    /// <remarks>
    /// Always on, with no setting to enable: a controller that is plugged in works, and
    /// one that is not costs nothing. This never throws — when SDL2 or a controller is
    /// missing the manager comes back reporting itself unavailable, and says why.
    /// </remarks>
    protected override void ConfigureGamepads()
    {
        // The engine's own status logging goes through ILogger at Information, which the
        // host's default LogLevel.Warning filters out — so a WORKING controller would log
        // nothing at all. Suppress it and report through the app's own log instead, which
        // is also what the settings screen's sink replays.
        gamepads = Engine.Instance.InitializeSdlGamepadManager(logStatus: false);
        LogGamepadAvailability();
    }

    private void LogGamepadAvailability()
    {
        if (!gamepads.IsAvailable)
        {
            LoggingService.LogWarning($"Gamepad support unavailable: {gamepads.UnavailableReason}");
            return;
        }

        if (gamepads.ConnectedAdapters.Count == 0)
        {
            LoggingService.LogInfo($"Gamepad support ready. {gamepads.GetNoControllersHint()}");
            return;
        }

        foreach (var adapter in gamepads.ConnectedAdapters)
        {
            // The mapping string is logged deliberately: it is what reconciles a device's
            // raw button and axis numbering with the standard layout, and it varies by
            // transport (the same pad reports differently over Bluetooth than over USB).
            LoggingService.LogInfo(
                $"Gamepad connected: {adapter.Name} (id {adapter.GamepadId}); mapping: {adapter.GetMappingString()}");
        }
    }

    /// <inheritdoc />
    protected override void ConfigureAudio()
        // Pin the shared device before any SoundChannel exists.
        => AudioSystem.Initialize(44100, 2);

    /// <inheritdoc />
    protected override void OnLoadContent()
    {
        assets = WolfAssets.Load(dataDirectory);
        session = new GameSession(assets, storage);
        session.QuitRequested += () => GameExited?.Invoke();
        video = new WolfVideo(assets, session, Presenter);
        userInput = new WolfUserInput(session, gamepads);
        sound = new WolfSound(assets, session.Logic);
        music = new WolfMusic(assets, session);
    }

    /// <inheritdoc />
    protected override void OnTic()
    {
        if (focusLostPending)
        {
            focusLostPending = false;
            session.OnFocusLost();
        }

        session.Tic(userInput.BuildInput());
        sound.Volume = session.SfxVolume / 10.0f;
        music.Update();
    }

    /// <inheritdoc />
    protected override void OnRenderFrame(Span<byte> frameBuffer)
        => video.RenderInto(frameBuffer);

    /// <inheritdoc />
    protected override void OnShutdown()
    {
        if (userInput != null)
        {
            userInput.Dispose();
            userInput = null;
        }

        if (gamepads != null)
        {
            // Detach before disposing: the engine's input poll reaches the manager
            // through this property, and it must not find a disposed one there.
            Engine.Instance.Input.GamepadManager = null;
            gamepads.Dispose();
            gamepads = null;
        }

        if (music != null)
        {
            music.Dispose();
            music = null;
        }

        if (sound != null)
        {
            sound.Dispose();
            sound = null;
        }

        AudioSystem.Shutdown();
    }
}
