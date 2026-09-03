# Doom.Brix

Doom.Brix plays the original id Software DOOM shareware episode as a
CodeBrix.Platform desktop application. There is no installer step, and the
application ships no game data: the first time you launch it the window opens
in **Assets Mode**, you pick a folder to keep the game data in, and an embedded
browser opens the idgames catalog entry for the shareware distribution. Browsing is
free, but exactly one file is allowed to download; when it arrives the
application checks it against the known-good size, CRC-32 and MD5, rebuilds the
split self-extracting archive inside it in memory, extracts `DOOM1.WAD` into the
folder you chose, and flips to **Game Mode**. From then on every launch
re-verifies that folder and boots straight into the game. Keyboard, mouse look
and a game controller all work; music plays through a bundled SoundFont; your
configuration and all six save slots live in the application's single portable
`settings.sqlite`, so a shipped build writes no files of its own beside the
executable.

The game itself is an adapted fork of the managed-doom engine, a C# translation
of Linux Doom, vendored into `Doom.Brix/src/libs/Doom.Brix.GameEngine` under its
original `ManagedDoom.*` namespaces and driven by the CodeBrix.Platform.GameEngine
library. Doom.Brix is the reference for hosting a real-time engine loop inside a
CodeBrix.Platform page on all six desktop heads: implementing the engine's video,
sound, music and input backends over platform services, running a
download-verify-extract asset pipeline from a view model, and keeping every byte
of mutable state in one sqlite settings store.

## What this sample shows a CodeBrix.Platform developer

