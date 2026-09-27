# Wolfenstein.Brix

Wolfenstein.Brix is a playable recreation of id Software's 1992 Wolfenstein 3D
shareware episode, running as a CodeBrix.Platform XAML application on all six
desktop heads. It ships no game data. On first launch it opens **Assets Mode**:
you choose a folder to keep the game files in, an embedded browser opens the page
the shareware release can be downloaded from, and the one download the policy
permits is verified against known checksums and unpacked into that folder. Every
later launch re-verifies the folder and, when it still holds the authentic files,
goes straight to **Game Mode** — a single game canvas filling the window, with the
original title screen, menus, difficulty select, gameplay, intermissions, high
scores and eight save slots. Configuration, saves and high scores all live in the
application's single portable `settings.sqlite`, so the game writes no files of
its own outside the folder you chose.

The application is a reference for two things at once. It hosts a fixed-rate game
loop and a software framebuffer presenter inside a CodeBrix.Platform page, with
keyboard, gamepad and audio backends wired to the engine. And it wraps that in
ordinary CodeBrix.Platform application work in the MVVM shape: a two-mode page
driven entirely by bound visibility, a WebView-backed setup mode whose download
policy lives in the view model, a multi-stage background pipeline reporting to a
bound overlay, and one settings store reached through a small application-named
facade.

## What this sample shows a CodeBrix.Platform developer

