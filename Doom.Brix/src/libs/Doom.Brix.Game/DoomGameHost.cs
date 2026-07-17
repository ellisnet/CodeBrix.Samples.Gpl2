// Doom.Brix — GPLv2 (see the repo LICENSE).
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
using System.IO;
using CodeBrix.Platform.GameEngine.Audio;
using CodeBrix.Platform.GameEngine.Host.Hosting;
using CodeBrix.Platform.GameEngine.Host.Rendering;
using ManagedDoom;

namespace Doom.Brix.Game;

/// <summary>
/// Hosts the vendored managed-doom engine on the CodeBrix.Platform.GameEngine's
/// software-rendered (framebuffer) stack: a dedicated 35 Hz game loop drives
/// <c>doom.Update()</c>, the software renderer fills a 320x200 (or 640x400)
/// column-major RGBA frame, and the canvas's <c>PixelFramePresenter</c> shows it.
/// Construct with the canvas and the folder that holds DOOM1.WAD, then call
/// <c>Initialize()</c> once, on the UI thread, from the canvas's
/// <c>FirstStarted</c> handler.
/// </summary>
public sealed class DoomGameHost : SoftwareRenderedGameHostBase
{
    // Doom's fixed logic rate: 35 tics per second, v1 renders 1:1 (fpsscale 1).
    private const int DoomTicRate = 35;

    private readonly string dataDirectory;

    private CommandLineArgs commandLineArgs;
    private Config config;
    private GameContent content;
    private CodeBrixVideo video;
    private CodeBrixSound sound;
    private CodeBrixMusic music;
    private CodeBrixUserInput userInput;
    private ManagedDoom.Doom doom;
    private bool gameCompleted;

    /// <summary>
    /// Creates the host.
    /// </summary>
    /// <param name="canvas">The canvas the game presents to.</param>
    /// <param name="dataDirectoryPath">
    /// The game-data folder: the verified assets folder holding DOOM1.WAD, where the
    /// config file and save-game slots also live.
    /// </param>
    public DoomGameHost(GameSurfaceCanvas canvas, string dataDirectoryPath)
        : base(canvas, DoomTicRate)
    {
        if (string.IsNullOrWhiteSpace(dataDirectoryPath))
        {
            throw new ArgumentException(
                "The data directory (the folder holding DOOM1.WAD) is required",
                nameof(dataDirectoryPath));
        }

        dataDirectory = dataDirectoryPath;
    }

    /// <summary>
    /// Raised (on the game-loop thread) when the player completes Doom's quit flow;
    /// the app layer closes the window in response. The config file has already been
    /// saved when this fires.
    /// </summary>
    public event Action GameExited;

    /// <summary>
    /// The app layer's keyboard-focus probe for the game canvas (must be safe to
    /// call from the game-loop thread). Doom uses it for its mouse-grab decisions;
    /// unset means always focused.
    /// </summary>
    public Func<bool> FocusProbe { get; set; }

    /// <inheritdoc />
    protected override void ConfigureAudio()
        // Pin the shared device before any SoundChannel/StreamingAudioSource exists.
        => AudioSystem.Initialize(44100, 2);

    /// <inheritdoc />
    protected override void OnLoadContent()
    {
        // The single funnel for the disk paths the game core still touches:
        // DOOM1.WAD discovery resolves against this directory.
        ConfigUtilities.DataDirectory = dataDirectory;

        // Persist config and save slots in the app's settings.sqlite (via
        // Doom.Brix.Settings) instead of writing managed-doom.cfg / doomsav*.dsg
        // files, so the game writes no files of its own.
        ConfigUtilities.Storage = new SqliteDoomStorage();

        commandLineArgs = new CommandLineArgs(Array.Empty<string>());
        config = Config.FromText(ConfigUtilities.Storage.LoadConfigText());
        content = new GameContent(commandLineArgs);
        video = new CodeBrixVideo(config, content, Presenter)
        {
            FocusProbe = () => FocusProbe == null || FocusProbe(),
        };
        sound = new CodeBrixSound(config, content);
        userInput = new CodeBrixUserInput(config);

        // Music needs the shipped SoundFont; when the file is missing (e.g. a
        // hand-trimmed deployment) the core substitutes its Null implementation
        // and the game runs on without music.
        var soundFontPath = Path.Combine(AppContext.BaseDirectory, "ThirdPartyAssets", "TimGM6mb.sf2");
        music = File.Exists(soundFontPath) ? new CodeBrixMusic(config, content, soundFontPath) : null;

        doom = new ManagedDoom.Doom(commandLineArgs, config, content, video, sound, music, userInput);
    }

    /// <inheritdoc />
    protected override void OnTic()
    {
        if (gameCompleted)
        {
            return;
        }

        userInput.PostPendingEventsTo(doom);

        if (doom.Update() == UpdateResult.Completed)
        {
            // Doom's quit flow (F10 / the quit menu) finished: save the config and
            // tell the app layer; the loop keeps presenting the final frame until
            // the app closes.
            gameCompleted = true;
            ConfigUtilities.Storage.SaveConfigText(config.SaveToText());
            GameExited?.Invoke();
        }
    }

    /// <inheritdoc />
    protected override void OnRenderFrame(Span<byte> frameBuffer)
        => video.RenderInto(doom, frameBuffer, Fixed.One);

    /// <inheritdoc />
    protected override void OnShutdown()
    {
        if (userInput != null)
        {
            userInput.Dispose();
            userInput = null;
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

        if (config != null && !gameCompleted)
        {
            ConfigUtilities.Storage.SaveConfigText(config.SaveToText());
        }

        AudioSystem.Shutdown();
    }
}