- How a fixed-rate loop runs inside an ordinary XAML page, with the page
  declaring the game surface canvas and the view model owning the host object's
  lifetime: [Host a fixed rate game loop inside a XAML page](../BLUEPRINTS.md#host-a-fixed-rate-game-loop-inside-a-xaml-page).
- How a software renderer's own pixel buffer reaches the screen with no
  per-frame CPU conversion, by telling the presenter the buffer's format,
  orientation, scale mode and filter quality once:
  [Present a software framebuffer through the game surface canvas](../BLUEPRINTS.md#present-a-software-framebuffer-through-the-game-surface-canvas).
- How an expensive object is created exactly once when two prerequisites arrive
  in either order (the canvas has started, and the data folder has verified):
  [Boot an expensive object only when both prerequisites have arrived](../BLUEPRINTS.md#boot-an-expensive-object-only-when-both-prerequisites-have-arrived).
- How discrete key edges and continuous held-key state are both sampled per tic
  on the game-loop thread, with the OS's own key repeat filtered out:
  [Pump keyboard events and held key state into a game loop](../BLUEPRINTS.md#pump-keyboard-events-and-held-key-state-into-a-game-loop).
- How the engine's relative-mouse session turns pointer movement into per-tic
  look deltas, and stays inert on a head that cannot supply one:
  [Grab the mouse for relative movement in a game loop](../BLUEPRINTS.md#grab-the-mouse-for-relative-movement-in-a-game-loop).
- How a focusable rendering surface keeps the keyboard alive across first
  render, clicks on the canvas and window activation:
  [Keep keyboard focus on a game canvas](../BLUEPRINTS.md#keep-keyboard-focus-on-a-game-canvas).
- How minimizing the window parks the whole engine and restoring resumes it
  exactly where it left off, with two idempotent calls from the application
  object: [Pause a game engine when the window is minimized](../BLUEPRINTS.md#pause-a-game-engine-when-the-window-is-minimized).
- How controller support can be always on, never configured, and completely
  inert when SDL2 or a controller is missing:
  [Add optional gamepad support that degrades to keyboard only](../BLUEPRINTS.md#add-optional-gamepad-support-that-degrades-to-keyboard-only).
- How polling the adapter list once per tic survives a controller that sleeps
  and wakes with a new identifier, and why a stick vector needs a unit-circle
  clamp: [Sample a gamepad once per tic without losing a sleeping controller](../BLUEPRINTS.md#sample-a-gamepad-once-per-tic-without-losing-a-sleeping-controller).
- How many short clips are registered once by key and replayed on a fixed pool
  of engine voices with per-play volume, pan and pitch:
  [Play short PCM clips on a pool of engine sound channels](../BLUEPRINTS.md#play-short-pcm-clips-on-a-pool-of-engine-sound-channels).
- How continuously generated audio is fed to the device by an allocation-free
  pull callback on the audio thread:
  [Stream synthesized audio through one engine voice](../BLUEPRINTS.md#stream-synthesized-audio-through-one-engine-voice).
- How a vendored port stays reviewable against its original, with upstream
  per-file copyright headers preserved and the license text beside them:
  [Port an emulator or codec and keep it reviewable against the original](../BLUEPRINTS.md#port-an-emulator-or-codec-and-keep-it-reviewable-against-the-original).
- How an original binary format (the WAD lump directory) is parsed in a library
  with no UI dependency so its behavior can be pinned by tests:
  [Parse original binary data formats into a testable library](../BLUEPRINTS.md#parse-original-binary-data-formats-into-a-testable-library).
- How the WinWpfSkia head is configured so a paint every tic cannot starve
  keyboard delivery: [Keep WPF paints from starving keyboard input](../BLUEPRINTS.md#keep-wpf-paints-from-starving-keyboard-input).
- How one page carries two full-screen modes plus an overlay, switched from a
  single flag on the view model rather than by navigation:
  [Switch a page between two full screen modes with visibility bindings](../BLUEPRINTS.md#switch-a-page-between-two-full-screen-modes-with-visibility-bindings).
- How the view model drives an embedded browser through a bridge the page
  supplies, and reads the authoritative current URL back after redirects:
  [Drive an embedded browser from the view model](../BLUEPRINTS.md#drive-an-embedded-browser-from-the-view-model).
- How a download policy that permits exactly one file lives in the view model as
  a single method the page asks before accepting a download:
  [Enforce a one file download policy from the view model](../BLUEPRINTS.md#enforce-a-one-file-download-policy-from-the-view-model).
- How a file fetched from a mirror you do not control is proven authentic by
  size, CRC-32 and MD5 computed in one streaming pass:
  [Verify a downloaded file against known checksums](../BLUEPRINTS.md#verify-a-downloaded-file-against-known-checksums).
- How an archive nested inside the download is reassembled in memory and only
  the one wanted member is written to disk:
  [Rebuild a nested archive in memory and extract only what you need](../BLUEPRINTS.md#rebuild-a-nested-archive-in-memory-and-extract-only-what-you-need).
- How the application can fetch the file itself instead of through the browser,
  with progress and the request headers a mirror expects:
  [Download a file with progress and mirror friendly request headers](../BLUEPRINTS.md#download-a-file-with-progress-and-mirror-friendly-request-headers).
- How a download-verify-extract pipeline reports stage and percentage to a bound
  overlay while running off the UI thread:
  [Report multi stage background progress to a bound overlay](../BLUEPRINTS.md#report-multi-stage-background-progress-to-a-bound-overlay).
- How a `SimpleCommand` opens the platform folder picker and the choice is
  remembered, re-validated and dropped when the folder is gone:
  [Let the user pick a folder and remember the choice](../BLUEPRINTS.md#let-the-user-pick-a-folder-and-remember-the-choice).
- How view-model logic raises an alert with no reference to the page, using the
  `XamlRoot` getter the page hands it:
  [Show an alert dialog from a view model](../BLUEPRINTS.md#show-an-alert-dialog-from-a-view-model).
- How pressing Enter in the address box runs the same command the GO button
  binds to: [Execute a view model command when Enter is pressed in a text box](../BLUEPRINTS.md#execute-a-view-model-command-when-enter-is-pressed-in-a-text-box).
- How a self-contained subsystem's file persistence is routed into the
  application's settings store through one interface, without forking its call
  sites: [Persist a subsystem behind one storage interface](../BLUEPRINTS.md#persist-a-subsystem-behind-one-storage-interface).
- How the AppSettings add-in is wrapped in one application-named facade that
  libraries call instead of the add-in:
  [Wrap the AppSettings add-in in an application named facade](../BLUEPRINTS.md#wrap-the-appsettings-add-in-in-an-application-named-facade).
- The startup sequence in the right order: service resolver, design-mode flag,
  default font, settings store, then the first page:
  [Bootstrap the application in App xaml cs](../BLUEPRINTS.md#bootstrap-the-application-in-app-xaml-cs).
- How one shared UI and one Core library become six executables that differ only
  in a builder call: [Start one application from six head projects](../BLUEPRINTS.md#start-one-application-from-six-head-projects).
- How `App.xaml` and the views are file-linked into every head from a shared
  shproj rather than referenced as a library:
  [Share App xaml and views through a shared shproj UI project](../BLUEPRINTS.md#share-app-xaml-and-views-through-a-shared-shproj-ui-project).
- Why every shared package belongs in the Core library and each head declares
  exactly one platform runtime package:
  [Carry every shared package in one Core library and one runtime package per head](../BLUEPRINTS.md#carry-every-shared-package-in-one-core-library-and-one-runtime-package-per-head).
- Which projects need the platform's conditional-compilation defines, and why a
  library under `src/libs` needs its own root namespace:
  [Set the RootNamespace and conditional compilation defines CodeBrix Platform needs](../BLUEPRINTS.md#set-the-rootnamespace-and-conditional-compilation-defines-codebrix-platform-needs).
- How a bundled data file and its license texts reach every head's output folder
  from a single item group:
  [Ship bundled assets and license notices into every head output](../BLUEPRINTS.md#ship-bundled-assets-and-license-notices-into-every-head-output).
- The standard test-project shape for a CodeBrix.Platform application, one test
  project per library:
  [Set up an xUnit v3 test project for a CodeBrix Platform application](../BLUEPRINTS.md#set-up-an-xunit-v3-test-project-for-a-codebrix-platform-application).
- How tests that need data you cannot commit say so, and why one suite here
  fails while another skips:
  [Make data dependent tests explain themselves when the data is missing](../BLUEPRINTS.md#make-data-dependent-tests-explain-themselves-when-the-data-is-missing).
- How an assembly whose code under test holds process-static caches is
  serialized, and how a process-global store is pointed at a throwaway folder:
  [Serialize a test assembly that touches process static state](../BLUEPRINTS.md#serialize-a-test-assembly-that-touches-process-static-state).
- How a hardware input path is tested with no hardware, by driving it through
  the engine's adapter interface with a hand-written fake:
  [Test a hardware input path behind the engine adapter interfaces](../BLUEPRINTS.md#test-a-hardware-input-path-behind-the-engine-adapter-interfaces).

## Building, running and testing

There is one solution, `Doom.Brix/Doom.Brix.slnx`. It holds the shared UI
shproj, the Core library, the six head projects, a `Libraries` folder with the
four projects under `src/libs`, and a `Tests` folder with the test projects. It
restores and builds with the plain .NET SDK on Linux, macOS and Windows: the
WinWpfSkia head sets `EnableWindowsTargeting`, so the whole solution builds off
Windows too. There are no per-OS solution variants.

| Head project | Platform |
| --- | --- |
| `Doom.Brix/src/Doom.Brix.LinuxX11` | Linux, X11 |
| `Doom.Brix/src/Doom.Brix.LinuxWayland` | Linux, Wayland |
| `Doom.Brix/src/Doom.Brix.LinuxFrameBuffer` | Linux, framebuffer (no desktop session) |
| `Doom.Brix/src/Doom.Brix.MacOS` | macOS |
| `Doom.Brix/src/Doom.Brix.Win32Skia` | Windows, Win32 |
| `Doom.Brix/src/Doom.Brix.WinWpfSkia` | Windows, WPF (`net10.0-windows`) |

All six are Skia heads; there are no native (WinUI 3, WPF, .NET MAUI) heads.

Prerequisites:

- The .NET 10 SDK. Every project targets `net10.0` except `Doom.Brix.WinWpfSkia`,
  which targets `net10.0-windows`.
- The embedded browser Assets Mode uses. On Windows and macOS the platform
  runtime supplies it; the Linux heads get theirs (WPE WebKit) from the
  CodeBrix.Platform.WebView add-in, which `Doom.Brix.Core` references once for
  all six heads.
- Optional on Linux: the system SDL2 shared library, for game-controller
  support (`sudo apt install libsdl2-2.0-0` on Debian-family systems). When it
  is absent the gamepad manager reports itself unavailable and the game runs on
  keyboard and mouse.
- Game data: none ships with the application, and you do not have to supply it
  by hand. Assets Mode fetches, verifies and installs the shareware
  `DOOM1.WAD` into the folder you pick on first launch.
- No accounts, tokens or GPU are needed.

Build and run one head from the repository root:

```text
dotnet build Doom.Brix/Doom.Brix.slnx
dotnet run --project Doom.Brix/src/Doom.Brix.LinuxX11
```

Substitute any other head project folder for the last argument.

`Doom.Brix/global.json` selects the Microsoft.Testing.Platform runner for the
whole tree, and every test project builds as a self-executing binary
(`OutputType=Exe` with `UseMicrosoftTestingPlatformRunner`). Under that runner
selection a plain solution-level `dotnet test` can report that zero tests ran,
depending on the SDK version. Running each test project's built executable
directly always works:

```text
dotnet build Doom.Brix/Doom.Brix.slnx
Doom.Brix/tests/libs/Doom.Brix.Assets.Tests/bin/Debug/net10.0/Doom.Brix.Assets.Tests
Doom.Brix/tests/libs/Doom.Brix.Game.Tests/bin/Debug/net10.0/Doom.Brix.Game.Tests
Doom.Brix/tests/libs/Doom.Brix.GameEngine.Tests/bin/Debug/net10.0/Doom.Brix.GameEngine.Tests
Doom.Brix/tests/libs/Doom.Brix.Settings.Tests/bin/Debug/net10.0/Doom.Brix.Settings.Tests
Doom.Brix/tests/libs/Doom.Brix.Synth.Tests/bin/Debug/net10.0/Doom.Brix.Synth.Tests
```

The tests need no GPU, no audio device and no network; music synthesis is
exercised headless. Two of the suites want game data that is deliberately not
committed, and they take opposite policies about it:

| Suite | Data it looks for | When the data is absent |
| --- | --- | --- |
| `Doom.Brix.GameEngine.Tests` | `Downloaded/Doom.Brix_assets/DOOM1.WAD`, found by walking up from the test binary | The data-dependent tests **fail** with a message naming the exact path, saying the folder is git-ignored, and pointing at the application's Assets Mode as the way to obtain the file; the pure math and logic tests are unaffected |
| `Doom.Brix.Assets.Tests` | `Downloaded/doom_assets/doom19s.zip`, found the same way (note the different folder name) | Those tests **skip** through `Assert.SkipWhen(...)`; the classifier and negative-path tests still run |

`Doom.Brix.Synth.Tests` needs only the committed SoundFont and its committed
`ReferenceData`, both of which it locates by walking up from the test binary.
`Doom.Brix.GameEngine.Tests` disables parallelism assembly-wide, because the
vendored engine's dummy-content caches are process-static.

## How the projects and folders are organized

```text
Doom.Brix/
  Doom.Brix.slnx                    The one solution: shared UI, Core, six heads, Libraries, Tests
  global.json                       Selects the Microsoft.Testing.Platform test runner for the tree
  THIRD-PARTY-NOTICES.txt           The application's attribution record
  LICENSE_ManagedDoom.txt           License text for the adapted managed-doom engine
  LICENSE_TimGM6mb.txt              License text for the bundled TimGM6mb SoundFont
  src/
    Doom.Brix.UI/                   Shared shproj: App.xaml(.cs) and Views/MainPage.xaml(.cs)
    Doom.Brix.Core/                 Class library carrying every shared package; ViewModels/, Helpers/
    Doom.Brix.LinuxX11/             Head: Program.cs plus exactly one platform runtime package
    Doom.Brix.LinuxWayland/         Head
    Doom.Brix.LinuxFrameBuffer/     Head
    Doom.Brix.MacOS/                Head
    Doom.Brix.Win32Skia/            Head
    Doom.Brix.WinWpfSkia/           Head (net10.0-windows, EnableWindowsTargeting)
    libs/
      Doom.Brix.Assets/             Asset catalog, URL classifier, downloader, verify/extract pipeline
      Doom.Brix.Game/               Engine backends, the game host, sqlite storage, ThirdPartyAssets/
      Doom.Brix.GameEngine/         The adapted managed-doom engine (ManagedDoom.* namespaces)
      Doom.Brix.Settings/           Thin application-named facade over the AppSettings add-in
  tests/
    libs/
      Doom.Brix.Assets.Tests/       URL classification and the verify/extract pipeline
      Doom.Brix.Game.Tests/         Sqlite storage round-trips and the gamepad input logic
      Doom.Brix.GameEngine.Tests/   Engine math, WAD parsing, storage, music, demo compatibility
      Doom.Brix.Settings.Tests/     The settings store the facade wraps
      Doom.Brix.Synth.Tests/        SoundFont object model, with committed ReferenceData/
```

Dependencies run strictly one way. Each head project references only
`Doom.Brix.Core`, imports `Doom.Brix.UI.projitems` (so `App.xaml` and the views
are file-linked into the head, not referenced as an assembly), and declares
exactly one platform runtime package. `Doom.Brix.Core` references all four
libraries under `src/libs` and carries every package the application shares: the
platform, the font, the WebView add-in, the generic host and console logging.
`Doom.Brix.Game` references `Doom.Brix.GameEngine` and `Doom.Brix.Settings` and
adds the game-engine and SDL2 gamepad packages. Below that the graph fans out
into projects with no UI dependency at all: `Doom.Brix.GameEngine` references
only CodeBrix.Audio, `Doom.Brix.Assets` only CodeBrix.Compression, and
`Doom.Brix.Settings` only the AppSettings add-in, which is why each of them is
testable on its own. The bundled SoundFont and the license and notices files are
`Content` items in `Doom.Brix.Game` marked copy-to-output, and copy-to-output
content flows transitively through project references, so every head's output
folder gets a `ThirdPartyAssets/` folder without any head csproj mentioning it.

## CodeBrix libraries and add-ins used

Library names, not package identifiers; for the exact package see the project's
csproj.

| Library or add-in | What it does in this application | Where |
| --- | --- | --- |
| CodeBrix.Platform | The XAML application framework: `Application`, `Window`, `Frame`, `Page`, the Simple MVVM toolkit (`SimpleViewModel`, `SimpleCommand`, `SimpleServiceResolver`, `SimpleDialog`) and the folder picker | `Doom.Brix/src/Doom.Brix.Core/Doom.Brix.Core.csproj`, `Doom.Brix/src/Doom.Brix.UI/App.xaml.cs`, `Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs` |
| CodeBrix.Platform runtime for each head | The per-head runtime; exactly one runtime package per head project and nothing else | the six `Doom.Brix/src/Doom.Brix.<Head>/*.csproj` files |
| CodeBrix.Platform.GameEngine | The fixed-rate loop, the global engine pause and resume, the software-framebuffer presenter and `GameSurfaceCanvas`, the keyboard and mouse pollers, the relative-mouse session, and the game audio channels; one package ships both the engine and the host assemblies | `Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs`, the `CodeBrix*.cs` backends beside it, `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml` |
| CodeBrix.Platform.GameEngine.Sdl2 add-on | Optional SDL2-backed game-controller support, started with the game-controller subsystem only so it opens no display | `Doom.Brix/src/libs/Doom.Brix.Game/DoomGamepadInput.cs`, `Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs` |
| CodeBrix.Platform.WebView add-in | The embedded browser Assets Mode uses; it supplies the Linux heads' WebView, while the Windows and macOS runtimes have one built in | `Doom.Brix/src/Doom.Brix.Core/Doom.Brix.Core.csproj`, `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml`, `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs` |
| CodeBrix.Platform.AppSettings add-in | The single portable `settings.sqlite` store, its startup auto-backup and pruning, typed properties, change events and the logging service the facade forwards to | `Doom.Brix/src/libs/Doom.Brix.Settings/SettingsService.cs`, `Doom.Brix/src/libs/Doom.Brix.Settings/LoggingService.cs` |
| CodeBrix.Platform.Fonts.OpenSans | The application font, set as the default text font family and declared as an `App.xaml` resource | `Doom.Brix/src/Doom.Brix.UI/App.xaml`, `Doom.Brix/src/Doom.Brix.UI/App.xaml.cs` |
| CodeBrix.Audio | The SoundFont synthesizer the MUS and MIDI decoders render music through, and the PCM types the sound backend registers clips with | `Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixMusic.cs`, `Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixSound.cs`, `Doom.Brix/src/libs/Doom.Brix.GameEngine/Audio/` |
| CodeBrix.Compression | Reads the downloaded zip and the split self-extracting archive inside it, and supplies the CRC-32 used for verification | `Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetPipeline.cs`, `Doom.Brix/src/libs/Doom.Brix.Assets/Internal/ChecksumHelper.cs` |
| CodeBrix.Sqlite | Reached transitively through the AppSettings add-in; the settings tests use its types directly | `Doom.Brix/tests/libs/Doom.Brix.Settings.Tests/SettingsStoreTests.cs` |

Third-party libraries:

| Library | What it does in this application | Where |
| --- | --- | --- |
| Microsoft.Extensions.Hosting | The generic host builder `SimpleServiceResolver` builds the container from | `Doom.Brix/src/Doom.Brix.Core/Helpers/HostHelper.cs` |
| Microsoft.Extensions.Logging.Console | The debug-only console logger factory installed before the host is built | `Doom.Brix/src/Doom.Brix.UI/App.xaml.cs` |
| xUnit v3, its Visual Studio runner, and the .NET test SDK | The test framework and the Microsoft.Testing.Platform runner plumbing | the test project csproj files under `Doom.Brix/tests/libs` |
| SilverAssertions | The fluent assertion style every test uses | every test file, for example `Doom.Brix/tests/libs/Doom.Brix.Assets.Tests/AssetUrlClassifierTests.cs` |

## Worth studying in this application

### One page, two modes, and the flag that switches them

The whole application is a single `Page`. `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml`
declares three sibling `Grid` children in z-order: the Assets Mode setup screen,
the Game Mode canvas, and a progress overlay declared last so it paints on top.
Nothing navigates. `MainViewModel` keeps one private bool, `IsGameMode`, and
derives every visible region's `Visibility` from it and from whether an assets
folder has been chosen: `GameModeVisibility`, `AssetsModeVisibility`,
`FolderSetupVisibility`, `BrowserAreaVisibility` and `DownloadOverlayVisibility`.
`SimpleViewModel.GetVisibility(bool)` produces the value, so no converter is
needed, and one private `NotifyModeProperties()` raises all of them together.
Read `Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs` from the
constructor down through the bindable-properties region first, then the XAML.
The view model is instantiated by `<Page.DataContext>` in XAML, which is why it
carries `[Bindable]` and a parameterless constructor whose first line is
`if (IsDesignMode(true)) { return; }`.
Generalized in [Switch a page between two full screen modes with visibility bindings](../BLUEPRINTS.md#switch-a-page-between-two-full-screen-modes-with-visibility-bindings).

### Booting the game host, and the lifecycle points it overrides

Two things must be true before `DoomGameHost` can exist: the game surface canvas
must have rendered at a real size, and Game Mode must be active with a verified
assets folder to use as the game's data directory. Either can happen first. The
view model holds both as fields and funnels both arrival points into one private
`StartGameIfReady()` guard, so the host is constructed exactly once. The canvas
raises `FirstStarted` on the UI thread; the page forwards it to the view model in
one line, which is the whole of that code-behind's involvement. Focus tracking is
the interesting thread detail: the UI thread writes a `volatile` bool from the
canvas's `GotFocus` and `LostFocus`, and the game-loop thread reads it through
the host's `FocusProbe` delegate to make Doom's mouse-grab decisions. In the
other direction the host's `GameExited` event arrives on the game-loop thread and
is marshaled to the UI thread before the application exits. In the MVVM shape the
canvas reaches the view model through a bridge interface exposing "focus changed"
and "run on the UI thread" rather than the control type itself; the ownership is
the part to copy, and the view model already owns the host, the guard and the
exit path. Files: `Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs`, then
`Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`.

`Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs` is the other half, and the one
file that ties the vendored engine to the platform. It subclasses the game
engine's software-rendered host base, passes Doom's fixed logic rate of 35 tics
per second to the base constructor, and overrides the lifecycle points:
`ConfigureGamepads()` creates the optional SDL2 manager, `ConfigureAudio()` pins
the shared audio device before any voice exists, `OnLoadContent()` builds the
config, the content and the four backends and then the game object, `OnTic()`
posts the tic's input and calls `doom.Update()`, and `OnRenderFrame(Span<byte>)`
fills the presentation buffer. `OnShutdown()` is the part worth reading twice: it
disposes in dependency order and detaches the gamepad manager from the engine
before disposing it, because the engine's input poll reaches the manager through
that property and must not find a disposed one there. It also saves the configuration on the way out if Doom's own
quit flow did not already do it. `OnLoadContent()` is also where the two disk
seams are set: `ConfigUtilities.DataDirectory` for WAD discovery, and
`ConfigUtilities.Storage` for everything the game would otherwise write to files.
Generalized in [Boot an expensive object only when both prerequisites have arrived](../BLUEPRINTS.md#boot-an-expensive-object-only-when-both-prerequisites-have-arrived)
and [Host a fixed rate game loop inside a XAML page](../BLUEPRINTS.md#host-a-fixed-rate-game-loop-inside-a-xaml-page).

### Getting a software framebuffer on screen without a per-frame conversion

`Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixVideo.cs` implements the engine's video
interface but inverts control: the game core never drives rendering, so the
interface's own `Render` method deliberately throws, and the host's
`OnRenderFrame` calls `RenderInto` with the presenter's buffer instead. That
inversion is what lets the presenter own the buffer. The backend configures the
presenter once, in its constructor, from the renderer's own dimensions (which
come from configuration, not constants, because Doom can render at a low or a
doubled resolution): pixel format, `FrameOrientation.Rotate90` because Doom's
renderer writes column-major, `PixelFrameScaleMode.Fit` to letterbox, and
`ImageFilterQuality.None` for the nearest-neighbor scaling that pixel art wants.
Declaring the orientation is how a per-frame CPU transpose is avoided entirely.
Generalized in [Present a software framebuffer through the game surface canvas](../BLUEPRINTS.md#present-a-software-framebuffer-through-the-game-surface-canvas).

### Three input paths that all land on the game-loop thread

`Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixUserInput.cs` is worth reading as a
single design. Key *events* (menus, save-name typing, weapon changes) arrive from
the engine's keyboard event poller during the host's per-tic input poll and queue
up for `PostPendingEventsTo`; held-key *state* for movement is polled straight
from the lock-free keyboard adapter when the tic command is built; and mouse look
comes from the host's relative-mouse session as a per-tic delta, with button state
from the mouse adapter. Because all three run on the game-loop thread, none of
them needs synchronization. Two details generalize: the backend must be
constructed from `OnLoadContent()` because the pollers only exist after the host
has wired its input adapters, and its constructor throws a message saying exactly
that; and repeated key events are dropped, because Doom implements its own key
repeat. `DoomKeys.cs` holds the two-way translation between the platform's
virtual-key codes (the same on every head) and the engine's key enumeration,
including the OEM codes with no named member. Doom's core decides when to grab
and release the mouse; on a head with no relative-mouse support the session simply
stays inactive, logs once, and the game runs keyboard-only.
Generalized in [Pump keyboard events and held key state into a game loop](../BLUEPRINTS.md#pump-keyboard-events-and-held-key-state-into-a-game-loop)
and [Grab the mouse for relative movement in a game loop](../BLUEPRINTS.md#grab-the-mouse-for-relative-movement-in-a-game-loop).

### Gamepad support that costs nothing when there is no gamepad

Controller support is always on and has no setting. `ConfigureGamepads()` in the
host asks the engine for an SDL2 gamepad manager and never throws: when SDL2 or a
controller is missing the manager comes back reporting itself unavailable, with a
reason. The host suppresses the engine's own status logging and reports through
the application's logging facade instead, because that logging goes through the
platform's `ILogger` categories at Information level and the application's
debug-only logger filters those to Warning, so a *working* controller would
otherwise log nothing at all. The connected-adapter log deliberately includes the
device's mapping string, because that string is what reconciles a pad's raw button
and axis numbering with the standard layout and it varies by transport.
`Doom.Brix/src/libs/Doom.Brix.Game/DoomGamepadInput.cs` polls the adapter list
every tic and diffs it against the previous tic rather than registering for button
events, because those registrations are per-controller-identifier and a pad that
sleeps and wakes returns with a new one. Its per-tic result is a struct whose
`default` value means "change nothing", which keeps the keyboard-only path free of
special cases; the weapon slot in it is one-based for that reason. Sticks are
clamped back onto the unit circle, because axes clamp independently and a
corner-held stick would otherwise be a diagonal speed boost.
Generalized in [Add optional gamepad support that degrades to keyboard only](../BLUEPRINTS.md#add-optional-gamepad-support-that-degrades-to-keyboard-only)
and [Sample a gamepad once per tic without losing a sleeping controller](../BLUEPRINTS.md#sample-a-gamepad-once-per-tic-without-losing-a-sleeping-controller).

### Sound effects, and music synthesized from a bundled SoundFont

Two backends, two different audio shapes.
`Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixSound.cs` registers every sound lump
once at construction into the engine's audio resource manager under a stable key,
then plays it on one of eight game voices plus a separate UI voice, setting
volume, pan and pitch per play. The listener-relative model is the detail to note:
Doom's original positions a 3D source around the listener, and the comment records
that the equivalent here is a single stereo pan value, so nobody has to re-derive
it. Voice pressure is handled by a priority policy, and pausing skips voices with
almost nothing left to play rather than pausing a clip that is about to end.
`Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixMusic.cs` is the opposite case: music
is generated, not decoded from a file, so it feeds a streaming voice through a
pull callback that runs on the audio thread. Its buffers are allocated generously
up front so the callback never allocates, and the "which track is playing" handoff
uses two `volatile` fields plus a reference check instead of a lock, with the
synthesizer reset inside the callback at the moment the new decoder takes over.
Decoding sits behind `IMusicDecoder` with a factory that sniffs the lump header
(`Doom.Brix/src/libs/Doom.Brix.GameEngine/Audio/`), which is what lets the whole
music path be exercised headless in the tests with no audio device present. The
SoundFont itself is optional at run time: the host looks for it under
`AppContext.BaseDirectory`, and a deployment without it simply runs music-less.
Generalized in [Play short PCM clips on a pool of engine sound channels](../BLUEPRINTS.md#play-short-pcm-clips-on-a-pool-of-engine-sound-channels)
and [Stream synthesized audio through one engine voice](../BLUEPRINTS.md#stream-synthesized-audio-through-one-engine-voice).

### Keeping the keyboard alive

A game surface only receives key events while it holds keyboard focus, and three
separate things take focus away: the very first render, a click on the canvas
itself (which is also how you fire), and the window being deactivated and
activated again. All three are handled. The page hands focus to the canvas from
the `FirstStarted` handler, re-applies it after every pointer release with
`handledEventsToo: true` because the press is often already marked handled, and
exposes one internal method the application object calls on window activation.
That method checks the view model's mode first: in Assets Mode the embedded
browser owns the keyboard and stealing focus would break typing in it. Focus is
always re-applied through the dispatcher, so it lands after whatever took it
finishes processing. The symptom this prevents is genuinely confusing, because a
connected gamepad keeps working the whole time: SDL2 reads the device directly and
needs no window focus. Focus is real view plumbing and belongs in the page; what
the view model owns is the mode the page consults. Two other pieces of the same
problem live elsewhere: the WinWpfSkia head opts into input-fair dispatcher
scheduling, because that head's default posts paints at a priority that outranks
the tier WPF delivers key events on, and a device where one paint takes longer
than the tic period would never empty the paint queue; and the application object
pauses and resumes the whole engine on window visibility changes, so a minimized
window costs nothing and comes back exactly where it left off. Files:
`Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`,
`Doom.Brix/src/Doom.Brix.UI/App.xaml.cs`,
`Doom.Brix/src/Doom.Brix.WinWpfSkia/Program.cs`.
Generalized in [Keep keyboard focus on a game canvas](../BLUEPRINTS.md#keep-keyboard-focus-on-a-game-canvas),
[Keep WPF paints from starving keyboard input](../BLUEPRINTS.md#keep-wpf-paints-from-starving-keyboard-input)
and [Pause a game engine when the window is minimized](../BLUEPRINTS.md#pause-a-game-engine-when-the-window-is-minimized).

### Assets Mode: one permitted download, verified and unpacked

Assets Mode is where the ordinary application patterns concentrate. The view
model owns all of it. A `SimpleCommand` opens the platform folder picker; the
chosen path goes into the settings store and the mode properties are re-derived.
A folder that already holds verified assets skips the download entirely. The
browser is navigated through a bridge the page supplies, and the address box's
text is a two-way bound property that both the GO command and the Enter key run
against; `NavigationCompleted` feeds the authoritative current URL back into that
property, read from the core WebView's own source rather than the XAML `Source`,
which does not reliably reflect redirects. Navigation is never intercepted, so
any mirror can be browsed; only downloads are policed. When the browser starts a
download it asks the view model's `HandleDownloadStarting`, which returns whether
to accept and hands back the temp target path. That method is the whole policy:
`AssetUrlClassifier` in `Doom.Brix/src/libs/Doom.Brix.Assets` decides whether this
is the one permitted file, matching either the suggested file name with the
browser's collision suffix stripped, or the file name in the URL path, because
mirrors serve it both ways; a second download while one is running is refused by
the same method; anything else is canceled with an explanation raised as a dialog
from the view model. In the MVVM shape the page's part of this is the bridge and
one-line event forwards, and the view model owns the policy, the progress state
and the dialogs, which is how it is arranged here. One more detail is worth
copying: the page does not initialize the WebView at all in Game Mode, both to
save the startup cost and to keep its native focus proxy from stealing keyboard
focus from the game canvas. Read
`Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs` from the commands
region to the end, then `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`.
Generalized in [Drive an embedded browser from the view model](../BLUEPRINTS.md#drive-an-embedded-browser-from-the-view-model),
[Enforce a one file download policy from the view model](../BLUEPRINTS.md#enforce-a-one-file-download-policy-from-the-view-model),
[Let the user pick a folder and remember the choice](../BLUEPRINTS.md#let-the-user-pick-a-folder-and-remember-the-choice),
[Execute a view model command when Enter is pressed in a text box](../BLUEPRINTS.md#execute-a-view-model-command-when-enter-is-pressed-in-a-text-box)
and [Show an alert dialog from a view model](../BLUEPRINTS.md#show-an-alert-dialog-from-a-view-model).

### The verify-and-extract pipeline, and what makes it two independent checks

`Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetPipeline.cs` is a static class in a
library with no UI dependency. It takes a file path, a destination folder, an
`IProgress<T>` and a cancellation token, runs the hashing and inflating inside a
`Task.Run` so none of it touches the caller's thread, and throws a typed exception
carrying the stage that failed. That single stage value is what lets the view
model produce the right wording for a download, a verification or an extraction
failure from one catch block. `DoomAssetCatalog.cs` holds every known-good fact
in one place: the permitted file name, the identity of the download and of the
extracted WAD, the default page to browse to, and the names of the two halves of
the split self-extracting archive nested inside the download.

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetCatalog.cs
// The two halves of the split PKZIP self-extracting archive inside
// doom19s.zip; concatenated in this order they are DOOMS_19.EXE.
internal static readonly string[] ArchivePartNames = { "DOOMS_19.1", "DOOMS_19.2" };
```

Those two parts are streamed in order into one presized memory stream, each
verified against the zip's own central-directory CRC-32 during the same copy pass,
and the rebuilt archive is then read for the one member wanted. No intermediate
file touches disk and the temp directory is removed in a `finally` whether the
pipeline succeeded or not. The extracted payload is then checked a second time,
against the catalog's known-good values, which makes the archive's declared
checksums and the catalog's genuinely independent, and a structural sanity check
follows: the file's header must describe a directory that ends exactly at
end-of-file. `ChecksumHelper` computes size, CRC-32 and MD5 in a single streaming
pass with a shared buffer. The launch-time check, `VerifyInstalledAssets`, runs the
same comparison but never throws: any failure returns false, and the application
falls back to the setup screen rather than to an error. `AssetDownloader.cs` is
the alternative path for fetching the file directly rather than through the
browser; it sets an infinite HTTP timeout on purpose, because cancellation
governs, and falls back to the catalog's known size for progress when a server
sends no content length.
Generalized in [Verify a downloaded file against known checksums](../BLUEPRINTS.md#verify-a-downloaded-file-against-known-checksums),
[Rebuild a nested archive in memory and extract only what you need](../BLUEPRINTS.md#rebuild-a-nested-archive-in-memory-and-extract-only-what-you-need),
[Download a file with progress and mirror friendly request headers](../BLUEPRINTS.md#download-a-file-with-progress-and-mirror-friendly-request-headers)
and [Report multi stage background progress to a bound overlay](../BLUEPRINTS.md#report-multi-stage-background-progress-to-a-bound-overlay).

### The vendored engine, and the seam that keeps it from writing files

`Doom.Brix/src/libs/Doom.Brix.GameEngine` is the adapted managed-doom engine,
organized into `Doom/` (the game logic, with its own `Math`, `Map`, `World`,
`Wad`, `Menu` and `Graphics` folders), `Video/`, `Audio/` and `UserInput/`. Its
sources keep their upstream per-file copyright headers, and the license text sits
beside them at `Doom.Brix/LICENSE_ManagedDoom.txt`, which is what makes the port
reviewable against its original. Upstream wrote a configuration file and save-game
files next to the data directory. Rather than editing every call site, the port
declares one interface, `IDoomStorage`, covering all of them, and
`ConfigUtilities.Storage` defaults to the original file-based implementation, so
the headless engine and its own tests need no setup at all. The application
assigns `SqliteDoomStorage` before the game boots.
`Doom.Brix/src/libs/Doom.Brix.Game/SqliteDoomStorage.cs` is worth reading in full:
it talks only to the settings facade, never to the store or the add-in, so the
sqlite handle keeps a single owner and the game's state inherits the add-in's
auto-backup, pruning and corruption handling for free. Save payloads become base64
strings and each slot's human-readable label is stored under a second key, decoded
exactly the way the load and save menus decode it from a save file, so a slot name
matches byte for byte after a restart and a list screen never has to decode a
payload. Slot presence is keyed off the data key alone, so a stale label key can
never resurrect a deleted save. Read `IDoomStorage.cs` and `ConfigUtilities.cs`
first, then `SqliteDoomStorage.cs`, then `DoomGameHost.OnLoadContent()`.
Generalized in [Persist a subsystem behind one storage interface](../BLUEPRINTS.md#persist-a-subsystem-behind-one-storage-interface)
and [Port an emulator or codec and keep it reviewable against the original](../BLUEPRINTS.md#port-an-emulator-or-codec-and-keep-it-reviewable-against-the-original).

### Startup, the settings facade, and the packaging rules that make six heads cheap

Each head's `Program.cs` is a handful of lines that differ only in one builder
call, and `[STAThread]` is on `Main` in every head including the Linux ones. All
the real bootstrap is in `Doom.Brix/src/Doom.Brix.UI/App.xaml.cs`, in a fixed
order: set the default text font family, create the service resolver from the host
provider in `Doom.Brix/src/Doom.Brix.Core/Helpers/HostHelper.cs`, call
`SetIsDesignMode(false)`, open the settings store, and only then
`InitializeComponent()`. Opening the store before `InitializeComponent()` is what
lets a view model constructed by page XAML read settings safely, and
`SetIsDesignMode(false)` must be called or a view model built through the designer
path short-circuits its constructor at run time too. The store is reached through
`Doom.Brix/src/libs/Doom.Brix.Settings/SettingsService.cs`, a static
application-named facade whose whole job is to be the only thing that knows about
the add-in; a companion `LoggingService` does the same for logging, so a library
that logs needs no add-in reference either. Both carry an overload taking a
directory and a `Shutdown()`, which exist so tests can point the process-global
store at a throwaway folder. On the packaging side, three rules do the work: every
shared package sits in `Doom.Brix.Core` and each head declares exactly one runtime
package (the rule is written into every head csproj in capitals, because adding a
second package there is the mistake it prevents); every project that compiles
against the platform sets the platform's conditional-compilation defines, and the
libraries that do not see platform types deliberately omit them; and
`Doom.Brix.Game` keeps a root namespace distinct from the application's, because a
transitive platform reference otherwise generates a duplicate resources type, an
error whose message does not obviously point at the root namespace.
Generalized in [Bootstrap the application in App xaml cs](../BLUEPRINTS.md#bootstrap-the-application-in-app-xaml-cs),
[Start one application from six head projects](../BLUEPRINTS.md#start-one-application-from-six-head-projects),
[Share App xaml and views through a shared shproj UI project](../BLUEPRINTS.md#share-app-xaml-and-views-through-a-shared-shproj-ui-project),
[Wrap the AppSettings add-in in an application named facade](../BLUEPRINTS.md#wrap-the-appsettings-add-in-in-an-application-named-facade),
[Carry every shared package in one Core library and one runtime package per head](../BLUEPRINTS.md#carry-every-shared-package-in-one-core-library-and-one-runtime-package-per-head),
[Set the RootNamespace and conditional compilation defines CodeBrix Platform needs](../BLUEPRINTS.md#set-the-rootnamespace-and-conditional-compilation-defines-codebrix-platform-needs)
and [Ship bundled assets and license notices into every head output](../BLUEPRINTS.md#ship-bundled-assets-and-license-notices-into-every-head-output).

### Testing an application whose data cannot be committed

One test project per library, each an xUnit v3 self-executing binary, each using
SilverAssertions and the family's naming style. The interesting part is how the
suites handle data. `Doom.Brix.GameEngine.Tests` includes demo-playback
compatibility tests that hash the world state every tic against upstream's
expected values, which is the strongest evidence the port still behaves like the
original; those need the shareware WAD, and `TestWad.cs` locates it by walking up
from the test binary's directory so the tests work from any output depth. When it
is not there they **fail**, with a message naming the exact path, saying the
folder is git-ignored, and pointing at the application's own Assets Mode as the
way to get the file, because a silent skip would hide a regression in exactly the
tests that matter most. `Doom.Brix.Assets.Tests` takes the opposite policy for a
file whose absence is not a coverage loss worth failing over, and skips.
Alongside the suite, `UPSTREAM-TEST-COVERAGE.txt` records what was ported, what
needs data, and what was deliberately not ported and why, which is worth keeping
whenever a vendored component arrives with its own tests. Two more habits
generalize: the engine assembly is serialized wholesale because the vendored
dummy-content caches are process-static, and the sqlite storage tests initialize
the process-global settings store once into a throwaway temp folder and clear the
keys they use in each test's constructor, so method ordering never matters.
`Doom.Brix.Game.Tests` shows how to test a hardware path with no hardware, driving
the gamepad logic through the engine's adapter interface with a hand-written fake.
Generalized in [Set up an xUnit v3 test project for a CodeBrix Platform application](../BLUEPRINTS.md#set-up-an-xunit-v3-test-project-for-a-codebrix-platform-application),
[Make data dependent tests explain themselves when the data is missing](../BLUEPRINTS.md#make-data-dependent-tests-explain-themselves-when-the-data-is-missing),
[Serialize a test assembly that touches process static state](../BLUEPRINTS.md#serialize-a-test-assembly-that-touches-process-static-state),
[Test a hardware input path behind the engine adapter interfaces](../BLUEPRINTS.md#test-a-hardware-input-path-behind-the-engine-adapter-interfaces)
and [Parse original binary data formats into a testable library](../BLUEPRINTS.md#parse-original-binary-data-formats-into-a-testable-library).

## Third-party content

[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) in this folder is the
application's attribution record. It covers the adapted managed-doom engine
vendored under `src/libs/Doom.Brix.GameEngine`, whose license text is
[LICENSE_ManagedDoom.txt](LICENSE_ManagedDoom.txt); the MeltySynth test suite the
SoundFont regression tests were adapted from, whose notice is reproduced inline;
the bundled TimGM6mb SoundFont by Tim Brechbill that ships for music playback,
whose license text is [LICENSE_TimGM6mb.txt](LICENSE_TimGM6mb.txt); and Polyphone,
named as the tool that produced the committed SoundFont-parameter reference data.
The notices file and both license texts are `Content` items in `Doom.Brix.Game`,
so a distributed build carries its own compliance set in a `ThirdPartyAssets`
folder beside the SoundFont they cover. The game data is not third-party content
that ships here at all: no game data is distributed with the application, and the
user supplies `DOOM1.WAD` through Assets Mode under id Software's original
shareware terms.

## License

Doom.Brix is free software, licensed under the GNU General Public License,
version 2, see [../LICENSE](../LICENSE).

Copyright (c) 2026 Jeremy Ellis and contributors