- How a game loop that must advance at its own fixed rate lives inside a XAML
  page: the page declares the engine's game surface canvas, the view model owns
  the host, and the host overrides the engine's lifecycle hooks —
  [Host a fixed rate game loop inside a XAML page](../BLUEPRINTS.md#host-a-fixed-rate-game-loop-inside-a-xaml-page).
- How a self-composed pixel buffer at a fixed resolution is scaled and
  letterboxed into whatever window size a head gives you —
  [Present a software framebuffer through the game surface canvas](../BLUEPRINTS.md#present-a-software-framebuffer-through-the-game-surface-canvas).
- How an expensive object is constructed only once two independent prerequisites
  have both arrived, in either order —
  [Boot an expensive object only when both prerequisites have arrived](../BLUEPRINTS.md#boot-an-expensive-object-only-when-both-prerequisites-have-arrived).
- How the whole engine parks when the window is minimized and resumes exactly
  where it left off, wired once at application level —
  [Pause a game engine when the window is minimized](../BLUEPRINTS.md#pause-a-game-engine-when-the-window-is-minimized).
- How a focusable rendering surface is kept in possession of the keyboard across
  clicks, first start and window reactivation —
  [Keep keyboard focus on a game canvas](../BLUEPRINTS.md#keep-keyboard-focus-on-a-game-canvas).
- How held-key state for continuous movement and one-shot edge events for menus
  and typing are both sampled once per tic —
  [Pump keyboard events and held key state into a game loop](../BLUEPRINTS.md#pump-keyboard-events-and-held-key-state-into-a-game-loop).
- How controller support is made always-on and completely inert when no
  controller and no SDL2 are present —
  [Add optional gamepad support that degrades to keyboard only](../BLUEPRINTS.md#add-optional-gamepad-support-that-degrades-to-keyboard-only).
- How a per-tic controller sample survives a controller sleeping and waking, with
  button edges and a menu repeat clock —
  [Sample a gamepad once per tic without losing a sleeping controller](../BLUEPRINTS.md#sample-a-gamepad-once-per-tic-without-losing-a-sleeping-controller).
- How continuously synthesized audio is pulled through a single engine voice
  while game state is followed from the loop, not the audio thread —
  [Stream synthesized audio through one engine voice](../BLUEPRINTS.md#stream-synthesized-audio-through-one-engine-voice).
- How many short sound effects are registered once and replayed on a bounded pool
  of channels with idle-first, round-robin-steal selection —
  [Play short PCM clips on a pool of engine sound channels](../BLUEPRINTS.md#play-short-pcm-clips-on-a-pool-of-engine-sound-channels).
- How one page becomes two full-screen experiences plus a modal overlay, with no
  navigation and no second page type —
  [Switch a page between two full screen modes with visibility bindings](../BLUEPRINTS.md#switch-a-page-between-two-full-screen-modes-with-visibility-bindings).
- How view-model commands navigate a WebView the page owns, and how the view
  model learns which page the user ended up on —
  [Drive an embedded browser from the view model](../BLUEPRINTS.md#drive-an-embedded-browser-from-the-view-model).
- How a browser the user drives freely is held to exactly one permitted download,
  with everything else refused and explained —
  [Enforce a one file download policy from the view model](../BLUEPRINTS.md#enforce-a-one-file-download-policy-from-the-view-model).
- How a download-verify-extract operation reports a stage caption and a
  percentage to a bound overlay without blocking the UI thread —
  [Report multi stage background progress to a bound overlay](../BLUEPRINTS.md#report-multi-stage-background-progress-to-a-bound-overlay).
- How content fetched from an untrusted mirror is proven to be exactly the
  expected file before anything acts on it —
  [Verify a downloaded file against known checksums](../BLUEPRINTS.md#verify-a-downloaded-file-against-known-checksums).
- How an old installer format — a zip whose entry is itself a catalog of
  DCL-imploded members — is walked and partially extracted safely —
  [Unpack a legacy DCL compressed archive safely](../BLUEPRINTS.md#unpack-a-legacy-dcl-compressed-archive-safely).
- How the application fetches a file itself, with progress and headers that get
  past a mirror's hotlink checks —
  [Download a file with progress and mirror friendly request headers](../BLUEPRINTS.md#download-a-file-with-progress-and-mirror-friendly-request-headers).
- How documented-by-reverse-engineering binary formats are parsed into a library
  that can be verified independently of the application —
  [Parse original binary data formats into a testable library](../BLUEPRINTS.md#parse-original-binary-data-formats-into-a-testable-library).
- How the whole application reads and writes settings through one short,
  application-named type instead of calling the add-in directly —
  [Wrap the AppSettings add-in in an application named facade](../BLUEPRINTS.md#wrap-the-appsettings-add-in-in-an-application-named-facade).
- How a one-time "where does the data live" choice is made, persisted, and
  re-validated on every launch —
  [Let the user pick a folder and remember the choice](../BLUEPRINTS.md#let-the-user-pick-a-folder-and-remember-the-choice).
- How a self-contained subsystem persists state through one interface it declares
  itself, with a working in-memory default —
  [Persist a subsystem behind one storage interface](../BLUEPRINTS.md#persist-a-subsystem-behind-one-storage-interface).
- How the view model tells the user something without knowing anything about the
  visual tree —
  [Show an alert dialog from a view model](../BLUEPRINTS.md#show-an-alert-dialog-from-a-view-model).
- How Enter in a text box runs the same command the adjacent button is bound to —
  [Execute a view model command when Enter is pressed in a text box](../BLUEPRINTS.md#execute-a-view-model-command-when-enter-is-pressed-in-a-text-box).
- How one shared UI and one shared Core library run on all six desktop heads —
  [Start one application from six head projects](../BLUEPRINTS.md#start-one-application-from-six-head-projects).
- How the startup sequence is ordered: service resolver, design-mode flag,
  default font, settings store, logging —
  [Bootstrap the application in App xaml cs](../BLUEPRINTS.md#bootstrap-the-application-in-app-xaml-cs).
- How `App.xaml` and the views are file-linked into every head through a shared
  project rather than compiled once —
  [Share App xaml and views through a shared shproj UI project](../BLUEPRINTS.md#share-app-xaml-and-views-through-a-shared-shproj-ui-project).
- How the WinWpfSkia head keeps high-rate paints from starving keyboard delivery,
  a failure that is total rather than gradual —
  [Keep WPF paints from starving keyboard input](../BLUEPRINTS.md#keep-wpf-paints-from-starving-keyboard-input).
- How every shared package sits in one Core library while each head declares
  exactly one platform runtime package —
  [Carry every shared package in one Core library and one runtime package per head](../BLUEPRINTS.md#carry-every-shared-package-in-one-core-library-and-one-runtime-package-per-head).
- Which projects need the platform's `RootNamespace` and conditional-compilation
  defines, and which must deliberately take a different one —
  [Set the RootNamespace and conditional compilation defines CodeBrix Platform needs](../BLUEPRINTS.md#set-the-rootnamespace-and-conditional-compilation-defines-codebrix-platform-needs).
- How license texts and the notices file ride into every head's build output so a
  distributed build carries its own compliance set —
  [Ship bundled assets and license notices into every head output](../BLUEPRINTS.md#ship-bundled-assets-and-license-notices-into-every-head-output).
- How a translated emulator keeps its upstream provenance and its deliberate
  deviations reviewable against the original —
  [Port an emulator or codec and keep it reviewable against the original](../BLUEPRINTS.md#port-an-emulator-or-codec-and-keep-it-reviewable-against-the-original).
- How each library gets its test project, and what the family's xUnit v3 setup
  looks like —
  [Set up an xUnit v3 test project for a CodeBrix Platform application](../BLUEPRINTS.md#set-up-an-xunit-v3-test-project-for-a-codebrix-platform-application).
- How tests that need large uncommittable data files either fail with actionable
  instructions or skip cleanly, never mysteriously —
  [Make data dependent tests explain themselves when the data is missing](../BLUEPRINTS.md#make-data-dependent-tests-explain-themselves-when-the-data-is-missing).
- How an input path that depends on a device you cannot attach on a build machine
  is still covered, by writing it against the engine's interfaces —
  [Test a hardware input path behind the engine adapter interfaces](../BLUEPRINTS.md#test-a-hardware-input-path-behind-the-engine-adapter-interfaces).

## Building, running and testing

There is one solution, `Wolfenstein.Brix/Wolfenstein.Brix.slnx`. It holds the
shared UI project, the Core library, all six heads, a `Libraries` folder for the
projects under `src/libs`, and a `Tests` folder for the projects under
`tests/libs`. It restores and builds with the plain .NET SDK on Linux, macOS and
Windows, so there is nothing to open elsewhere.

The heads:

| Head project | Platform |
| --- | --- |
| `Wolfenstein.Brix/src/Wolfenstein.Brix.LinuxX11` | Linux X11 |
| `Wolfenstein.Brix/src/Wolfenstein.Brix.LinuxWayland` | Linux Wayland |
| `Wolfenstein.Brix/src/Wolfenstein.Brix.LinuxFrameBuffer` | Linux framebuffer |
| `Wolfenstein.Brix/src/Wolfenstein.Brix.MacOS` | macOS |
| `Wolfenstein.Brix/src/Wolfenstein.Brix.Win32Skia` | Windows (Win32) |
| `Wolfenstein.Brix/src/Wolfenstein.Brix.WinWpfSkia` | Windows (WPF) |

Every project targets `net10.0`; the WinWpfSkia head targets `net10.0-windows`
and sets `EnableWindowsTargeting`, which is what lets the whole solution restore
and build on Linux and macOS too. There are no native heads here — every head is
a Skia head of CodeBrix.Platform.

Prerequisites:

- The .NET 10 SDK. Nothing else is required to build.
- Game data you supply. The application ships none: the shareware `.WL1` data
  files are downloaded, verified and installed by Assets Mode into a folder you
  choose. No account, token or API key is involved.
- The embedded browser. On Windows and macOS the platform runtime has one built
  in; on the Linux heads it comes from the CodeBrix.Platform.WebView add-in.
- Optional, at run time on Linux: the system SDL2 runtime library, which on a
  Debian-family system is an `apt` package (the exact name is in the comment in
  `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/Wolfenstein.Brix.Game.csproj`).
  It is used only for game controllers, and only the game-controller subsystem is
  started, so it opens no display and contends with nothing on any head. When it
  is missing, the gamepad manager reports itself unavailable, says why in the log,
  and the game runs on with the keyboard.
- No GPU is needed. The game composes its frames in software.

To build the solution and run one head, from the repository root:

```text
dotnet build Wolfenstein.Brix/Wolfenstein.Brix.slnx
dotnet run --project Wolfenstein.Brix/src/Wolfenstein.Brix.LinuxX11
```

`Wolfenstein.Brix/global.json` selects the Microsoft.Testing.Platform test
runner, and every test project builds as an executable with
`UseMicrosoftTestingPlatformRunner`, because xUnit v3 test projects are
self-executing binaries. On some SDK builds a solution-level `dotnet test` will
report that zero tests ran. The way that always works is to build and then run
each test project's own executable:

```text
dotnet build Wolfenstein.Brix/Wolfenstein.Brix.slnx
Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.GameEngine.Tests/bin/Debug/net10.0/Wolfenstein.Brix.GameEngine.Tests
```

The tests need no GPU and no network. The data-dependent tests do need the
shareware files on disk, and the two projects that want data take deliberately
different routes.
`Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.GameEngine.Tests/TestWl1.cs` walks
up from the test binary looking for `Downloaded/Wolfenstein.Brix_assets/` and,
when it is absent, throws with a message naming the data files, the folder to put
them in, and the fact that the application's Assets Mode can download and install
them — so those tests fail loudly with instructions rather than silently.
`Wolfenstein.Brix.Assets.Tests` looks for the downloaded archive under
`Downloaded/wolfenstein_assets/` and calls `Assert.SkipWhen(...)`, so its
pipeline tests skip quietly instead. The `Downloaded` folder is git-ignored. The
pure math, decompression, classifier and gamepad tests need no data at all and
always run.

## How the projects and folders are organized

```text
Wolfenstein.Brix/
  Wolfenstein.Brix.slnx                  The single solution: shared UI, Core, six heads, libs, tests
  global.json                            Selects the Microsoft.Testing.Platform test runner
  THIRD-PARTY-NOTICES.txt                The application's third-party attribution record
  LICENSE_CSharpWolfenstein.txt          License text for the adapted raycaster and map decoding
  LICENSE_Wolf3D-iOS.txt                 License text for id's Wolf3D iOS source release
  LICENSE_NukedOPL3.txt                  License text for the OPL3 emulator port
  src/
    Wolfenstein.Brix.UI/                 Shared project: App.xaml(.cs) and Views/MainPage.xaml(.cs),
                                         file-linked into every head
    Wolfenstein.Brix.Core/               Class library carrying every shared package;
                                         ViewModels/MainViewModel.cs and Helpers/HostHelper.cs
    Wolfenstein.Brix.LinuxX11/           Head: Program.cs and one platform runtime package
    Wolfenstein.Brix.LinuxWayland/       Head: Program.cs and one platform runtime package
    Wolfenstein.Brix.LinuxFrameBuffer/   Head: Program.cs and one platform runtime package
    Wolfenstein.Brix.MacOS/              Head: Program.cs and one platform runtime package
    Wolfenstein.Brix.Win32Skia/          Head: Program.cs and one platform runtime package
    Wolfenstein.Brix.WinWpfSkia/         Head: as above, plus the WPF dispatcher-scheduling setup
    libs/
      Wolfenstein.Brix.Assets/           Asset ACQUISITION: the known-good catalog, the download
                                         classifier, the HTTP downloader, the checksum helper and
                                         the download/verify/extract pipeline
      Wolfenstein.Brix.GameEngine/       The game itself, with no dependencies at all:
                                           Assets/     the .WL1 format parsers and decompression
                                           Logic/      session, actors, player, doors, saves,
                                                       IWolfStorage
                                           Rendering/  raycaster, wall/sprite renderers, frame composer
                                           Audio/      the OPL3 chip port and the AdLib/IMF driver
      Wolfenstein.Brix.Game/             The host layer: WolfGameHost plus the video, sound, music,
                                         keyboard and gamepad backends, and SqliteWolfStorage
      Wolfenstein.Brix.Settings/         The application-named settings facade: SettingsService
                                         and LoggingService
  tests/
    libs/
      Wolfenstein.Brix.Assets.Tests/     Classifier and pipeline tests; skip without the archive
      Wolfenstein.Brix.GameEngine.Tests/ Decompression, palette, parsing, rendering, session and
                                         OPL tests; the data-dependent ones fail with instructions
      Wolfenstein.Brix.Game.Tests/       Gamepad sampling, driven through fake adapter interfaces
      Wolfenstein.Brix.Settings.Tests/   The settings store the facade wraps
```

Dependencies run one way. Each head project references `Wolfenstein.Brix.Core`
and exactly one CodeBrix.Platform runtime package, and *file-links* the shared UI
by importing `Wolfenstein.Brix.UI.projitems`, so `App.xaml`, `MainPage.xaml` and
their code-behind compile into every head rather than into a library of their
own. `Wolfenstein.Brix.Core` carries every other package — CodeBrix.Platform, the
font package, the WebView add-in, the generic host and console logging — and
project-references the libraries under `src/libs`.

Inside `src/libs` the direction is `Wolfenstein.Brix.Game` to
`Wolfenstein.Brix.GameEngine` and `Wolfenstein.Brix.Game` to
`Wolfenstein.Brix.Settings`. `Wolfenstein.Brix.GameEngine` references nothing at
all: no packages, no projects, just managed game code, which is what makes it
testable headless. `Wolfenstein.Brix.Assets` references only CodeBrix.Compression.
Only `Wolfenstein.Brix.Game` touches the GameEngine add-in and its SDL2 companion,
so the engine add-in never leaks into the game logic or the acquisition pipeline.
Each test project references exactly the one library it tests.

## CodeBrix libraries and add-ins used

| Library or add-in | What it does in this application | Where |
| --- | --- | --- |
| CodeBrix.Platform | The XAML application framework: `Application`, `Window`, `Frame`, `Page`, the Simple MVVM toolkit (`SimpleViewModel`, `SimpleCommand`, `SimpleServiceResolver`, `IXamlRootGetter`), the folder picker and the theme resources. | `Wolfenstein.Brix/src/Wolfenstein.Brix.Core/Wolfenstein.Brix.Core.csproj`, `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml.cs`, `Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs` |
| CodeBrix.Platform runtime for the head | One runtime package per head project, and only one; every other package comes from Core. | The six head csprojs under `Wolfenstein.Brix/src/` |
| CodeBrix.Platform Open Sans font | The application font: set as the default text font family in `App()` and exposed as the `OpenSansFont` resource in `App.xaml`. | `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml`, `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml.cs` |
| CodeBrix.Platform.GameEngine add-in | The fixed-rate game loop and its host base class, the software framebuffer presenter, the game surface canvas, the keyboard poller and adapter, and the audio system with its streaming sources and sound channels. | `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGameHost.cs`, `WolfVideo.cs`, `WolfUserInput.cs`, `WolfSound.cs`, `WolfMusic.cs` |
| CodeBrix.Platform.GameEngine.Sdl2 add-on | Optional SDL2-backed game controller support, initialized from the host's `ConfigureGamepads()` override and sampled once per tic. | `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGameHost.cs`, `WolfGamepadInput.cs` |
| CodeBrix.Platform.WebView add-in | The embedded browser for Assets Mode, used through the `WebView2` XAML element and `CoreWebView2`; on the Linux heads it is what supplies a WebView at all. | `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml`, `MainPage.xaml.cs` |
| CodeBrix.Platform.AppSettings add-in | The whole settings machinery behind the facade: one portable `settings.sqlite` store with typed properties, change events, startup auto-backup and pruning, corrupt-file quarantine and import/export, plus its logging service. | `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Settings/SettingsService.cs`, `LoggingService.cs` |
| CodeBrix.Compression | CRC-32 for checksum verification, zip reading for the outer archive, and DCL "implode" decompression for the installer archive inside it. | `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/WolfensteinAssetPipeline.cs`, `Internal/ChecksumHelper.cs` |
| CodeBrix.Audio | Reached through the GameEngine add-in; the sound backend uses its playback-state enumeration to find an idle channel. | `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfSound.cs` |
| CodeBrix.Sqlite | Reached through the AppSettings add-in as the settings store's backing; the settings tests reference it directly. | `Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.Settings.Tests/SettingsStoreTests.cs` |
| SilverAssertions | The assertion style in every test project. | `Wolfenstein.Brix/tests/libs/` |

Third-party libraries:

| Library | What it does in this application | Where |
| --- | --- | --- |
| Microsoft.Extensions.Hosting | The generic host builder `SimpleServiceResolver` builds its container from. | `Wolfenstein.Brix/src/Wolfenstein.Brix.Core/Helpers/HostHelper.cs` |
| Microsoft.Extensions.Logging (console) | Debug-only console logging, wired from each head's `Main` before the host is built. | `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml.cs` |
| xUnit v3 | The test framework, run through Microsoft.Testing.Platform. | `Wolfenstein.Brix/tests/libs/` |

## Worth studying in this application

### One page, two modes, and the decision made at launch

`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml` is the entire
visual tree: three sibling grids inside one parent grid. The first is Assets
Mode, the second is Game Mode with the game surface canvas in it, the third is
the download progress overlay. There is no navigation and no second page type.
The view model keeps one private mode flag and every visible region binds to a
derived `Visibility` property computed with `SimpleViewModel.GetVisibility(bool)`,
so no bool-to-visibility converter appears anywhere. Because the derived
properties are computed rather than stored, they need explicit change
notification; `MainViewModel` groups all of them into a single
`NotifyModeProperties()` so they cannot drift apart. Document order puts the
overlay on top with no z-index work.

Read `MainPage.xaml` first, then the bindable-properties region of
`Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs`.

Which mode the application opens in is decided in that same view model's
constructor, before any UI renders. It reads the remembered assets folder from
the settings facade, drops it if the folder no longer exists, and then re-verifies
the folder's contents against the catalog's known-good hashes before taking the
fast path into Game Mode. A
remembered path is never trusted on its own: existence is checked, then content
is re-verified, and a corrupt or emptied folder simply sends the user back to
setup rather than crashing. `IsDesignMode(true)` is the first line of the
constructor, which is what keeps the XAML designer from executing any of it.

That ordering only works because `App`'s constructor opens the settings store
before `InitializeComponent()`, and the page declares its view model in XAML —
so the store is open before the first view model constructor runs. See
[Switch a page between two full screen modes with visibility bindings](../BLUEPRINTS.md#switch-a-page-between-two-full-screen-modes-with-visibility-bindings),
[Let the user pick a folder and remember the choice](../BLUEPRINTS.md#let-the-user-pick-a-folder-and-remember-the-choice)
and
[Bootstrap the application in App xaml cs](../BLUEPRINTS.md#bootstrap-the-application-in-app-xaml-cs).

### Booting the game host when both prerequisites have arrived

Two independent things must happen before `WolfGameHost` can be constructed: the
game canvas must have started with a real size, and Game Mode must be active with
a verified assets folder to use as the host's data directory. Either can happen
first. The view model owns both the check and the host field:
`CanvasFirstStart(canvas)` records the canvas, the `IsGameMode` setter records the
mode, and both call the same private `StartGameIfReady()`, which returns early
unless every prerequisite holds and the host does not already exist.

The page's contribution is one handler. `GameCanvas.FirstStarted` hands the canvas
to the view model and then focuses it — and because the canvas lives inside the
collapsed Game Mode grid, `FirstStarted` fires exactly when Game Mode first
becomes visible, which is the ordering the whole design leans on. The other
direction matters too: the host raises `GameExited` from the game-loop thread when
the player picks Quit, and the view model hops to the UI thread through the
canvas's `DispatcherQueue` before closing the application.

Read `MainPage.xaml.cs`'s constructor, then the game-hosting region of
`MainViewModel.cs`. See
[Boot an expensive object only when both prerequisites have arrived](../BLUEPRINTS.md#boot-an-expensive-object-only-when-both-prerequisites-have-arrived)
and
[Host a fixed rate game loop inside a XAML page](../BLUEPRINTS.md#host-a-fixed-rate-game-loop-inside-a-xaml-page).

### The host: one class, six lifecycle overrides, one frame per tic

`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGameHost.cs` is the whole
seam between the application and the game. It derives from the engine's
software-rendered host base class, hands the base constructor the game's own tic
rate as a plain constant (the original's 70 Hz, from
`WolfLogic.TicsPerSecond`), and overrides the hooks: `ConfigureGamepads()`,
`ConfigureAudio()`, `OnLoadContent()`, `OnTic()`, `OnRenderFrame(...)` and
`OnShutdown()`. Nothing about the head or the window appears in it, so the same
loop runs at the same rate on all six heads with no per-head configuration.

The ordering inside those overrides is the part worth copying.
`ConfigureAudio()` pins the shared audio device before any sound channel exists.
`OnLoadContent()` is where the backends are constructed, because that is the first
point at which the engine has wired its input adapters — construct the keyboard
reader earlier and it throws with that instruction. `OnShutdown()` detaches the
engine's gamepad manager property *before* disposing the manager, because the
engine's input poll reaches the manager through that property and must not find a
disposed one there.

`OnTic()` is deliberately tiny: drain a pending focus-loss flag, advance the
session with one input struct, follow the sound and music volumes. `OnRenderFrame`
is one line. Everything else lives in the backends beside it.

`WolfVideo.cs` is where that one line lands. It owns the renderer and configures
the host's presenter exactly once, in its constructor: the game's native 320x200,
row-major RGBA, fit-scaled and letterboxed, with filtering off so the
nearest-neighbor look survives. Per frame
it does one thing — cast the `Span<byte>` the engine hands it to a span of pixels
with `MemoryMarshal.Cast<byte, uint>` and let the frame composer write whole
pixels into it, with no copy. None of this reaches the view model; the presenter
is a host concern and stays one.

The composer itself is in
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Rendering/FrameComposer.cs`,
where the screen dimensions are declared. See
[Host a fixed rate game loop inside a XAML page](../BLUEPRINTS.md#host-a-fixed-rate-game-loop-inside-a-xaml-page)
and
[Present a software framebuffer through the game surface canvas](../BLUEPRINTS.md#present-a-software-framebuffer-through-the-game-surface-canvas).

### Keyboard focus, and the two pauses

Keys reach the game only while the game surface holds keyboard focus, and three
separate paths take focus away: first start, every click on the canvas, and the
window being deactivated and activated again. `MainPage.xaml.cs` repairs the
first two with one-liners that funnel into `FocusGameCanvas()`, which defers
through the dispatcher so focus lands after whatever took it has finished
processing; the pointer handler is registered with `handledEventsToo: true`,
because the press may already be marked handled. The third repair is the
engine's: `GameWindowLifecycle`, attached to the window in
`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml.cs`, hands focus back to the
canvas of every running game host whenever the window is activated. The host
exists only from Game Mode on, so in Assets Mode the embedded browser keeps the
keyboard and typing in it is never broken.

Two different pauses live nearby and should not be conflated. The canvas's
`LostFocus` is forwarded to the host, and the game's own logic pauses gameplay
into its menu on the next tic. Separately, the same `GameWindowLifecycle` call
pauses the engine when the window is minimized and resumes it when the window is
shown again, so minimizing parks the entire loop — including the music stream —
and restoring resumes with the gap invisible to game time. A pause arriving
before the host initializes simply starts the loop parked, so no ordering guard is
needed, and the helper resumes only a pause it made itself. Workspace switches
deliberately do not pause.

See
[Keep keyboard focus on a game canvas](../BLUEPRINTS.md#keep-keyboard-focus-on-a-game-canvas)
and
[Pause a game engine when the window is minimized](../BLUEPRINTS.md#pause-a-game-engine-when-the-window-is-minimized).

### Input: held state, edges, and a controller that costs nothing

`WolfUserInput.cs` states its own rule in its header: key *events* — menus, name
typing — queue up from the engine's keyboard poller, while held-key *state* for
movement polls the lock-free adapter. Both the poller callback and the per-tic
drain run on the game-loop thread, so the edge flags need no synchronization, and
building the input clears them, so an edge is consumed exactly once.
`StartMonitoringAllKeys()` has to be called or held-key queries stay false.

The controller path folds into the same struct. `WolfGamepadInput.cs` polls the
adapter list every tic rather than registering for button events, because the
engine's registrations are keyed by gamepad id and a controller that sleeps and
wakes comes back with a new one. It clamps a stick vector back onto the unit
circle — X and Y are clamped independently, so a corner-held stick legitimately
reads above magnitude one and would otherwise become a diagonal speed boost — and
uses a firmer threshold for menu navigation than for movement. With no controller
connected the sample is `default` and every fold-in downstream is a no-op, so the
keyboard path carries no branches for it.

Availability reporting is worth a look in `WolfGameHost.LogGamepadAvailability()`:
the SDL2 add-on's own status logging goes out at Information level, which the
host's default warning filter drops, so a *working* controller would log nothing
at all. The host suppresses that and reports through the application's own logging
facade instead — including each device's mapping string, which varies by transport
and is what reconciles raw button numbering with the standard layout.

See
[Pump keyboard events and held key state into a game loop](../BLUEPRINTS.md#pump-keyboard-events-and-held-key-state-into-a-game-loop),
[Add optional gamepad support that degrades to keyboard only](../BLUEPRINTS.md#add-optional-gamepad-support-that-degrades-to-keyboard-only)
and
[Sample a gamepad once per tic without losing a sleeping controller](../BLUEPRINTS.md#sample-a-gamepad-once-per-tic-without-losing-a-sleeping-controller).

### Two kinds of audio through one engine

The game needs both a continuously generated source and a lot of short clips, and
the two backends are shaped quite differently.

`WolfMusic.cs` owns the OPL synthesizer and one pull-model streaming source built
from the synthesizer's fill method. That fill callback runs on the audio thread
and its doc comment states the contract: fast and allocation-free. Control calls
— play, stop, volume — come from the game-loop thread, and a short lock hands
state across. `Update()` is called every tic but compares the requested track
against the current one and returns immediately when they match, so calling it
that often is free.

`WolfSound.cs` takes the other shape: every digitized sound is registered once
with the engine's audio resource manager at load time, straight from the raw PCM
in the game data with its own sample rate and bit depth (rate conversion is built
into the channel, so no resampling step of your own is needed). Playback runs on a
fixed pool of channels, picking an idle one and otherwise stealing round-robin,
and each channel remembers which clip it currently holds so an unchanged clip is
not re-set. Every channel is disposed from the backend's `Dispose()`, which the
host calls from `OnShutdown()`.

See
[Stream synthesized audio through one engine voice](../BLUEPRINTS.md#stream-synthesized-audio-through-one-engine-voice)
and
[Play short PCM clips on a pool of engine sound channels](../BLUEPRINTS.md#play-short-pcm-clips-on-a-pool-of-engine-sound-channels).

### Assets Mode: browse freely, download exactly one thing

Assets Mode is the most instructive MVVM material here, because the temptation to
put it in code-behind is strong and the sample resists it. The *policy* lives in
`MainViewModel`: `HandleDownloadStarting(url, suggestedFileName, out targetFilePath)`
answers "is this the file we want, and where should it go", and three short
methods take progress, completion and failure. The page subscribes to the
WebView's download event and forwards to them, holding no policy of its own; it
cancels the download when the view model says no. Only the download event is
hooked, never navigation, so the user can browse anywhere.

The classifier in
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/AssetUrlClassifier.cs`
recognizes the wanted file two ways — by the download's suggested file name, with
the browser's " (N)" collision suffix stripped, or by the file name in the URL
path — because a mirror may serve it under a URL that does not show its name. The
accepted download is redirected to a temp path the application controls, and is
verified before anything trusts it.

Navigation runs the other way through a bridge the page supplies. The view model
exposes a settable navigation delegate, a `GoCommand`, and a method the page calls
once the bridge is wired; `EnsureBrowserStarted()` returns early when no bridge
has been set, which is the graceful-degradation path. The page reads
`CoreWebView2.Source` for the authoritative current URL after redirects, because
the XAML `Source` property does not reliably reflect those, and skips WebView
initialization entirely in Game Mode — both to save the startup cost and to keep
the WebView's native focus proxy from stealing keyboard focus from the game
canvas.

Read `MainPage.xaml.cs`'s `InitializeBrowser()` alongside the browser-bridge and
download-policy regions of `MainViewModel.cs`. See
[Enforce a one file download policy from the view model](../BLUEPRINTS.md#enforce-a-one-file-download-policy-from-the-view-model),
[Drive an embedded browser from the view model](../BLUEPRINTS.md#drive-an-embedded-browser-from-the-view-model)
and
[Execute a view model command when Enter is pressed in a text box](../BLUEPRINTS.md#execute-a-view-model-command-when-enter-is-pressed-in-a-text-box).

### Verifying and unpacking what came back

`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/` is a UI-free library, and the
view model never touches a stream or computes a hash — it calls the pipeline,
watches an `IProgress<T>`, and reacts to a typed exception that carries the failed
stage so one catch block can pick the right wording for the user.

`WolfensteinAssetCatalog.cs` holds the known-good facts as constants: the one
permitted file name, its size, CRC-32 and MD5, the installer archive inside it,
the default page to browse to, and a dictionary of every expected extracted file
with its own size and hash. `Internal/ChecksumHelper.cs` computes size, CRC-32 and
MD5 together in a single streaming pass over one shared buffer, which is possible
because CodeBrix.Compression's CRC-32 is incremental and pairs with
`IncrementalHash`.

`WolfensteinAssetPipeline.cs` is where the interesting handling is. It pushes the
hashing and decompression onto a background task so they never run on the caller's
thread, wraps a per-stage adapter around the progress reporter for sub-operations
that only know a fraction, then reads the zip entry with `ReadExactly` into a
buffer sized from the declared size and confirms the stream is exhausted — an
entry *larger* than declared is caught that way. Walking the installer archive
inside it, members that are not wanted are skipped by their compressed size
without ever being decompressed, so the archive's DOS executables never touch
disk; every member name is validated before being joined to an output folder; a
fixed-size name field is cut at the first NUL because what follows the terminator
may be uninitialized garbage; and the walk must consume the archive to its exact
final byte and have found every expected member, with either shortfall treated as
a failure. Structural signatures add a layer the hashes cannot, since the inner
compression format carries no checksum of its own. The launch-time re-verification
method deliberately swallows every exception and returns false, so a missing or
unreadable file is a "not installed" answer rather than a crash.

`AssetDownloader.cs` is the path used when the application fetches a file itself
rather than through the browser: a shared `HttpClient` with an infinite timeout
(cancellation governs, not a timeout), `HttpCompletionOption.ResponseHeadersRead`
so progress is reportable at all, a fallback total for servers that declare no
content length, and request headers that present the request the way the embedded
browser the user clicked in would.

See
[Verify a downloaded file against known checksums](../BLUEPRINTS.md#verify-a-downloaded-file-against-known-checksums),
[Unpack a legacy DCL compressed archive safely](../BLUEPRINTS.md#unpack-a-legacy-dcl-compressed-archive-safely),
[Download a file with progress and mirror friendly request headers](../BLUEPRINTS.md#download-a-file-with-progress-and-mirror-friendly-request-headers),
[Report multi stage background progress to a bound overlay](../BLUEPRINTS.md#report-multi-stage-background-progress-to-a-bound-overlay)
and
[Show an alert dialog from a view model](../BLUEPRINTS.md#show-an-alert-dialog-from-a-view-model).

### Everything persists through one store, reached two ways

The application has exactly one persistence story and two front doors onto it.
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Settings/SettingsService.cs` is a
static, application-named facade over the AppSettings add-in: the application name
as a constant, one forwarding member per operation, plus a directory-taking
`Initialize(...)` overload and a `Shutdown()` that exist specifically so test hosts
can point at a throwaway folder and re-initialize. `LoggingService.cs` does the
same for the add-in's logging service, and is what the game host's gamepad
reporting goes through.

The game reaches the same store through a seam it declares itself.
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Logic/IWolfStorage.cs`
names everything the game needs to load and save — config text, the eight save
slots, the high-score table — and declares the slot count on the interface so
there is no second source of truth. It ships an in-memory implementation as the
default, which is what makes headless testing possible.
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/SqliteWolfStorage.cs` is the real
one, and it talks only to the settings facade, never to a sqlite handle of its
own: binary save payloads go in base64-encoded, with two keys per slot and the
data key as the presence test, so the description key alone can never conjure a
phantom slot. The result is that the whole of the application's state — the
assets-folder choice, the game config, saves and high scores — rides the add-in's
auto-backup, pruning and corrupt-file quarantine for free. The view model injects
the implementation in exactly one place, where it constructs the host.

See
[Wrap the AppSettings add-in in an application named facade](../BLUEPRINTS.md#wrap-the-appsettings-add-in-in-an-application-named-facade)
and
[Persist a subsystem behind one storage interface](../BLUEPRINTS.md#persist-a-subsystem-behind-one-storage-interface).

### The game data formats, and the ported emulator

`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Assets/` reads the original
binary formats: one parser class per format, each with a static `Load(...)` factory
and a private constructor, gathered behind `WolfAssets.Load(assetsFolderPath)`
which loads the whole set and reports a clear error naming the missing file *and*
the part of the application that obtains it. Everything is parsed into memory at
startup and nothing derived is written back to disk.

The structural decision worth copying is in `Compression.cs`: the decompression
schemes the formats need are separated from the parsers themselves, so they can be
covered by golden tests built on tiny hand-worked synthetic streams and only the
parsers then need real data. A dedicated exception type for malformed data keeps
"this file is wrong" distinguishable from "the code is wrong". `WolfAssets.cs` also
carries a searchable comment marker at the exact place a future change to support
the registered data files would start — a forward-looking limitation recorded in
the code rather than in a document that will drift.

The audio side of the same library takes the other route into original data.
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Audio/NukedOpl3.cs` is a
hand-port of a reference C implementation rather than a parser, and its header is
the model to copy: the upstream project, the exact upstream files, the license,
what was deliberately omitted, and a "Porting notes" block listing every deviation
from the original's shape — including that field and table names deliberately keep
the C source's snake_case, so a reviewer diffs the two side by side rather than
"fixing" them.

See
[Parse original binary data formats into a testable library](../BLUEPRINTS.md#parse-original-binary-data-formats-into-a-testable-library)
and
[Port an emulator or codec and keep it reviewable against the original](../BLUEPRINTS.md#port-an-emulator-or-codec-and-keep-it-reviewable-against-the-original).

### How the tests are arranged

Each library under `src/libs` has exactly one peer under `tests/libs` named after
it, and the library carries an `InternalsVisibleTo.cs` naming that peer — except
`Wolfenstein.Brix.Settings`, whose facade is entirely public and needs none. Test
bodies follow the family style: snake_case names, `//Arrange` / `//Act` /
`//Assert` comments, SilverAssertions, and the test context's cancellation token
passed to any API that takes one.

The division of labor across the test projects is what makes the whole thing
testable without a machine that can run the game.
`Wolfenstein.Brix.GameEngine.Tests` covers the decompression primitives against
hand-worked synthetic streams, palette spot checks, parsing of the real data
files, headless full-frame render checks including determinism, and gameplay flow
on a real level with seeded random streams so the same seed and inputs replay
identically. `Wolfenstein.Brix.Game.Tests` covers the gamepad reader through fake
adapter and manager implementations — possible only because the reader's
constructor takes the engine's interface rather than the concrete SDL2 manager,
which is sealed around a live handle; the tests cover the no-controller case
explicitly, asserting that every field of the sample is the change-nothing value.
`Wolfenstein.Brix.Assets.Tests` covers the classifier and the pipeline, including
that a wrong file fails at the verifying stage, that no executable reaches disk,
and that a single flipped byte fails re-verification.
`Wolfenstein.Brix.Settings.Tests` covers the store behind the facade.

The two missing-data helpers are worth reading together, in
`Wolfenstein.Brix.GameEngine.Tests/TestWl1.cs` and
`Wolfenstein.Brix.Assets.Tests/WolfensteinAssetPipelineTests.cs`. Both walk up from
`AppContext.BaseDirectory` rather than hard-coding a relative path, because the
test binary's depth below the repository root changes with configuration and
target, and both probe for a specific *file* inside the candidate folder so a
half-populated folder is not accepted. They then pick opposite policies —
throwing with instructions, versus skipping quietly — and, since they look in
differently named folders, a machine set up for one is not automatically set up
for the other.

See
[Set up an xUnit v3 test project for a CodeBrix Platform application](../BLUEPRINTS.md#set-up-an-xunit-v3-test-project-for-a-codebrix-platform-application),
[Make data dependent tests explain themselves when the data is missing](../BLUEPRINTS.md#make-data-dependent-tests-explain-themselves-when-the-data-is-missing)
and
[Test a hardware input path behind the engine adapter interfaces](../BLUEPRINTS.md#test-a-hardware-input-path-behind-the-engine-adapter-interfaces).

### Where the head- and build-specific rules live, and where they do not

Every head's `Program.cs` is the same short file: initialize logging, build the
host with the one builder call that selects the head, run it. The two Windows
heads add a call to use the direct Skia canvas mode, and
`Wolfenstein.Brix/src/Wolfenstein.Brix.WinWpfSkia/Program.cs` is the only one that
differs materially — worth reading for the failure it documents. The game
presents a frame every tic and each present schedules a paint on the UI thread;
under the WPF head's default scheduling those paints are posted at a priority that
outranks the tier WPF delivers key events on, so on a device where a paint takes
longer than a tic period the render queue never empties and keyboard input is
starved outright rather than merely delayed. The game keeps rendering and never
responds, sitting on a title screen that has no timeout. `InputFair` dispatcher
scheduling puts paints and key events in one FIFO queue so they interleave. The
same file also sets the software render surface type on the built host, after
`Build()`, by pattern-matching the host type.

The build rules are similarly concentrated. The head projects and
`Wolfenstein.Brix.Core` set the platform's conditional-compilation defines, and
Core sets `RootNamespace` to the application namespace. `Wolfenstein.Brix.Game`
takes a *different* `RootNamespace` on purpose: it picks up CodeBrix.Platform
transitively, and a library sharing the application's root namespace would
generate a duplicate `GlobalStaticResources`. The two libraries that do not touch
the platform at all — `Wolfenstein.Brix.GameEngine` and `Wolfenstein.Brix.Assets`
— set no `RootNamespace` and no defines whatsoever, which is the point: the build
rules stay where they are needed and nowhere else.

Each head declares exactly one platform runtime package and nothing else, with the
comment saying so repeated in all six, and needs both the `Page` glob for `.xaml`
files and the matching `None Remove`. The third-party license texts and the
notices file are declared once, in `Wolfenstein.Brix.Game.csproj` — the library
nearest the code they cover — as `Content` items with `PreserveNewest` and a
`Link` into a `ThirdPartyAssets` subfolder; content items flow transitively through
project references, so declaring them once puts them in every head's output.

See
[Keep WPF paints from starving keyboard input](../BLUEPRINTS.md#keep-wpf-paints-from-starving-keyboard-input),
[Start one application from six head projects](../BLUEPRINTS.md#start-one-application-from-six-head-projects),
[Carry every shared package in one Core library and one runtime package per head](../BLUEPRINTS.md#carry-every-shared-package-in-one-core-library-and-one-runtime-package-per-head),
[Set the RootNamespace and conditional compilation defines CodeBrix Platform needs](../BLUEPRINTS.md#set-the-rootnamespace-and-conditional-compilation-defines-codebrix-platform-needs),
[Ship bundled assets and license notices into every head output](../BLUEPRINTS.md#ship-bundled-assets-and-license-notices-into-every-head-output)
and
[Share App xaml and views through a shared shproj UI project](../BLUEPRINTS.md#share-app-xaml-and-views-through-a-shared-shproj-ui-project).

## Third-party content

[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) in this folder is the
authoritative record of the third-party code this application incorporates: the
adapted csharp-wolfenstein raycaster, wall/door/sprite renderers and map
decompression (code only, none of that project's pre-extracted assets); the game
logic, data-format handling, VGA palette and AdLib drivers hand-translated from id
Software's GPL Wolf3D iOS source release; and the Nuked OPL3 FM synthesizer
emulator hand-ported to C# for the music and AdLib effects. The full license text
for each sits beside the notices file as
[LICENSE_CSharpWolfenstein.txt](LICENSE_CSharpWolfenstein.txt),
[LICENSE_Wolf3D-iOS.txt](LICENSE_Wolf3D-iOS.txt) and
[LICENSE_NukedOPL3.txt](LICENSE_NukedOPL3.txt), and each translated or vendored
source file repeats its provenance in a header naming the upstream file it came
from. The notices file and those license texts are all copied into every head's
build output under `ThirdPartyAssets/`, so a distributed build carries its own
compliance set.

No game data is distributed here or with the application. The shareware `.WL1`
data files are content the user downloads at run time through Assets Mode under id
Software's original shareware terms, together with the vendor license text that
accompanies them.

## License

Wolfenstein.Brix is free software, licensed under the GNU General Public License,
version 2, see [../LICENSE](../LICENSE).

Copyright (c) 2026 Jeremy Ellis and contributors
