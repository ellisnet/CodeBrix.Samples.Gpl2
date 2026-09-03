# CodeBrix.Samples.Gpl2 Blueprints

This file is a set of how-tos for building CodeBrix.Platform applications, mined
from the applications in this repository. Each blueprint says when you would
want it, shows the code, and names the application and files it came from, so
you can open the real thing and read the parts this page leaves out.

The blueprints are written in the MVVM shape CodeBrix.Platform is built for:
view models derived from `SimpleViewModel` own the state (bound properties) and
the behavior (`SimpleCommand` commands, `[AffectsCommands]` to refresh
`CanExecute`, `InvokeOnMainThread` to touch bound state from another thread);
code-behind stays thin, constructing or resolving the view model, setting
`DataContext`, and forwarding platform plumbing in a line or two; platform
capabilities reach the view model through bridge interfaces the page or head
supplies, and the view model degrades gracefully when a head supplies none;
services sit behind interfaces registered with `SimpleServiceResolver` at
startup and resolved in the view model; heavy work runs off the UI thread and
marshals results back. A code block marked `// From ...` is verbatim from the
file it names (with `// ...` where something was trimmed); a block marked
`// Adapted from ...` was recast into that shape, and the prose says what
changed.

Packages are referred to by library or add-in name, never by package ID or
version. The application's csproj is the source of truth for the exact package.

Both applications in this repository are games: a fixed-rate engine loop
advances a simulation and presents its own pixel buffer. The blueprints here
cover the parts of them that are ordinary CodeBrix.Platform application code
(startup, the shared UI, Assets Mode, settings, the download pipeline, the page
shell) plus a dedicated area on hosting the engine itself. The MVVM guidance
applies to the shell around the game, not to the engine internals: inside the
loop the code is a plain object graph on the game-loop thread, and that is
correct there.

## Contents

- Application structure and startup
  - [Start one application from six head projects](#start-one-application-from-six-head-projects)
  - [Bootstrap the application in App xaml cs](#bootstrap-the-application-in-app-xaml-cs)
  - [Share App xaml and views through a shared shproj UI project](#share-app-xaml-and-views-through-a-shared-shproj-ui-project)
  - [Keep WPF paints from starving keyboard input](#keep-wpf-paints-from-starving-keyboard-input)
- View models, commands and threading
  - [Boot an expensive object only when both prerequisites have arrived](#boot-an-expensive-object-only-when-both-prerequisites-have-arrived)
  - [Report multi stage background progress to a bound overlay](#report-multi-stage-background-progress-to-a-bound-overlay)
  - [Show an alert dialog from a view model](#show-an-alert-dialog-from-a-view-model)
- Bridging platform services into the view model
  - [Drive an embedded browser from the view model](#drive-an-embedded-browser-from-the-view-model)
  - [Enforce a one file download policy from the view model](#enforce-a-one-file-download-policy-from-the-view-model)
- Views, XAML and custom controls
  - [Switch a page between two full screen modes with visibility bindings](#switch-a-page-between-two-full-screen-modes-with-visibility-bindings)
  - [Keep keyboard focus on a game canvas](#keep-keyboard-focus-on-a-game-canvas)
  - [Execute a view model command when Enter is pressed in a text box](#execute-a-view-model-command-when-enter-is-pressed-in-a-text-box)
- Documents, data and web APIs
  - [Verify a downloaded file against known checksums](#verify-a-downloaded-file-against-known-checksums)
  - [Rebuild a nested archive in memory and extract only what you need](#rebuild-a-nested-archive-in-memory-and-extract-only-what-you-need)
  - [Unpack a legacy DCL compressed archive safely](#unpack-a-legacy-dcl-compressed-archive-safely)
  - [Download a file with progress and mirror friendly request headers](#download-a-file-with-progress-and-mirror-friendly-request-headers)
  - [Parse original binary data formats into a testable library](#parse-original-binary-data-formats-into-a-testable-library)
- Settings and persistence
  - [Wrap the AppSettings add-in in an application named facade](#wrap-the-appsettings-add-in-in-an-application-named-facade)
  - [Let the user pick a folder and remember the choice](#let-the-user-pick-a-folder-and-remember-the-choice)
  - [Persist a subsystem behind one storage interface](#persist-a-subsystem-behind-one-storage-interface)
- Hosting a game engine
  - [Host a fixed rate game loop inside a XAML page](#host-a-fixed-rate-game-loop-inside-a-xaml-page)
  - [Present a software framebuffer through the game surface canvas](#present-a-software-framebuffer-through-the-game-surface-canvas)
  - [Pause a game engine when the window is minimized](#pause-a-game-engine-when-the-window-is-minimized)
  - [Pump keyboard events and held key state into a game loop](#pump-keyboard-events-and-held-key-state-into-a-game-loop)
  - [Grab the mouse for relative movement in a game loop](#grab-the-mouse-for-relative-movement-in-a-game-loop)
  - [Add optional gamepad support that degrades to keyboard only](#add-optional-gamepad-support-that-degrades-to-keyboard-only)
  - [Sample a gamepad once per tic without losing a sleeping controller](#sample-a-gamepad-once-per-tic-without-losing-a-sleeping-controller)
  - [Play short PCM clips on a pool of engine sound channels](#play-short-pcm-clips-on-a-pool-of-engine-sound-channels)
  - [Stream synthesized audio through one engine voice](#stream-synthesized-audio-through-one-engine-voice)
  - [Port an emulator or codec and keep it reviewable against the original](#port-an-emulator-or-codec-and-keep-it-reviewable-against-the-original)
- Testing
  - [Set up an xUnit v3 test project for a CodeBrix Platform application](#set-up-an-xunit-v3-test-project-for-a-codebrix-platform-application)
  - [Make data dependent tests explain themselves when the data is missing](#make-data-dependent-tests-explain-themselves-when-the-data-is-missing)
  - [Serialize a test assembly that touches process static state](#serialize-a-test-assembly-that-touches-process-static-state)
  - [Test a hardware input path behind the engine adapter interfaces](#test-a-hardware-input-path-behind-the-engine-adapter-interfaces)
- Project layout, packaging and native assets
  - [Carry every shared package in one Core library and one runtime package per head](#carry-every-shared-package-in-one-core-library-and-one-runtime-package-per-head)
  - [Set the RootNamespace and conditional compilation defines CodeBrix Platform needs](#set-the-rootnamespace-and-conditional-compilation-defines-codebrix-platform-needs)
  - [Ship bundled assets and license notices into every head output](#ship-bundled-assets-and-license-notices-into-every-head-output)
- [Not yet covered by a sample](#not-yet-covered-by-a-sample)

## Application structure and startup

### Start one application from six head projects

**When you want this.** You are building a CodeBrix.Platform application and
want one shared UI and one shared Core library to run on all six desktop heads:
LinuxX11, LinuxWayland, LinuxFrameBuffer, MacOS, Win32Skia and WinWpfSkia.

**The MVVM shape.** Nothing application-specific lives in a head. Each head's
`Program.cs` initializes logging, builds the platform host with its own
`Use...()` call, and runs. Every piece of logic sits in the shared UI project
and the Core library the head references, so adding a head is adding a folder
with one file and one package reference.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.LinuxX11/Program.cs
using CodeBrix.Platform.UI.Hosting;
using System;

namespace Doom.Brix;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        var host = CodeBrixPlatformHostBuilder.Create()
            .App(() => new App())
            .UseLinuxX11()
            .Build();

        host.Run();
    }
}
```

The five sibling heads differ only in the builder calls:

| Head project | Builder calls |
| --- | --- |
| `Doom.Brix.LinuxX11` | `.UseLinuxX11()` |
| `Doom.Brix.LinuxWayland` | `.UseLinuxWayland()` |
| `Doom.Brix.LinuxFrameBuffer` | `.UseLinuxFrameBuffer()` |
| `Doom.Brix.MacOS` | `.UseMacOS()` |
| `Doom.Brix.Win32Skia` | `.UseWindowsWin32()` then `.UseDirectSkiaCanvasMode()` |
| `Doom.Brix.WinWpfSkia` | `.UseWindowsWpf(...)` then `.UseDirectSkiaCanvasMode()` (see the WPF blueprint below) |

**Where to look.**

- `Doom.Brix/src/Doom.Brix.LinuxX11/Program.cs`
- `Doom.Brix/src/Doom.Brix.Win32Skia/Program.cs`
- the four other `Doom.Brix/src/Doom.Brix.<Head>/Program.cs` files

**Also shown by.** Wolfenstein.Brix, identically:
`Wolfenstein.Brix/src/Wolfenstein.Brix.LinuxX11/Program.cs` and its five
siblings.

**Sharp edges.**

- `[STAThread]` is on `Main` in every head, including the Linux ones.
- The two Windows heads add `.UseDirectSkiaCanvasMode()`; the Linux and macOS
  heads do not.
- The `net10.0-windows` WinWpfSkia head sets `EnableWindowsTargeting`, which is
  what lets the whole solution restore and build on Linux and macOS too.

### Bootstrap the application in App xaml cs

**When you want this.** You need the standard startup sequence, in the right
order, before any UI renders: the default font, the service resolver, the
design-mode flag, the settings store, and only then `InitializeComponent()`.

**The MVVM shape.** `App` owns bootstrap and nothing else. It creates
`SimpleServiceResolver` from a host-builder provider (so view models can resolve
services), turns design mode off, opens the settings store, and navigates a
`Frame` to the first page. Logging is installed from each head's `Main` before
the host is built.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/App.xaml.cs
public App()
{
    //Set Open Sans as the default font for all text in the application
    global::CodeBrix.Platform.UI.FeatureConfiguration.Font.DefaultTextFontFamily =
        "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf";

    SimpleServiceResolver.CreateInstance(HostHelper.GetHost(), services =>
    {
        //Register the app's services here

    });
    SimpleViewModel.SetIsDesignMode(false);

    //Open (or silently create) the single portable settings.sqlite store —
    //  including its startup auto-backup and pruning — before any UI renders.
    SettingsService.Initialize();

    InitializeComponent();
}
```

The resolver is built from a tiny provider that wraps the generic host builder:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/Helpers/HostHelper.cs
public static class HostHelper
{
    private sealed class HostBuilderProvider : IHostBuilderProvider
    {
        public IHostBuilder CreateDefaultBuilder() => Host.CreateDefaultBuilder();
        public IHostBuilder CreateDefaultBuilder(string[] args) => Host.CreateDefaultBuilder(args);
    }

    private static readonly HostBuilderProvider Provider = new();

    /// <summary>Gets the shared host-builder provider.</summary>
    public static IHostBuilderProvider GetHost() => Provider;
}
```

Logging is a static method on `App`, called from every head's `Main` before the
host is built, and compiled only into debug builds:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/App.xaml.cs
// Called from each head's Program.Main BEFORE building the host.
public static void InitializeLogging()
{
#if DEBUG
    var factory = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddFilter("CodeBrix.Platform", LogLevel.Warning);
        builder.AddFilter("Windows", LogLevel.Warning);
        builder.AddFilter("Microsoft", LogLevel.Warning);
    });

    global::CodeBrix.Platform.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_CODEBRIX
    global::CodeBrix.Platform.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
}
```

`App.xaml` declares the same font a second time, as a resource, and says why it
points at the `.ttf` directly:

```xml
<!-- From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/App.xaml -->
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <!-- Load WinUI resources -->
      <c:XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
    </ResourceDictionary.MergedDictionaries>
    <!-- Open Sans font - reference the .ttf file directly (the Fonts.xaml
         merge does not work on Skia targets) -->
    <m:FontFamily x:Key="OpenSansFont">ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf</m:FontFamily>
  </ResourceDictionary>
</Application.Resources>
```

`OnLaunched` creates the window, installs a `Frame`, and navigates it:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/App.xaml.cs
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    MainWindow = new Window
    {
        Title = "Doom.Brix"
    };

    if (MainWindow.Content is not Frame rootFrame)
    {
        rootFrame = new Frame();
        MainWindow.Content = rootFrame;
        rootFrame.NavigationFailed += OnNavigationFailed;
    }

    if (rootFrame.Content == null)
    {
        rootFrame.Navigate(typeof(Views.MainPage), args.Arguments);
    }

    // ... window lifecycle handlers (see the pause and focus blueprints) ...

    MainWindow.Activate();
}
```

**Where to look.**

- `Doom.Brix/src/Doom.Brix.UI/App.xaml.cs`
- `Doom.Brix/src/Doom.Brix.UI/App.xaml`
- `Doom.Brix/src/Doom.Brix.Core/Helpers/HostHelper.cs`

**Also shown by.** Wolfenstein.Brix, line for line:
`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml.cs`,
`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml`,
`Wolfenstein.Brix/src/Wolfenstein.Brix.Core/Helpers/HostHelper.cs`.

**Sharp edges.**

- `SetIsDesignMode(false)` must be called at startup, right after the resolver
  is created. Without it, a view model constructed from XAML takes its
  design-mode early-return path at run time and looks inert.
- The default font is set on `FeatureConfiguration` *and* declared as an
  `App.xaml` resource, and the resource points at the `.ttf` directly because
  the `Fonts.xaml` merge does not work on Skia targets.
- The settings store is opened before `InitializeComponent()`, so a view model
  constructed by page XAML can read settings safely in its constructor.
- The `LogLevel.Warning` filter on the platform's own categories hides anything
  a library logs at Information. If you want a subsystem's status messages, log
  them through your own facade instead (see the gamepad blueprint).

### Share App xaml and views through a shared shproj UI project

**When you want this.** You want one copy of `App.xaml` and your pages compiled
*into* every head, rather than a library the heads reference.

**The MVVM shape.** The shproj holds `App.xaml(.cs)` and `Views/*.xaml(.cs)`
only. View models live in the Core library and are reached from XAML by
`clr-namespace:...;assembly=<Core>`, so the same view model type is shared by
every head without being duplicated.

**Code.**

```xml
<!-- From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.LinuxX11/Doom.Brix.LinuxX11.csproj -->
<!-- Tell MSBuild to treat .xaml files as CodeBrix.Platform XAML pages -->
<ItemGroup>
  <Page Include="**\*.xaml" Exclude="bin\**\*.xaml;obj\**\*.xaml" />
  <None Remove="**\*.xaml" />
</ItemGroup>

<!-- Shared UI files (App.xaml + Views) -->
<Import Project="..\Doom.Brix.UI\Doom.Brix.UI.projitems" Label="Shared" />
<ItemGroup>
  <ProjectReference Include="..\Doom.Brix.Core\Doom.Brix.Core.csproj" />
</ItemGroup>
```

```xml
<!-- From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/Doom.Brix.UI.projitems -->
<PropertyGroup Label="Configuration">
  <Import_RootNamespace>Doom.Brix.UI</Import_RootNamespace>
</PropertyGroup>
<ItemGroup>
  <Page Include="$(MSBuildThisFileDirectory)App.xaml">
    <SubType>Designer</SubType>
    <Generator>MSBuild:Compile</Generator>
  </Page>
  <Page Include="$(MSBuildThisFileDirectory)Views\MainPage.xaml">
    <SubType>Designer</SubType>
    <Generator>MSBuild:Compile</Generator>
  </Page>
</ItemGroup>
```

The page then reaches its view model across the assembly boundary:

```xml
<!-- From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml -->
<Page
    x:Class="Doom.Brix.Views.MainPage"
    xmlns="clr-namespace:Microsoft.UI.Xaml.Controls;assembly=CodeBrix.Platform.UI"
    xmlns:d="clr-namespace:Microsoft.UI.Xaml.Data;assembly=CodeBrix.Platform.UI"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="clr-namespace:Doom.Brix.ViewModels;assembly=Doom.Brix.Core"
    FontFamily="{StaticResource OpenSansFont}"
    Background="#FF120F0C">

    <Page.DataContext>
        <vm:MainViewModel />
    </Page.DataContext>
```

**Where to look.**

- `Doom.Brix/src/Doom.Brix.UI/Doom.Brix.UI.shproj`
- `Doom.Brix/src/Doom.Brix.UI/Doom.Brix.UI.projitems`
- any of the six `Doom.Brix/src/Doom.Brix.<Head>/*.csproj` files

**Also shown by.** `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Wolfenstein.Brix.UI.projitems`
and the six `Wolfenstein.Brix/src/Wolfenstein.Brix.<Head>/*.csproj` files.

**Sharp edges.**

- Every head needs both the `Page` include *and* the `None Remove` line;
  without the removal the XAML is also treated as content.
- The projitems `Import_RootNamespace` is the shproj's own name
  (`Doom.Brix.UI`), while the XAML `x:Class` values use the application
  namespace (`Doom.Brix.App`, `Doom.Brix.Views.MainPage`). They are deliberately
  different.
- A view model instantiated by `<Page.DataContext>` in XAML must have a
  parameterless constructor and be marked `[Bindable]`.

### Keep WPF paints from starving keyboard input

**When you want this.** The WinWpfSkia head, when your application presents
frames at a high fixed rate and each present schedules a paint on the UI thread.

**The MVVM shape.** A head-only concern, expressed entirely in that head's
`Program.Main`. The other five heads are untouched.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.WinWpfSkia/Program.cs
var host = CodeBrixPlatformHostBuilder.Create()
    .App(() => new App())
    //The game presents a frame every tic (70 Hz) and each present schedules a paint on the
    //  UI thread. Under the WPF head's default RenderFirst scheduling those paints are
    //  posted at DispatcherPriority.Render, which outranks the Input tier WPF delivers key
    //  events on — so on a device where one paint takes longer than the ... tic period
    //  the Render queue never empties, and keyboard input is starved outright rather than
    //  merely delayed: the game keeps rendering but never responds, sitting on the title
    //  screen forever (the title screen has no timeout).
    //  InputFair posts the dispatcher pump on the Input tier instead, so paints and key
    //  events share one FIFO queue and interleave.
    .UseWindowsWpf(wpf => wpf.DispatcherScheduling(WpfDispatcherScheduling.InputFair))
    .UseDirectSkiaCanvasMode()
    .Build();

if (host is WpfHost wpfHost)
{
    wpfHost.RenderSurfaceType = RenderSurfaceType.Software;
}

host.Run();
```

**Where to look.**

- `Wolfenstein.Brix/src/Wolfenstein.Brix.WinWpfSkia/Program.cs`

**Also shown by.** `Doom.Brix/src/Doom.Brix.WinWpfSkia/Program.cs`, which
applies the same fix at a lower tic rate and records why it is applied anyway:
Doom's slower loop "leaves just enough of a gap to squeak by ... but 'just
barely keeping up' is not a margin worth relying on: a bigger window or a slower
device closes it."

**Sharp edges.**

- The failure mode is total, not gradual: the application keeps rendering and
  never responds to a key.
- `RenderSurfaceType.Software` is set on the built host object, after `Build()`,
  by pattern-matching the host to the WPF host type.
- The starvation is rate-dependent, so both applications apply the fix
  unconditionally rather than waiting for the symptom on a slow machine.

## View models, commands and threading

### Boot an expensive object only when both prerequisites have arrived

**When you want this.** Two independent prerequisites must both arrive before an
expensive object can be created, and either can arrive first. Here they are "the
rendering canvas has started" (a view event) and "the data is verified and the
page is in the right mode" (view-model state).

**The MVVM shape.** The view model holds both prerequisites and owns the created
object. One private guard method is called from both arrival points and returns
early unless every prerequisite holds, so the object is created exactly once in
either ordering. The page contributes a single line of code-behind.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs
/// <summary>
/// Called (on the UI thread) by the page code-behind when the game canvas first
/// renders with a real size — with the canvas inside the Game Mode grid, that is
/// when Game Mode first becomes visible.
/// </summary>
public void CanvasFirstStart(GameSurfaceCanvas canvas)
{
    _gameCanvas = canvas;

    //Tracked on the UI thread, read (lock-free) from the game thread by the
    //  host's focus probe (Doom's mouse-grab decisions).
    _canvasHasFocus = true;
    canvas.GotFocus += (_, _) => _canvasHasFocus = true;
    canvas.LostFocus += (_, _) => _canvasHasFocus = false;

    StartGameIfReady();
}

//The host boots exactly once, when BOTH prerequisites have arrived (in either
//  order): the canvas has started, and Game Mode is active with a verified
//  assets folder for the host's data directory.
private void StartGameIfReady()
{
    if (_gameHost != null || _gameCanvas == null || !IsGameMode || !HasAssetsFolder)
    {
        return;
    }

    _gameHost = new DoomGameHost(_gameCanvas, _assetsFolder)
    {
        FocusProbe = () => _canvasHasFocus,
    };

    //Doom's quit flow completed (config already saved): close the application.
    //  The event arrives on the game-loop thread; hop to the UI thread to exit.
    _gameHost.GameExited += () =>
        _gameCanvas.DispatcherQueue.TryEnqueue(() => Application.Current.Exit());

    _gameHost.Initialize();
}
```

The other arrival point is the mode property's setter, which calls the same
guard:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs
/// <summary>Whether Game Mode is active (otherwise Assets Mode shows).</summary>
public bool IsGameMode
{
    get => _isGameMode;
    private set
    {
        SetProperty(ref _isGameMode, value);
        NotifyModeProperties();
        StartGameIfReady();
    }
}
```

The page's whole contribution:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs
//Fires (on the UI thread) once the game canvas first renders with a real
//  size — with the canvas inside the Game Mode grid, that is when Game
//  Mode first becomes visible. The view model boots the game host then.
GameCanvas.FirstStarted += (_, _) =>
{
    (DataContext as MainViewModel)?.CanvasFirstStart(GameCanvas);
    FocusGameCanvas();
};
```

**Where to look.**

- `Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs`
- `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`

**Also shown by.**
`Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs` and
`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml.cs`, with the same
guard and a simpler canvas hookup:

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs
public void CanvasFirstStart(GameSurfaceCanvas canvas)
{
    _gameCanvas = canvas;

    //Focus loss pauses gameplay into the menu (the game's own pause;
    //  the engine-level minimize pause is wired in App.xaml.cs).
    canvas.LostFocus += (_, _) => _gameHost?.NotifyFocusLost();

    StartGameIfReady();
}
```

**Sharp edges.**

- The "canvas first started" event fires only once the canvas has a real size.
  With the canvas inside a collapsed container, that is the moment the container
  first becomes visible, which is exactly the ordering this pattern relies on.
- A flag written on the UI thread and read from a worker thread must be
  `volatile`; `_canvasHasFocus` is.
- Events raised on the worker thread must be marshaled before touching UI state.
  Both applications hop through the canvas's `DispatcherQueue.TryEnqueue(...)`
  before exiting the application; `InvokeOnMainThread` on `SimpleViewModel` does
  the same job without the view model holding a view object.

### Report multi stage background progress to a bound overlay

**When you want this.** A long operation with distinct phases (download, verify,
extract) that must show a stage caption and a percentage without blocking the UI
thread.

**The MVVM shape.** The service reports `IProgress<T>` where `T` carries both
the stage and a fraction in [0, 1], and pushes its CPU work onto `Task.Run` so
it never runs on the caller's thread. The view model translates the stage into
display text and the fraction into a bound percentage, and shows or hides a
full-screen overlay from one bindable flag that is reset in a `finally`.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/WolfensteinAssetPipeline.cs
// Hashing and decompression are pure CPU/disk work; keep them off the caller's (UI) thread.
private static Task VerifyAndExtractAsync(string zipPath, string assetsFolderPath,
    IProgress<AssetProgress> progress, CancellationToken cancellationToken) =>
    Task.Run(() =>
    {
        progress?.Report(new AssetProgress(AssetStage.Verifying, 0d));
        VerifyDownloadedZip(zipPath, cancellationToken);
        progress?.Report(new AssetProgress(AssetStage.Verifying, 1d));

        progress?.Report(new AssetProgress(AssetStage.Extracting, 0d));
        ExtractAssets(zipPath, assetsFolderPath, cancellationToken);
        progress?.Report(new AssetProgress(AssetStage.Extracting, 1d));
    }, cancellationToken);

// ...

private sealed class StageProgress : IProgress<double>
{
    private readonly IProgress<AssetProgress> _inner;
    private readonly AssetStage _stage;
    // ...
    public void Report(double value) => _inner?.Report(new AssetProgress(_stage, value));
}
```

That `StageProgress` adapter is what lets a sub-operation that only knows about
a fraction report into a staged pipeline unchanged.

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs
private async Task InstallAssetsAsync(string zipPath)
{
    try
    {
        var progress = new Progress<AssetProgress>(OnAssetProgress);
        await DoomAssetPipeline.InstallDownloadedZipAsync(zipPath, _assetsFolder,
            progress, CancellationToken.None);

        //Assets Mode completed successfully: on to Game Mode.
        IsGameMode = true;
    }
    catch (AssetPipelineException ex)
    {
        await ShowInstallFailedAsync(ex.Stage);
    }
    catch (Exception)
    {
        await ShowInstallFailedAsync(AssetStage.Verifying);
    }
    finally
    {
        IsDownloading = false;
    }
}

private void OnAssetProgress(AssetProgress report)
{
    DownloadStageText = report.Stage switch
    {
        AssetStage.Downloading => $"Downloading {DoomAssetCatalog.AssetFileName}…",
        AssetStage.Verifying => "Verifying the downloaded file…",
        _ => $"Extracting {DoomAssetCatalog.WadFileName}…",
    };
    DownloadProgress = Math.Clamp(report.Fraction, 0d, 1d) * 100d;
}
```

The bound properties, including the hand-written compare-and-notify for the
`double`:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs
/// <summary>Whether the asset download/verify/extract pipeline is running.</summary>
public bool IsDownloading
{
    get;
    private set
    {
        SetProperty(ref field, value);
        NotifyPropertyChanged(nameof(DownloadOverlayVisibility));
    }
}

/// <summary>The download progress overlay's visibility.</summary>
public Visibility DownloadOverlayVisibility => GetVisibility(IsDownloading);

/// <summary>The pipeline progress in [0, 100].</summary>
public double DownloadProgress
{
    get => _downloadProgress;
    private set
    {
        //No SetProperty overload takes a double; compare-and-notify by hand.
        if (_downloadProgress.Equals(value)) { return; }
        _downloadProgress = value;
        NotifyPropertyChanged(nameof(DownloadProgress));
        NotifyPropertyChanged(nameof(DownloadProgressText));
    }
}

/// <summary>The percentage caption beside the progress bar.</summary>
public string DownloadProgressText => $"{_downloadProgress:0}%";
```

The overlay is the last child of the page's root `Grid`, so it paints on top:

```xml
<!-- From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml -->
<!-- Download/verify/extract progress overlay (topmost) -->
<Grid Visibility="{d:Binding DownloadOverlayVisibility}" Background="#E0000000">
    <Border HorizontalAlignment="Center" VerticalAlignment="Center"
            Background="#FF1E1712" BorderBrush="#FFC21807" BorderThickness="2"
            Padding="36,28" MinWidth="480">
        <StackPanel>
            <TextBlock Text="{d:Binding DownloadStageText}"
                       FontSize="17" FontWeight="SemiBold"
                       Foreground="#FFE8DCC0" HorizontalAlignment="Center" />
            <ProgressBar Margin="0,18,0,8" Height="20" Minimum="0" Maximum="100"
                         Foreground="#FFC21807" Background="#FF3A2C1F"
                         Value="{d:Binding DownloadProgress}" />
            <TextBlock Text="{d:Binding DownloadProgressText}"
                       FontSize="14" FontWeight="Bold"
                       Foreground="#FFFFB000" HorizontalAlignment="Center" />
        </StackPanel>
    </Border>
</Grid>
```

**Where to look.**

- `Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs`
- `Doom.Brix/src/libs/Doom.Brix.Assets/Models/AssetProgress.cs`
- `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml`

**Also shown by.**
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/WolfensteinAssetPipeline.cs`,
`Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs`,
`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml`.

**Sharp edges.**

- `new Progress<T>(...)` captures the creating thread's synchronization context,
  so the callback lands on the UI thread even though the pipeline runs on a
  worker. That is what makes it safe to set bound properties directly in the
  handler.
- `SetProperty` has no `double` overload here; compare and notify by hand, and
  notify the derived caption property in the same setter.
- A custom exception type carrying the failed *stage* lets one catch block
  produce the right wording for three different failures.
- Reset the overlay flag in a `finally`, so a failure never leaves a modal
  overlay on screen.
- Both applications pass `CancellationToken.None` into the pipeline even though
  every layer beneath takes a token. If you want the operation cancelable, hold
  a `CancellationTokenSource` on the view model and pass its token.

### Show an alert dialog from a view model

**When you want this.** View-model logic needs to tell the user something, with
the page supplying nothing but the XAML root.

**The MVVM shape.** The page hands the view model a `XamlRoot` getter through
the platform's `IXamlRootGetter` interface as soon as the data context arrives.
The view model then calls `CreateDialog(...)` (a `SimpleViewModel` helper) and
awaits `ShowAsync()`, with no reference to the page at all.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs
//Doing this before InitializeComponent() - in case InitializeComponent()
//  is the thing that sets the data context.
DataContextChanged += (_, _) =>
{
    //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
    (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);
};

this.InitializeComponent();
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs
private async Task ShowInstallFailedAsync(AssetStage stage)
{
    var word = stage switch
    {
        AssetStage.Downloading => "download",
        AssetStage.Verifying => "verification",
        _ => "extraction",
    };
    using var alert = CreateDialog(
        $"The {word} of the game assets from the selected file failed, please try again.",
        "Assets Setup Failed");
    _ = await alert.ShowAsync();
}
```

**Where to look.**

- `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`
- `Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs`

**Also shown by.**
`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml.cs` and
`Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs`
(`ShowUnrecognizedDownloadCanceledAsync`).

**Sharp edges.**

- Subscribe to `DataContextChanged` *before* `InitializeComponent()`, because on
  these pages `InitializeComponent()` is what sets the data context (the page
  declares `<Page.DataContext><vm:MainViewModel /></Page.DataContext>` in XAML).
- The dialog is disposable; `using var` is the shape.
- Both applications reach these from non-async call sites with
  `_ = ShowSomethingAsync();`. That is fire-and-forget with no failure path for
  the task itself. If the dialog call can throw, capture the task or route it
  through a safe fire-and-forget helper.

## Bridging platform services into the view model

### Drive an embedded browser from the view model

**When you want this.** Commands on the view model must navigate an embedded
browser the page owns, and the view model must know which page the user ended up
on.

**The MVVM shape.** The view model owns the navigation behavior (a
`SimpleCommand` plus URL normalization) and consumes the browser through a
bridge the page implements. It degrades gracefully: when no head supplied a
bridge, `EnsureBrowserStarted()` simply returns. The block below is adapted:
both samples expose a bare `public Action<string> NavigateToUrl { get; set; }`
that the code-behind assigns, and this recasts that into the bridge interface
the rest of the sample family uses. The mechanism is identical.

**Code.**

```csharp
// Adapted from CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs
// (the sample exposes `public Action<string> NavigateToUrl { get; set; }` and a
//  parameterless `OnBrowserReady()`; here the same wiring is one bridge interface)
public interface IWebViewBridge
{
    void Navigate(string url);
}

private IWebViewBridge _browser;

/// <summary>Called by the code-behind once the WebView bridge is wired.</summary>
public void SetWebViewBridge(IWebViewBridge bridge)
{
    _browser = bridge;
    EnsureBrowserStarted();
}

private void EnsureBrowserStarted()
{
    if (_hasNavigated || IsGameMode || !HasAssetsFolder || _browser == null) { return; }

    _hasNavigated = true;
    _browser.Navigate(WolfensteinAssetCatalog.DefaultBrowseUrl);
}
```

Everything else is verbatim. The command and the URL normalization stay in the
view model:

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs
/// <summary>Navigates the embedded browser to the address bar's URL.</summary>
public SimpleCommand GoCommand => field ??= new SimpleCommand(NavigateToAddress);

private void NavigateToAddress()
{
    var url = NormalizeUrl(AddressText);
    if (url == null) { return; }

    _hasNavigated = true;
    NavigateToUrl?.Invoke(url);
}

/// <summary>Tracks the page the user is on (for the address bar).</summary>
public void SetCurrentBrowserUrl(string url)
{
    if (string.IsNullOrWhiteSpace(url)) { return; }

    AddressText = url;
}
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs
//Accepts bare host names ("example.com/page") by defaulting to https.
internal static string NormalizeUrl(string text)
{
    if (string.IsNullOrWhiteSpace(text)) { return null; }

    var trimmed = text.Trim();
    if (!trimmed.Contains("://", StringComparison.Ordinal))
    {
        trimmed = "https://" + trimmed;
    }

    return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        ? uri.AbsoluteUri
        : null;
}
```

The page supplies the implementation and reports the authoritative current URL
back:

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml.cs
//Use CoreWebView2.Source (the authoritative current URL after redirects / user
//  navigation); the XAML Browser.Source property does not reliably reflect those.
Browser.NavigationCompleted += (_, _) =>
    viewModel.SetCurrentBrowserUrl(Browser.CoreWebView2?.Source ?? Browser.Source?.AbsoluteUri);

viewModel.NavigateToUrl = url =>
{
    if (!string.IsNullOrWhiteSpace(url)) { Browser.Source = new Uri(url); }
};

viewModel.OnBrowserReady();
```

The browser itself is one XAML element inside the mode grid it belongs to:

```xml
<!-- From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml -->
<Border Grid.Row="2" BorderBrush="#FF4A3B2C" BorderThickness="1">
    <WebView2 x:Name="Browser" />
</Border>
```

**Where to look.**

- `Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs`
- `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml.cs`
- `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml`

**Also shown by.**
`Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs` and
`Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`.

**Sharp edges.**

- Read `CoreWebView2.Source` for the current URL after redirects and user
  navigation; the XAML `Source` property does not reliably reflect them.
- Initialize the browser only in the mode that uses it. Both pages skip
  `EnsureCoreWebView2Async()` entirely in Game Mode, which saves the startup cost
  and, more importantly, keeps the WebView's native focus proxy from stealing
  keyboard focus from the game canvas.
- On the Linux heads the embedded browser comes from the CodeBrix.Platform.WebView
  add-in (WPE WebKit); on Windows and macOS the platform runtime supplies one.
  The add-in is referenced once, from the Core library, not per head.
- Normalize bare host names and reject any scheme that is not http or https
  before navigating.

### Enforce a one file download policy from the view model

**When you want this.** The user must fetch a file you cannot redistribute, from
a site you do not control. Browsing should be free, but exactly one download is
permitted and everything else is refused with an explanation.

**The MVVM shape.** The view model owns the policy as one method that answers
"is this the file we want, and where should it go?", plus three notification
methods for progress, completion and failure. The page's code-behind subscribes
to the browser's download event and forwards to those methods; it holds no
policy of its own. The recognition rule itself is a pure static class in a
library with no UI dependency, so it can be unit tested.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs
/// <summary>
/// The Assets Mode download policy, called when the WebView starts a
/// download (page navigation is never intercepted). The one permitted
/// asset file is accepted: the browser downloads it into the returned
/// temp target path and the .Assets pipeline verifies/extracts it on
/// completion. Any other download returns false to be canceled, with an
/// explanation — as does a second download while one is in progress.
/// </summary>
public bool HandleDownloadStarting(string url, string suggestedFileName, out string targetFilePath)
{
    targetFilePath = null;

    if (!AssetUrlClassifier.IsAssetDownload(url, suggestedFileName))
    {
        _ = ShowUnrecognizedDownloadCanceledAsync();
        return false;
    }

    if (IsDownloading) { return false; }

    IsDownloading = true;
    DownloadProgress = 0;
    DownloadStageText = $"Downloading {DoomAssetCatalog.AssetFileName}…";
    targetFilePath = DoomAssetPipeline.CreateTempDownloadPath();
    return true;
}

/// <summary>Download progress for the accepted asset download.</summary>
public void OnAssetDownloadProgress(long bytesReceived) =>
    DownloadProgress = Math.Clamp((double)bytesReceived / DoomAssetCatalog.AssetZipSize, 0d, 1d) * 100d;

/// <summary>The accepted asset download finished; verify and extract it.</summary>
public void OnAssetDownloadCompleted(string zipPath) => _ = InstallAssetsAsync(zipPath);

/// <summary>The accepted asset download failed or was interrupted.</summary>
public void OnAssetDownloadFailed()
{
    IsDownloading = false;
    _ = ShowInstallFailedAsync(AssetStage.Downloading);
}
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs
await Browser.EnsureCoreWebView2Async();
Browser.CoreWebView2.DownloadStarting += (_, args) =>
{
    if (viewModel.HandleDownloadStarting(args.DownloadOperation.Uri,
            System.IO.Path.GetFileName(args.ResultFilePath), out var targetFilePath))
    {
        args.ResultFilePath = targetFilePath;
        args.DownloadOperation.BytesReceivedChanged += (operation, _) =>
            viewModel.OnAssetDownloadProgress(operation.BytesReceived);
        args.DownloadOperation.StateChanged += (operation, _) =>
        {
            if (operation.State == CoreWebView2DownloadState.Completed)
            {
                viewModel.OnAssetDownloadCompleted(operation.ResultFilePath);
            }
            else if (operation.State == CoreWebView2DownloadState.Interrupted)
            {
                viewModel.OnAssetDownloadFailed();
            }
        };
    }
    else
    {
        args.Cancel = true;
    }
};
```

The recognition rule is pure and testable:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Assets/AssetUrlClassifier.cs
/// <summary>
/// Whether a download the browser is starting is the one permitted asset
/// file, judged by the download's suggested target file name (with the
/// browser's collision suffix — "doom19s (2).zip" — ignored) or, as a
/// fallback, by the file name visible in the download URL's path (a
/// mirror may serve the file under a URL that does not show its name).
/// </summary>
public static bool IsAssetDownload(string url, string suggestedFileName) =>
    string.Equals(StripCollisionSuffix(suggestedFileName), DoomAssetCatalog.AssetFileName,
        StringComparison.OrdinalIgnoreCase)
    || IsAssetFileUrl(url);
```

**Where to look.**

- `Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs`
- `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`
- `Doom.Brix/src/libs/Doom.Brix.Assets/AssetUrlClassifier.cs`

**Also shown by.**
`Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs`,
`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml.cs`,
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/AssetUrlClassifier.cs`.
Their classifiers are unit tested in
`Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.Assets.Tests` and
`Doom.Brix/tests/libs/Doom.Brix.Assets.Tests`.

**Sharp edges.**

- Hook the download event only; never intercept navigation. Users need to be
  free to browse to whichever mirror works for them.
- Recognize the wanted file two ways, by the suggested file name (with the
  browser's " (2)" collision suffix stripped) *or* by the file name in the URL
  path, because a mirror may serve it under a URL that does not show the name.
- Redirect the download to a temp path you control by assigning
  `args.ResultFilePath`, then verify the bytes yourself before trusting them.
- Refuse a second download while one is in progress, in the same method.
- The whole `DownloadStarting` / `BytesReceivedChanged` / `StateChanged` wiring
  sits in code-behind in both samples. The policy is already in the view model;
  moving the subscriptions behind the same bridge interface would leave the page
  with a one-line-per-member implementation.

## Views, XAML and custom controls

### Switch a page between two full screen modes with visibility bindings

**When you want this.** One page that is really two experiences (setup and
main), plus a modal overlay, with no navigation and no second page type.

**The MVVM shape.** A single private mode flag on the view model; every visible
region binds to a derived read-only `Visibility` property computed with
`SimpleViewModel.GetVisibility(bool)`, which removes the need for a
bool-to-visibility converter. One private method notifies every derived property
together whenever the mode or its inputs change, so they cannot drift apart.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs
/// <summary>Whether Game Mode is active (otherwise Assets Mode shows).</summary>
public bool IsGameMode
{
    get => _isGameMode;
    private set
    {
        SetProperty(ref _isGameMode, value);
        NotifyModeProperties();
        StartGameIfReady();
    }
}

public Visibility GameModeVisibility => GetVisibility(IsGameMode);
public Visibility AssetsModeVisibility => GetVisibility(!IsGameMode);
public bool HasAssetsFolder => !string.IsNullOrWhiteSpace(_assetsFolder);
public string AssetsFolderLabel => HasAssetsFolder ? _assetsFolder : "Choose assets folder…";
public Visibility FolderSetupVisibility => GetVisibility(!IsGameMode && !HasAssetsFolder);
public Visibility BrowserAreaVisibility => GetVisibility(!IsGameMode && HasAssetsFolder);

private void NotifyModeProperties()
{
    NotifyPropertyChanged(nameof(GameModeVisibility));
    NotifyPropertyChanged(nameof(AssetsModeVisibility));
    NotifyPropertyChanged(nameof(HasAssetsFolder));
    NotifyPropertyChanged(nameof(AssetsFolderLabel));
    NotifyPropertyChanged(nameof(FolderSetupVisibility));
    NotifyPropertyChanged(nameof(BrowserAreaVisibility));
}
```

```xml
<!-- From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml -->
<Grid>
  <Grid Visibility="{d:Binding  AssetsModeVisibility}"> <!-- ... --> </Grid>
  <Grid Visibility="{d:Binding  GameModeVisibility}" Background="#FF000010">
    <game:GameSurfaceCanvas x:Name="GameCanvas" />
  </Grid>
  <Grid Visibility="{d:Binding  DownloadOverlayVisibility}" Background="#E0000020">
    <!-- progress card -->
  </Grid>
</Grid>
```

Inside Assets Mode, a nested pair of regions is driven by the same technique:
the folder-setup card shows until a folder is chosen, and the browser area
replaces it afterwards.

```xml
<!-- From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml -->
<!-- Folder setup (no folder chosen yet) -->
<Border Visibility="{d:Binding FolderSetupVisibility}"
        HorizontalAlignment="Center" VerticalAlignment="Center"
        Background="#FF241D16" BorderBrush="#FFC21807" BorderThickness="2"
        MaxWidth="620" Padding="32,28">
    <StackPanel>
        <!-- ... -->
        <Button HorizontalAlignment="Center" Height="44" Padding="24,0" FontWeight="Bold"
                Background="#FF3A2C1F" Foreground="#FFF0E6D2"
                BorderBrush="#FFC21807" BorderThickness="2"
                Content="CHOOSE ASSETS FOLDER…"
                Command="{d:Binding PickFolderCommand}" />
    </StackPanel>
</Border>
```

**Where to look.**

- `Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs`
- `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml`
- `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml`

**Also shown by.**
`Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs`, with the same
property set.

**Sharp edges.**

- Sibling grids in one parent grid, each with its own visibility, stack by
  document order. Declare the overlay last and no z-index is needed.
- Derived visibility properties need explicit `NotifyPropertyChanged` calls;
  grouping them in one method is what keeps a new property from being forgotten.
- The pages declare
  `xmlns:d="clr-namespace:Microsoft.UI.Xaml.Data;assembly=CodeBrix.Platform.UI"`
  and use `{d:Binding ...}` throughout, including
  `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged` on the address `TextBox`.

### Keep keyboard focus on a game canvas

**When you want this.** A focusable rendering surface receives key events only
while it holds keyboard focus, and ordinary interactions keep taking focus away
from it. The symptom is a keyboard that goes silently dead with nothing on
screen to explain it.

**The MVVM shape.** Focus is genuine view plumbing and stays in the page, but
the page keeps it to a handful of one-line handlers and asks the view model
which mode it is in before stealing focus. The application forwards window
activation to the page through one internal method. The view model never touches
focus; it only publishes the mode.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs
//Keys reach the game only while the game surface holds keyboard focus,
//  and clicking the canvas (firing, with mouse support) moves focus off
//  it — KeyDown then routes to the focused element's ancestors, never
//  the canvas, and the keyboard goes dead until Tab restores focus.
//  Hand focus straight back after every click. handledEventsToo: true
//  because the press may already be marked handled.
GameCanvas.AddHandler(
    UIElement.PointerReleasedEvent,
    new PointerEventHandler((_, _) => FocusGameCanvas()),
    handledEventsToo: true);

// ...

//Called by the app when the window is activated. ... Game Mode only: in
//  Assets Mode the embedded browser owns the keyboard, and stealing focus
//  would break typing in it.
internal void OnWindowActivated()
{
    if (DataContext is MainViewModel { IsGameMode: true })
    {
        FocusGameCanvas();
    }
}

//Defer to the dispatcher so focus lands after whatever took it from the
//  click finishes processing.
private void FocusGameCanvas() =>
    DispatcherQueue.TryEnqueue(() => GameCanvas.Focus(FocusState.Programmatic));
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/App.xaml.cs
MainWindow.Activated += (_, e) =>
{
    if (e.WindowActivationState != global::Windows.UI.Core.CoreWindowActivationState.Deactivated &&
        rootFrame.Content is Views.MainPage page)
    {
        page.OnWindowActivated();
    }
};
```

**Where to look.**

- `Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`
- `Doom.Brix/src/Doom.Brix.UI/App.xaml.cs`

**Also shown by.**
`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml.cs` and
`Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml.cs`, identically.

**Sharp edges.**

- Three separate paths lose focus and each needs its own repair: the canvas's
  first start, every click on the canvas, and window deactivation followed by
  activation (alt-tabbing away and back, or raising the window from another
  application).
- Register the pointer handler with `handledEventsToo: true`; the press is often
  already marked handled by the time it reaches you.
- Re-apply focus through the dispatcher, not inline in the handler, so it lands
  after whatever took it finishes processing.
- Only steal focus in the mode that owns the keyboard. In Assets Mode the
  embedded browser owns it, and stealing focus would break typing.
- A connected gamepad keeps working throughout the failure, because the SDL2
  path reads the device directly and needs no window focus. Both applications
  record that in a comment, because it makes the symptom look stranger than it
  is.
- The application casts the frame's content to the concrete page type to make
  this call. A small interface the page implements would remove that coupling
  from `App`.

### Execute a view model command when Enter is pressed in a text box

**When you want this.** An address bar or search box where Enter should do what
the adjacent button does.

**The MVVM shape.** The command lives on the view model and the button binds to
it normally. The key handler is one line of view plumbing that asks the same
command whether it can run and then runs it. The handler below is adapted: the
sample reaches the command through a property pattern
(`DataContext is MainViewModel { GoCommand: var go }`), and this reads it from a
named local instead, which is easier to follow. Behavior is identical.

**Code.**

```xml
<!-- From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml -->
<TextBox x:Name="AddressBox" Grid.Column="0" Height="38"
         KeyDown="AddressBox_KeyDown"
         Text="{d:Binding  AddressText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
<Button Grid.Column="1" Content="GO" Command="{d:Binding  GoCommand}" />
```

```csharp
// Adapted from CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml.cs
//Pressing Enter in the address bar navigates, just like clicking GO.
private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
{
    if (e.Key == Windows.System.VirtualKey.Enter
        && DataContext is MainViewModel viewModel
        && viewModel.GoCommand.CanExecute(null))
    {
        viewModel.GoCommand.Execute(null);
        e.Handled = true;
    }
}
```

**Where to look.**

- `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml`
- `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml.cs`

**Also shown by.**
`Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml` and
`Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`.

**Sharp edges.**

- `UpdateSourceTrigger=PropertyChanged` on the `TextBox` is required, or the
  view model has not seen the typed text when Enter arrives.
- Ask `CanExecute` before `Execute`, and set `e.Handled` so the key does not
  travel on.
- A `KeyboardAccelerator` in XAML bound to the same command, or an attached
  behavior, keeps the page free of the handler entirely; that is the shape to
  reach for when the gesture is not tied to one specific control.

## Documents, data and web APIs

### Verify a downloaded file against known checksums

**When you want this.** Content fetched from a mirror you do not control must be
proven to be exactly the file you expected before you act on it, and the check
must be cheap enough to repeat on every launch.

**The MVVM shape.** A pure static service in a library with no UI dependency,
plus a catalog class holding the known-good facts as constants. The view model
never computes a hash; it calls the service and reacts to a typed exception that
carries the failing stage.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/Internal/ChecksumHelper.cs
using CodeBrix.Compression.Checksum;

internal static class ChecksumHelper
{
    /// <summary>
    /// Computes the file's size, CRC-32 and MD5 (lowercase hex) in a single
    /// streaming pass with the shared copy buffer.
    /// </summary>
    public static (long Size, long Crc32, string Md5Hex) ComputeFileChecksums(
        string filePath, byte[] buffer, CancellationToken cancellationToken = default)
    {
        var crc = new Crc32();
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        long totalBytes = 0;

        using (var stream = File.OpenRead(filePath))
        {
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                crc.Update(new ArraySegment<byte>(buffer, 0, bytesRead));
                md5.AppendData(buffer, 0, bytesRead);
                totalBytes += bytesRead;
            }
        }

        return (totalBytes, crc.Value, Convert.ToHexStringLower(md5.GetHashAndReset()));
    }
}
```

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/WolfensteinAssetPipeline.cs
if (checks.Size != WolfensteinAssetCatalog.AssetZipSize
    || checks.Crc32 != WolfensteinAssetCatalog.AssetZipCrc32
    || checks.Md5Hex != WolfensteinAssetCatalog.AssetZipMd5)
{
    throw new AssetPipelineException(AssetStage.Verifying, /* ... */);
}
```

The launch-time re-check never throws, so a corrupt install sends the user back
to setup instead of crashing:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetPipeline.cs
public static bool VerifyInstalledAssets(string assetsFolderPath)
{
    if (string.IsNullOrWhiteSpace(assetsFolderPath)) { return false; }

    try
    {
        var wadPath = Path.Combine(assetsFolderPath, DoomAssetCatalog.WadFileName);
        if (!File.Exists(wadPath)) { return false; }

        var checks = ChecksumHelper.ComputeFileChecksums(wadPath, new byte[CopyBufferSize]);
        return checks.Size == DoomAssetCatalog.WadSize
            && checks.Crc32 == DoomAssetCatalog.WadCrc32
            && checks.Md5Hex == DoomAssetCatalog.WadMd5
            && HasConsistentIwadStructure(wadPath);
    }
    catch
    {
        return false;
    }
}
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/Internal/ChecksumHelper.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/WolfensteinAssetCatalog.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/WolfensteinAssetPipeline.cs`

**Also shown by.**
`Doom.Brix/src/libs/Doom.Brix.Assets/Internal/ChecksumHelper.cs`,
`Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetCatalog.cs`,
`Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetPipeline.cs`.

**Sharp edges.**

- One streaming pass computes size, CRC-32 and MD5 together from one shared
  buffer. Hashing a large file three times is the thing to avoid.
- CodeBrix.Compression's `Crc32` is incremental (`Update(...)` then `.Value`),
  which is what makes the single pass possible alongside `IncrementalHash`.
- Add a structural check after the checksums. Both applications do: Doom.Brix
  requires the extracted file's header to describe a directory that ends exactly
  at end-of-file, and Wolfenstein.Brix checks its map file's editor magic and
  its map header's compression tag, because the inner format carries no checksum
  of its own.
- Verify the extracted payload twice: once against the archive's own declared
  values, and once against your known-good values. Those are two genuinely
  independent checks.
- The launch-time method swallows every exception and returns false, so a
  missing or unreadable file is a "not installed" answer rather than a crash.

### Rebuild a nested archive in memory and extract only what you need

**When you want this.** The file you download is an archive whose interesting
content is itself an archive, split across entries, and you do not want any
intermediate file on disk.

**The MVVM shape.** A static pipeline class in a library with no UI dependency,
taking a folder path and an `IProgress<T>`, throwing a typed exception carrying
the failed stage. The view model calls it and never touches a stream.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetPipeline.cs
// Hashing and inflating are pure CPU/disk work; keep them off the caller's (UI) thread.
private static Task VerifyAndExtractAsync(string zipPath, string assetsFolderPath,
    IProgress<AssetProgress> progress, CancellationToken cancellationToken) =>
    Task.Run(() =>
    {
        progress?.Report(new AssetProgress(AssetStage.Verifying, 0d));
        VerifyDownloadedZip(zipPath, cancellationToken);
        progress?.Report(new AssetProgress(AssetStage.Verifying, 1d));

        progress?.Report(new AssetProgress(AssetStage.Extracting, 0d));
        ExtractAssets(zipPath, assetsFolderPath, cancellationToken);
        progress?.Report(new AssetProgress(AssetStage.Extracting, 1d));
    }, cancellationToken);
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetPipeline.cs
// Streams the two archive part entries, in order, into one presized
// MemoryStream ... with each part's CRC-32 verified against the zip's central
// directory during the same copy pass.
using var sourceZip = new ZipFile(zipPath);
// ...
int index = sourceZip.FindEntry(DoomAssetCatalog.ArchivePartNames[i], ignoreCase: true);
// ...
using Stream partStream = sourceZip.GetInputStream(part);
// ... copy into archiveStream while updating crc ...
if (crc.Value != part.Crc)
{
    throw new AssetPipelineException(AssetStage.Extracting,
        $"CRC-32 mismatch for '{part.Name}': computed {crc.Value:x8}, declared {part.Crc:x8}.");
}
```

**Where to look.**

- `Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetPipeline.cs`
- `Doom.Brix/src/libs/Doom.Brix.Assets/DoomAssetCatalog.cs`
- `Doom.Brix/src/libs/Doom.Brix.Assets/Internal/ChecksumHelper.cs`

**Sharp edges.**

- The nested archive is rebuilt in memory into one presized stream; no
  intermediate file touches disk, and the temp directory is deleted in a
  `finally` whether the pipeline succeeded or not.
- Verify each part's CRC-32 against the outer archive's central directory during
  the same copy pass that assembles it, not afterwards.
- `Crc32` and the zip reader both come from CodeBrix.Compression; the MD5 is
  `System.Security.Cryptography.IncrementalHash`.

### Unpack a legacy DCL compressed archive safely

**When you want this.** You must read an old installer format: a zip whose entry
is itself a proprietary catalog of DCL-imploded members, and you want only some
of those members on disk.

**The MVVM shape.** A static extraction service in a library, taking a source
path, a destination folder and a cancellation token. Nothing about it is
UI-aware.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/WolfensteinAssetPipeline.cs
using CodeBrix.Compression.Checksum;
using CodeBrix.Compression.Dcl;
using CodeBrix.Compression.Zip;

// ...

private static byte[] ExtractShrFromZip(string zipPath)
{
    using var zipFile = new ZipFile(zipPath);

    int index = zipFile.FindEntry(WolfensteinAssetCatalog.ShrEntryName, ignoreCase: true);
    if (index < 0) { throw new AssetPipelineException(AssetStage.Extracting, /* ... */); }

    ZipEntry entry = zipFile[index];
    var data = new byte[entry.Size];

    using (Stream entryStream = zipFile.GetInputStream(entry))
    {
        entryStream.ReadExactly(data);
        if (entryStream.ReadByte() != -1)
        {
            throw new AssetPipelineException(AssetStage.Extracting,
                $"'{WolfensteinAssetCatalog.ShrEntryName}' is larger than its declared size.");
        }
    }

    var crc = new Crc32();
    crc.Update(data);
    if (crc.Value != entry.Crc) { throw new AssetPipelineException(AssetStage.Extracting, /* ... */); }

    return data;
}

// ... inside the catalog walk, for a member worth keeping:
using (var compressedStream = new MemoryStream(archive, position, compressedSize, writable: false))
using (var dclStream = new DclInputStream(compressedStream))
using (var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5))
using (FileStream outputStream = File.Create(destinationPath))
{
    int bytesRead;
    while ((bytesRead = dclStream.Read(buffer, 0, buffer.Length)) > 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        md5.AppendData(buffer, 0, bytesRead);
        outputStream.Write(buffer, 0, bytesRead);
        uncompressedSize += bytesRead;
    }
    md5Hex = Convert.ToHexStringLower(md5.GetHashAndReset());
}
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/WolfensteinAssetPipeline.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/WolfensteinAssetCatalog.cs`

**Sharp edges.**

- Read a zip entry with `ReadExactly` into a buffer sized from the declared
  size, then confirm the stream is exhausted. An entry *larger* than declared is
  caught that way, and the central directory's CRC-32 is checked against the
  bytes actually read.
- Skip unwanted members by their compressed size without decompressing them, so
  nothing you did not ask for ever touches disk. This pipeline calls that out
  explicitly for the archive's DOS executables.
- Validate every member name before joining it to an output folder: reject an
  empty name, any name containing a path separator, and any name containing
  `..`.
- A fixed-size name field may be NUL-terminated with uninitialized garbage after
  the terminator; cut at the *first* NUL.
- Require the walk to consume the archive to its exact final byte and to have
  found every expected member, and treat either shortfall as a failure.
- One pass feeds the hash and the disk write from the same buffer fill.

### Download a file with progress and mirror friendly request headers

**When you want this.** Your application fetches a file itself, rather than
through an embedded browser, from a mirror that may check where the request came
from.

**The MVVM shape.** A small sealed downloader class with a static shared
`HttpClient`, taking the URL, a referrer, a destination path, an expected-size
fallback, an `IProgress<double>` and a cancellation token. It knows nothing
about the UI, and the view model turns its fraction into a bound percentage.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/AssetDownloader.cs
static HttpClient CreateClient()
{
    var created = new HttpClient();
    // Downloads can be long on slow links; cancellation governs, not a timeout.
    created.Timeout = Timeout.InfiniteTimeSpan;
    return created;
}

public async Task DownloadFileAsync(string url, string referrerUrl, string destinationFilePath,
    long expectedSizeFallback, IProgress<double> progress, CancellationToken cancellationToken)
{
    // ...
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
    request.Headers.TryAddWithoutValidation("Accept", "*/*");
    if (!string.IsNullOrWhiteSpace(referrerUrl) && Uri.TryCreate(referrerUrl, UriKind.Absolute, out var referrer))
        request.Headers.Referrer = referrer;

    using var response = await client
        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var totalBytes = response.Content.Headers.ContentLength ?? expectedSizeFallback;
    // ... stream to disk, reporting received/totalBytes ...
}
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/AssetDownloader.cs`

**Also shown by.** `Doom.Brix/src/libs/Doom.Brix.Assets/AssetDownloader.cs`.

**Sharp edges.**

- `HttpCompletionOption.ResponseHeadersRead` is what makes progress reporting
  possible; without it the whole body buffers before you see a byte.
- Set the client timeout to infinite and let the cancellation token govern. A
  long download on a slow link is not an error.
- Have a fallback total when the server declares no `Content-Length`, or the
  progress bar has no denominator.
- The request deliberately presents itself like the embedded browser the user
  clicked the link in (a matching User-Agent plus the clicked page as
  `Referer`), which satisfies the hotlink checks common on download mirrors.

### Parse original binary data formats into a testable library

**When you want this.** You must read files in a format documented mainly by
reverse engineering, and you want the parsing verifiable independently of the
application.

**The MVVM shape.** One parser class per file format, each with a static
`Load(...)` factory and a private constructor, gathered behind one aggregate
type that loads the whole set and reports a clear error naming the missing file
and the part of the application that obtains it. Everything is parsed into
memory at startup; nothing derived is written back to disk.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Assets/WolfAssets.cs
public sealed class WolfAssets
{
    /// <summary>The shareware data file extension.</summary>
    public const string FileExtension = "WL1";

    // RE-ENABLE ADDITIONAL .WL6 FILE SUPPORT HERE: the parsers handle
    // the registered data files as-is; to support them, make the
    // extension selectable ("WL6"), pick it by probing which set is
    // present in the assets folder, and remove the shareware-only
    // level-count assumptions in the level-flow logic.

    public VswapFile Vswap { get; private set; }
    public GameMapsFile Maps { get; private set; }
    public VgaGraphFile Graphics { get; private set; }
    public AudioTFile Audio { get; private set; }

    public static WolfAssets Load(string assetsFolderPath)
    {
        string PathOf(string baseName)
        {
            var candidate = Path.Combine(assetsFolderPath, baseName + "." + FileExtension);
            if (!File.Exists(candidate))
            {
                throw new FileNotFoundException(
                    $"The Wolfenstein 3-D shareware data file {baseName}.{FileExtension} was not found in " +
                    $"{assetsFolderPath}. Use the application's Assets Mode to download and install the " +
                    "shareware episode.", candidate);
            }

            return candidate;
        }

        return new WolfAssets
        {
            Vswap = VswapFile.Load(PathOf("VSWAP")),
            Maps = GameMapsFile.Load(PathOf("MAPHEAD"), PathOf("GAMEMAPS")),
            Graphics = VgaGraphFile.Load(PathOf("VGADICT"), PathOf("VGAHEAD"), PathOf("VGAGRAPH")),
            Audio = AudioTFile.Load(PathOf("AUDIOHED"), PathOf("AUDIOT")),
        };
    }
}
```

The decompression schemes the formats need sit in one `Compression` static class
with golden tests built on hand-worked synthetic streams, so the primitives are
verifiable without any real data file at all.

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Assets/WolfAssets.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Assets/VswapFile.cs`,
  `GameMapsFile.cs`, `VgaGraphFile.cs`, `AudioTFile.cs`, `Compression.cs`
- `Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.GameEngine.Tests/CompressionTests.cs`,
  `WolfAssetsTests.cs`

**Also shown by.** `Doom.Brix/src/libs/Doom.Brix.GameEngine/` for the WAD
format, with its parsing covered by
`Doom.Brix/tests/libs/Doom.Brix.GameEngine.Tests`.

**Sharp edges.**

- Separate the decompression primitives from the file parsers. The primitives
  can be tested with tiny synthetic streams whose expansions are worked out by
  hand, and only the parsers then need real data.
- Give malformed data its own exception type (here `InvalidWolfDataException`),
  so "this file is wrong" stays distinguishable from "the code is wrong".
- The error message for a missing file names the file *and* tells the user which
  part of the application obtains it.
- Record a forward-looking limitation as a searchable comment marker at the
  exact place a future change would start, rather than in a separate document.

## Settings and persistence

### Wrap the AppSettings add-in in an application named facade

**When you want this.** Every part of an application, including libraries that
must not depend on the platform, reads and writes settings through one short,
discoverable, application-named type instead of calling the add-in directly.

**The MVVM shape.** A static facade class in its own small library, with the
application name as a constant and one forwarding member per operation.
`Initialize()` is called once from the `App` constructor, before any UI renders.
View models and other libraries call the facade; only the facade knows the
add-in.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Settings/SettingsService.cs
public static class SettingsService
{
    /// <summary>The application name the settings store is registered under.</summary>
    public const string AppName = "Wolfenstein.Brix";

    public static bool IsInitialized => AppSettingsService.IsInitialized;
    public static AppSettingsStore Store => AppSettingsService.Store;
    public static string DefaultDirectory => AppSettingsService.GetDefaultDirectory(AppName);

    /// <summary>
    /// Opens the settings store in the default folder, running the startup
    /// auto-backup and pruning sequence. Call once, before any UI renders.
    /// </summary>
    public static void Initialize() => AppSettingsService.Initialize(AppName);

    /// <summary>Opens the settings store in the given folder.</summary>
    public static void Initialize(string directoryPath) =>
        AppSettingsService.Initialize(AppName, directoryPath);

    /// <summary>
    /// Closes the store and permits a later <see cref="Initialize()"/> (test hosts).
    /// </summary>
    public static void Shutdown() => AppSettingsService.Shutdown();

    public static AppSettingProperty<T> Wrap<T>(string property, T defaultValue) =>
        AppSettingsService.Wrap(property, defaultValue);
    public static bool HasValue(string property) => AppSettingsService.HasValue(property);
    public static T Get<T>(string property, T defaultValue) => AppSettingsService.Get(property, defaultValue);
    public static T Get<T>(string property) => AppSettingsService.Get<T>(property);
    public static void Set(string key, object val) => AppSettingsService.Set(key, val);
    public static void AddPropertyHandler(string propertyName, EventHandler<AppSettingChangedEventArgs> handler) =>
        AppSettingsService.AddSettingHandler(propertyName, handler);
    public static void RemovePropertyHandler(string propertyName, EventHandler<AppSettingChangedEventArgs> handler) =>
        AppSettingsService.RemoveSettingHandler(propertyName, handler);
}
```

A sibling `LoggingService` forwards to the add-in's logging service in the same
way (`AddSink`, `LogInfo`, `LogWarning`, `LogError`), so a library that logs
does not need a reference to the add-in either. That is what the game host logs
gamepad availability through.

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Settings/SettingsService.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Settings/LoggingService.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Settings/Wolfenstein.Brix.Settings.csproj`

**Also shown by.** `Doom.Brix/src/libs/Doom.Brix.Settings/SettingsService.cs`
and `Doom.Brix/src/libs/Doom.Brix.Settings/LoggingService.cs`, with the same
member set under a different `AppName`.

**Sharp edges.**

- `Initialize()` runs in the `App` constructor, before `InitializeComponent()`,
  so the store (and its startup auto-backup and pruning) is open before any UI
  renders and before any view model constructor reads a setting.
- The overload taking a directory and the `Shutdown()` method exist so test
  hosts can point the process-global store at a throwaway folder and
  re-initialize.
- Each facade csproj records the boundary in a comment: the add-in provides the
  machinery (store, typed properties, change events, backup, import/export);
  this library is the thin application-named facade over it.
- The facade is a static class, not an interface registered with the resolver.
  That is a deliberate trade (other libraries call it without a container), but
  it means a view model cannot be tested against a fake settings store. If you
  want that, put an interface in front of it and register the implementation
  with `SimpleServiceResolver`.

### Let the user pick a folder and remember the choice

**When you want this.** A one-time setup choice (where data lives) that survives
restarts and is re-validated on every launch.

**The MVVM shape.** A `SimpleCommand` opens the platform folder picker, the
awaited result is stored through the settings facade, and the view model
re-derives the properties that drive the UI. The constructor reads the
remembered value back and drops it when it no longer holds up.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs
/// <summary>The settings.sqlite key holding the user's chosen assets folder.</summary>
public const string AssetsFolderKey = "Doom.Brix.Settings.AssetsFolder";

/// <summary>Creates the view model and decides the starting mode.</summary>
public MainViewModel()
{
    if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

    _assetsFolder = SettingsService.Get<string>(AssetsFolderKey);
    if (!string.IsNullOrWhiteSpace(_assetsFolder) && !Directory.Exists(_assetsFolder))
    {
        //The remembered folder was deleted between sessions; ask again.
        _assetsFolder = null;
    }

    //The assets re-verify on every launch: straight to Game Mode only
    //  when the remembered folder still holds the authentic DOOM1.WAD.
    if (!string.IsNullOrWhiteSpace(_assetsFolder) && DoomAssetPipeline.VerifyInstalledAssets(_assetsFolder))
    {
        _isGameMode = true;
    }
}

/// <summary>Opens the folder picker to choose where the game assets live.</summary>
public SimpleCommand PickFolderCommand => field ??=
    new SimpleCommand(_ => PickFolderAsync());

private async Task PickFolderAsync()
{
    var picker = new FolderPicker
    {
        SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
    };
    picker.FileTypeFilter.Add("*");

    var folder = await picker.PickSingleFolderAsync();
    if (folder == null) { return; }

    SetAssetsFolder(folder.Path);
}

private void SetAssetsFolder(string path)
{
    _assetsFolder = path;
    SettingsService.Set(AssetsFolderKey, path);
    NotifyModeProperties();

    //A folder that already holds verified assets skips the download entirely.
    if (DoomAssetPipeline.VerifyInstalledAssets(path))
    {
        IsGameMode = true;
        return;
    }

    EnsureBrowserStarted();
}
```

**Where to look.**

- `Doom.Brix/src/Doom.Brix.Core/ViewModels/MainViewModel.cs`

**Also shown by.**
`Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs`, with
the same three methods under its own settings key.

**Sharp edges.**

- `if (IsDesignMode(true)) { return; }` must be the first line of a view model
  constructor that touches services. Both samples carry the comment saying so.
- `picker.FileTypeFilter.Add("*")` is required even for a folder picker.
- A remembered path is validated twice: the folder must still exist, and its
  contents must still verify. Both failures fall back to the setup screen rather
  than to an error message.
- Choosing a folder that already holds verified data skips the acquisition step
  entirely, so a user who moves the application to a new machine and points it
  at a copied folder is straight into the application.

### Persist a subsystem behind one storage interface

**When you want this.** A self-contained subsystem (an engine, a document model,
a vendored component) must persist state without knowing where it goes, must be
testable headless, and must not write files of its own next to your executable.

**The MVVM shape.** The subsystem declares one interface covering every
persistence call site and ships a working default implementation, so nothing
downstream (including the component's own test suite) needs the application to
be present. The application supplies the real implementation, which talks only
to the settings facade. There are two ways to hand it over, and this repository
shows both.

**Code.** *Constructor injection*, when you control the subsystem's construction:

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Logic/IWolfStorage.cs
/// <summary>
/// The engine's one storage seam: game configuration, save slots and
/// the high-score table all persist through this interface. The
/// application injects the settings.sqlite-backed implementation; the
/// game itself never writes files.
/// </summary>
public interface IWolfStorage
{
    /// <summary>The number of save slots.</summary>
    const int SaveSlotCount = 8;

    string LoadConfigText();
    void SaveConfigText(string text);
    byte[] LoadSaveSlot(int slot);
    void SaveSaveSlot(int slot, byte[] data, string description);
    string[] GetSaveSlotDescriptions();
    HighScore[] LoadHighScores();
    void SaveHighScores(HighScore[] scores);
}

/// <summary>
/// The in-memory fallback storage used when the application injects
/// nothing (tests, headless tools). Holds data for the process
/// lifetime only.
/// </summary>
public sealed class MemoryWolfStorage : IWolfStorage
{
    // ...
}
```

The view model injects the real one in a single place:
`new WolfGameHost(_gameCanvas, _assetsFolder, new SqliteWolfStorage())`.

*A settable static seam*, when you are retrofitting a vendored component whose
call sites you do not want to change:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.GameEngine/ConfigUtilities.cs
// The persistence seam for config and save slots. Defaults to the
// file-based storage (managed-doom's original behavior, under
// DataDirectory) so the headless engine and its tests need no setup;
// the hosting application assigns a settings.sqlite-backed
// implementation before booting the game, exactly like DataDirectory.
public static IDoomStorage Storage
{
    get => storage ??= new FileDoomStorage();
    set => storage = value;
}
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs
protected override void OnLoadContent()
{
    // The single funnel for the disk paths the game core still touches:
    // DOOM1.WAD discovery resolves against this directory.
    ConfigUtilities.DataDirectory = dataDirectory;

    // Persist config and save slots in the app's settings.sqlite (via
    // Doom.Brix.Settings) instead of writing managed-doom.cfg / doomsav*.dsg
    // files, so the game writes no files of its own.
    ConfigUtilities.Storage = new SqliteDoomStorage();
    // ...
}
```

Either way, the store-backed implementation is small and talks only to the
facade:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/SqliteDoomStorage.cs
public sealed class SqliteDoomStorage : IDoomStorage
{
    public const string ConfigKey = "Doom.Brix.Game.Config";

    public static string SlotDataKey(int slot) => "Doom.Brix.Game.SaveSlot" + slot + ".Data";
    public static string SlotDescriptionKey(int slot) => "Doom.Brix.Game.SaveSlot" + slot + ".Description";

    public string LoadConfigText() => SettingsService.Get<string>(ConfigKey, null);
    public void SaveConfigText(string text) => SettingsService.Set(ConfigKey, text);

    public byte[] LoadSaveSlot(int slot)
    {
        var base64 = SettingsService.Get<string>(SlotDataKey(slot), null);
        return string.IsNullOrEmpty(base64) ? null : Convert.FromBase64String(base64);
    }

    public void SaveSaveSlot(int slot, string description, byte[] data)
    {
        SettingsService.Set(SlotDataKey(slot), Convert.ToBase64String(data));

        // Store the description exactly as the load/save menus decode it from a
        // .dsg ... so sqlite-backed slot names match vanilla file-based names
        // byte-for-byte after a restart.
        SettingsService.Set(SlotDescriptionKey(slot),
            DoomInterop.ToString(data, 0, SaveAndLoad.DescriptionSize));
    }

    public string[] ReadSlotDescriptions()
    {
        var descriptions = new string[SaveSlots.SlotCount];
        for (var i = 0; i < descriptions.Length; i++)
        {
            // A slot is present only when its data key exists; the description
            // key alone (or a stale one) never conjures a phantom slot.
            descriptions[i] = SettingsService.HasValue(SlotDataKey(i))
                ? SettingsService.Get<string>(SlotDescriptionKey(i), null)
                : null;
        }

        return descriptions;
    }
}
```

**Where to look.**

- `Doom.Brix/src/libs/Doom.Brix.GameEngine/IDoomStorage.cs`
- `Doom.Brix/src/libs/Doom.Brix.GameEngine/ConfigUtilities.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/SqliteDoomStorage.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs`

**Also shown by.**
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Logic/IWolfStorage.cs`,
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/SqliteWolfStorage.cs`,
`Wolfenstein.Brix/src/Wolfenstein.Brix.Core/ViewModels/MainViewModel.cs`.

**Sharp edges.**

- Give the seam a working default. Wolfenstein.Brix defaults to an in-memory
  implementation; Doom.Brix defaults to the original file-based one. Either way
  the subsystem's own tests need no application.
- Binary payloads go in as base64 strings, with a second key holding the
  human-readable label so a list screen never has to decode the payload.
- Make the *data* key the presence test, so a stale label key cannot resurrect a
  deleted entry.
- The implementation talks only to the settings facade, never to the store's
  sqlite handle. That keeps one owner of the handle, and everything the
  application persists inherits the add-in's auto-backup, pruning and
  corrupt-file quarantine for free.
- Let the subsystem-side interface declare its own constants (the slot count),
  so both implementations and the UI agree without a second source of truth.

## Hosting a game engine

### Host a fixed rate game loop inside a XAML page

**When you want this.** You have a simulation that must advance at a fixed rate
independent of the UI and render its own pixels, and you want it inside an
ordinary page rather than taking over the whole window.

**The MVVM shape.** The page declares the engine's game surface canvas in XAML.
The view model owns the host object and its lifetime; the page's only job is to
hand the canvas to the view model when the canvas first starts. The host itself
derives from the engine's software-rendered base class and overrides the
lifecycle hooks: configure gamepads, configure audio, load content, tic, render,
shut down.

**Code.**

```xml
<!-- From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml -->
<Page
    x:Class="Wolfenstein.Brix.Views.MainPage"
    xmlns="clr-namespace:Microsoft.UI.Xaml.Controls;assembly=CodeBrix.Platform.UI"
    xmlns:d="clr-namespace:Microsoft.UI.Xaml.Data;assembly=CodeBrix.Platform.UI"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="clr-namespace:Wolfenstein.Brix.ViewModels;assembly=Wolfenstein.Brix.Core"
    xmlns:game="clr-namespace:CodeBrix.Platform.GameEngine.Host.Rendering;assembly=CodeBrix.Platform.GameEngine.Host">
  <!-- ... -->
  <Grid Visibility="{d:Binding  GameModeVisibility}" Background="#FF000010">
    <game:GameSurfaceCanvas x:Name="GameCanvas" />
  </Grid>
</Page>
```

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGameHost.cs
public sealed class WolfGameHost : SoftwareRenderedGameHostBase
{
    public WolfGameHost(GameSurfaceCanvas canvas, string dataDirectoryPath, IWolfStorage storage = null)
        : base(canvas, WolfLogic.TicsPerSecond)
    {
        // ...
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
        // ...
        session.Tic(userInput.BuildInput());
        sound.Volume = session.SfxVolume / 10.0f;
        music.Update();
    }

    /// <inheritdoc />
    protected override void OnRenderFrame(Span<byte> frameBuffer)
        => video.RenderInto(frameBuffer);
```

Shutdown disposes in dependency order, and the base class calls it for you:

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGameHost.cs
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
```

The tic rate is a plain constant handed to the base constructor, so the loop
runs at the simulation's own rate on every head with no per-head configuration:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs
public sealed class DoomGameHost : SoftwareRenderedGameHostBase
{
    // Doom's fixed logic rate: 35 tics per second, v1 renders 1:1 (fpsscale 1).
    private const int DoomTicRate = 35;

    // ...

    public DoomGameHost(GameSurfaceCanvas canvas, string dataDirectoryPath)
        : base(canvas, DoomTicRate)
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGameHost.cs`
- `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/Views/MainPage.xaml`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Logic/WolfLogic.cs`

**Also shown by.** `Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs`,
`Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml`,
`Doom.Brix/src/Doom.Brix.UI/Views/MainPage.xaml.cs`.

**Sharp edges.**

- `ConfigureAudio()` must pin the shared audio device before any sound channel
  or streaming source exists.
- `OnLoadContent()` is where the input backend must be constructed: the engine
  has wired its input adapters by then and not before.
- `Initialize()` is called exactly once, on the UI thread, from the canvas's
  first-start handler.
- The XAML namespace shows a packaging fact: the canvas type lives in the
  `...GameEngine.Host` assembly, which ships in the same package as the engine.
  There is no separate host package.
- In `OnShutdown()`, detach the gamepad manager from the engine *before*
  disposing it. The engine's input poll reaches it through that property.

### Present a software framebuffer through the game surface canvas

**When you want this.** Your renderer composes its own pixel buffer at a fixed
resolution and you want it scaled and letterboxed into whatever window size the
head gives you, with no per-frame CPU conversion.

**The MVVM shape.** A small backend class owns the renderer and configures the
host's presenter once, in its constructor. The host's `OnRenderFrame` override
hands the backend the frame span. Nothing about this reaches the view model.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfVideo.cs
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
```

When the renderer's buffer is not row-major, declare the orientation instead of
transposing it yourself, and take the dimensions from the renderer rather than
from constants:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixVideo.cs
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

    public void RenderInto(ManagedDoom.Doom doom, Span<byte> frame, Fixed frameFrac)
    {
        renderer.Render(doom, renderBuffer, frameFrac);
        renderBuffer.CopyTo(frame);
    }

    void IVideo.Render(ManagedDoom.Doom doom, Fixed frameFrac)
    {
        // The game core never drives rendering; the host's OnRenderFrame calls
        // RenderInto with the presentation buffer instead.
        throw new NotSupportedException("Rendering is driven by DoomGameHost.OnRenderFrame.");
    }

    public bool HasFocus() => FocusProbe == null || FocusProbe();

    // ... window size, gamma, wipe members forward straight to the renderer ...
}
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfVideo.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Rendering/FrameComposer.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixVideo.cs`

**Sharp edges.**

- Configure the presenter once in the constructor, never per frame.
- `MemoryMarshal.Cast<byte, uint>` on the span the engine hands you lets the
  renderer write whole pixels with no copy.
- A column-major renderer costs nothing if you declare
  `FrameOrientation.Rotate90` on the presenter; a per-frame CPU transpose is
  avoidable work.
- `ImageFilterQuality.None` gives the nearest-neighbor scaling a pixel-art
  target wants; `PixelFrameScaleMode.Fit` letterboxes rather than stretching.
- Take the presenter's dimensions from the renderer (a low-resolution or a
  doubled buffer is a configuration choice), not from constants.
- Inverting control so the host's `OnRenderFrame` drives rendering is what lets
  the presenter own the buffer. Doom.Brix makes the engine interface's own
  `Render` method deliberately unsupported to enforce that.

### Pause a game engine when the window is minimized

**When you want this.** A background window should not keep a loop and an audio
device busy, and the user should come back exactly where they left off.

**The MVVM shape.** Window-level lifecycle belongs to the application object.
Both calls are idempotent, so no state has to be tracked and no ordering guard
is needed.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml.cs
//The GameEngine's GLOBAL pause: minimizing the window parks the whole
//  engine (the 70 Hz game loop idles at ~zero CPU and audio — including
//  the OPL music stream — suspends); restoring resumes exactly where it
//  left off, with the pause invisible to game time. The game's own
//  ESC-menu pause is separate game logic and unaffected. Both calls are
//  idempotent, and a pause that lands before the game host initializes
//  simply starts the loop parked.
// ... (X11 minimize needs a platform head that tracks _NET_WM_STATE;
//      workspace switches deliberately do NOT pause)
MainWindow.VisibilityChanged += (_, e) =>
{
    if (e.Visible)
    {
        global::CodeBrix.Platform.GameEngine.Engine.Instance.Resume();
    }
    else
    {
        global::CodeBrix.Platform.GameEngine.Engine.Instance.Pause();
    }
};
```

**Where to look.**

- `Wolfenstein.Brix/src/Wolfenstein.Brix.UI/App.xaml.cs`

**Also shown by.** `Doom.Brix/src/Doom.Brix.UI/App.xaml.cs`, identically.

**Sharp edges.**

- On X11 this depends on the head tracking `_NET_WM_STATE` and raising
  `VisibilityChanged` on iconification; older platform builds ignored the unmap
  and kept the loop running while minimized.
- Workspace switches deliberately do not pause.
- This engine-level pause is separate from any in-application pause the
  simulation implements itself. Do not conflate them.
- The calls reach a static engine singleton from the application class. Behind a
  small lifecycle interface registered with `SimpleServiceResolver` this would
  be testable and would keep `App` framework-only.

### Pump keyboard events and held key state into a game loop

**When you want this.** A loop that needs both discrete key edges (menus,
typing) and continuous held-key state (movement), sampled consistently once per
tic.

**The MVVM shape.** Not a view-model concern. A small input class constructed
from the host's `OnLoadContent`, disposed from `OnShutdown`. It subscribes to
the engine's keyboard poller for edges and polls the lock-free adapter for held
state; the loop asks it for one input value per tic. Both paths run on the
game-loop thread, which removes the need for synchronization.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfUserInput.cs
public WolfUserInput(GameSession gameSession, SdlGamepadManager gamepadManager)
{
    session = gameSession;
    gamepad = new WolfGamepadInput(gamepadManager);

    var poller = KeyboardEventPoller.Instance;
    if (poller == null)
    {
        throw new InvalidOperationException(
            "The keyboard event poller is not available; construct WolfUserInput from OnLoadContent (after the host wired the input adapters).");
    }

    keyboard = poller.Adapter;
    poller.StartMonitoringAllKeys();
    poller.KeyDown += OnKeyEvent;
}

// ...

/// <summary>Samples held keys and the controller, and drains this tic's edges into one input.</summary>
public SessionInput BuildInput()
{
    // ...
    if (keyboard.IsDown(VkUp) || keyboard.IsDown(VkW)) { game.ForwardMove += move; }
    // ...
    var input = new SessionInput
    {
        Game = game,
        MenuUp = edgeUp || pad.MenuUp,
        // ...
        TypedChar = typedChar,
    };

    edgeUp = edgeDown = edgeActivate = edgeBack = edgeBackspace = false;
    typedChar = '\0';
    return input;
}

public void Dispose()
{
    if (KeyboardEventPoller.Instance is KeyboardEventPoller poller)
    {
        poller.KeyDown -= OnKeyEvent;
    }
}
```

Filter out the operating system's own key repeat when your consumer implements
repeat itself, and queue only true edges:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixUserInput.cs
private void OnKeyEvent(KeyDownEventArgs args)
{
    // Doom does its own key repeat; only true edges are forwarded.
    if (args.KeyAction == KeyAction.Repeated)
    {
        return;
    }

    var key = DoomKeys.ToDoomKey(args.KeyCode);
    if (key == DoomKey.Unknown)
    {
        return;
    }

    // Raised on the game-loop thread (during InputPump.PollNow), the same thread
    // that drains the queue in PostPendingEventsTo — no synchronization needed.
    pendingKeys.Enqueue(new QueuedKey(key, args.KeyAction == KeyAction.Pressed));
}
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixUserInput.cs
// Held-key state is polled straight from the lock-free adapter:
for (var i = 0; i < weaponKeys.Length; i++)
{
    weaponKeys[i] = keyboard.IsDown(0x31 + i); // The top-row 1..7 keys.
}
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfUserInput.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixUserInput.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/DoomKeys.cs`

**Sharp edges.**

- The poller only exists after the host has wired its input adapters, so
  construct the backend from `OnLoadContent()`. Both applications throw a message
  saying exactly that when the poller is null.
- `StartMonitoringAllKeys()` must be called, or held-key queries stay false.
- Clear the edge flags as part of building the input value, so an edge is
  consumed exactly once.
- Key codes are Windows virtual-key codes on every head. Keep a translation
  table in both directions, including the OEM codes with no named member.
- `Dispose()` unsubscribes from the poller's event; the poller itself is a
  singleton owned by the engine, so do not dispose it.

### Grab the mouse for relative movement in a game loop

**When you want this.** Pointer movement should turn or look rather than move a
cursor, the pointer should be released whenever a menu is open, and the whole
thing should be optional so a head without relative-mouse support still runs.

**The MVVM shape.** Not a view-model concern. The host creates a relative-mouse
session over its render surface and hands it to the input backend, which
consumes a delta once per tic and exposes grab and release as one-line methods
the simulation's own policy drives.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs
mouseSession = new RelativeMouseSession(RenderSurface);
userInput = new CodeBrixUserInput(config, mouseSession, gamepads);
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixUserInput.cs
// Mouse look, as upstream: sensitivity-scaled per-tic deltas turn (or
// strafe) horizontally and move vertically; y is dropped when the
// config disables it.
if (mouseSession != null && mouseSession.IsActive)
{
    var (deltaX, deltaY) = mouseSession.ConsumeDelta();
    if (config.mouse_disableyaxis)
    {
        deltaY = 0;
    }

    var ms = 0.5F * config.mouse_sensitivity;
    var mx = (int)MathF.Round(ms * deltaX);
    var my = (int)MathF.Round(ms * -deltaY);
    forward += my;
    if (strafe)
    {
        side += mx * 2;
    }
    else
    {
        cmd.AngleTurn -= (short)(mx * 0x8);
    }
}
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixUserInput.cs
// Doom's core drives these from its own grab policy (in-game with focus =>
// grab; menus/paused/unfocused => release). On a platform head without
// relative-mouse support the session stays inactive (it logs once) and the
// game simply runs keyboard-only.
public void GrabMouse() => mouseSession?.Begin();

public void ReleaseMouse() => mouseSession?.End();
```

**Where to look.**

- `Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixUserInput.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs`

**Sharp edges.**

- Consume the accumulated delta once per tic; do not read it more than once, and
  drain it in a `Reset()` so a paused frame does not deliver a jump.
- Every call site guards on `mouseSession?.IsActive`, so a head that cannot
  supply relative-mouse support degrades to keyboard-only with no branches
  elsewhere.
- The grab and release decision belongs to the simulation's own state machine
  (grab in play with focus, release for menus and when unfocused), not to the
  input backend.
- Mouse *buttons* are read through the mouse poller's adapter, but only while
  the session is active, so a released pointer does not fire the weapon.

### Add optional gamepad support that degrades to keyboard only

**When you want this.** Controller support that is always on, never configured,
and completely inert when there is no controller and no SDL2 on the machine.

**The MVVM shape.** Not a view-model concern. The manager is created in the
host's `ConfigureGamepads()` override, which never throws, and availability is
reported through the application's own logging facade rather than the engine's.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGameHost.cs
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
```

The optional add-on is referenced only by the library that uses it, and the
csproj records what it does and does not require:

```xml
<!-- Adapted from CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/Doom.Brix.Game.csproj
     (package IDs and versions elided; see the project's csproj) -->
<ItemGroup>
  <PackageReference Include="..." Version="..." />
  <!-- Optional add-on: SDL2-backed game controller support, published independently of
       the engine package (it pins the engine version above). SDL2 is started with the
       game-controller subsystem only, so it opens no display and contends with nothing
       on any head. On Linux the SYSTEM SDL2 is used (sudo apt install libsdl2-2.0-0);
       when it is missing the manager reports itself unavailable and the game runs on
       with keyboard and mouse. -->
  <PackageReference Include="..." Version="..." />
</ItemGroup>
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGameHost.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/Doom.Brix.Game.csproj`

**Also shown by.** `Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs`, with the
same `ConfigureGamepads()` and `LogGamepadAvailability()` pair.

**Sharp edges.**

- SDL2 is started with the game-controller subsystem only, so it opens no
  display and contends with nothing on any head, including the framebuffer head.
- On Linux the system SDL2 is used. When the library is missing, the manager
  reports itself unavailable rather than throwing.
- Pass `logStatus: false` and report through your own logging. The add-on's own
  status messages go through `ILogger` at Information, which the host's default
  warning filter drops, so a *working* controller would otherwise log nothing at
  all.
- Log the device's mapping string. It is what reconciles a device's raw button
  and axis numbering with the standard layout, and it varies by transport.
- Detach `Engine.Instance.Input.GamepadManager` before disposing the manager in
  `OnShutdown()`.

### Sample a gamepad once per tic without losing a sleeping controller

**When you want this.** Per-tic controller polling that survives a controller
sleeping and waking, with edge detection for buttons and a repeat clock for menu
navigation.

**The MVVM shape.** Not a view-model concern. A small struct carries one tic's
sample; the reader keeps previous-tic button state for edges and a repeat clock
for held directions. With no controller connected the sample is `default` and
every fold-in downstream is a no-op, so the keyboard path needs no branches.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGamepadInput.cs
// Held state as of the previous tic, for edge detection. The engine's poller could
// raise ButtonDown events instead, but its registrations are per-GamepadId and a
// controller that sleeps and wakes comes back with a NEW id — polling the adapter
// list every tic sidesteps that re-registration entirely.
private bool prevA;
// ...

public WolfGamepadSample Sample(PlayerState player)
{
    IGamepadAdapter pad = FirstConnectedPad();
    if (pad == null)
    {
        ResetHeldState();
        return default;
    }

    GamepadStickState move = pad.LeftStick?.WithDeadzone(MoveDeadzone) ?? default;
    GamepadStickState look = pad.RightStick?.WithDeadzone(TurnDeadzone) ?? default;

    // Magnitude legitimately exceeds 1 on a corner-held stick (up to about 1.25 on a
    // real pad, because X and Y are clamped independently). Scaling movement by the
    // raw components would hand out that extra as diagonal speed, which is the
    // classic diagonal-speed-boost bug; shrink the vector back onto the unit circle.
    (float forward, float side) = ClampToUnitCircle(move.Y, move.X);

    bool a = pad.PressedButtons.Contains(SdlGamepadButtons.A);
    // ...
    var sample = new WolfGamepadSample
    {
        Forward = forward,
        Side = side,
        Turn = look.X,
        Fire = pad.RightTrigger > TriggerThreshold,
        Use = a,
        Run = pad.LeftTrigger > TriggerThreshold,
        // ...
        MenuActivate = a && !prevA,
    };
    // ...
}
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/DoomGamepadInput.cs
/// <summary>
/// Shrinks a stick vector whose magnitude exceeds 1 back onto the unit circle,
/// leaving everything inside it untouched.
/// </summary>
internal static (float Y, float X) ClampToUnitCircle(float y, float x)
{
    float magnitude = MathF.Sqrt((x * x) + (y * y));
    return magnitude > 1f ? (y / magnitude, x / magnitude) : (y, x);
}
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGamepadInput.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/DoomGamepadInput.cs`

**Sharp edges.**

- Poll the adapter list every tic instead of registering for button events. The
  engine's registrations are per gamepad id, and a controller that sleeps and
  wakes comes back with a new id.
- Clamp a stick vector back onto the unit circle. X and Y clamp independently,
  so a corner-held stick legitimately reads above magnitude 1 and would
  otherwise become a diagonal speed boost.
- The engine's deadzone helper gates the whole stick radially, not per axis.
- A stick that doubles as a menu D-pad needs a firmer gate than movement does;
  both applications use a separate, larger menu threshold plus a repeat clock.
- Make the struct's `default` value mean "change nothing" (Doom.Brix makes its
  weapon slot deliberately one-based for exactly this reason), so the
  keyboard-only path stays free of special cases.
- Gate synthesized cursor keys to menu state only. In play the same keys may be
  read as press-and-hold panning; keep the gate as one static predicate.

### Play short PCM clips on a pool of engine sound channels

**When you want this.** Many short sound effects, registered once and replayed
cheaply, with a bounded number of simultaneous voices.

**The MVVM shape.** Not a view-model concern. A backend class registers every
clip with the engine's audio resource manager at load time and keeps a fixed
pool of channels. The simulation raises an event with a sound number; the
backend picks an idle channel or steals round-robin.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfSound.cs
public WolfSound(WolfAssets assets, WolfLogic logic)
{
    var sounds = assets.Vswap.DigitizedSounds;
    clipRegistered = new bool[sounds.Length];
    for (var i = 0; i < sounds.Length; i++)
    {
        if (sounds[i].Length == 0)
        {
            continue;
        }

        AudioResourceManager.Instance.LoadFromPcm(
            ClipKey(i), sounds[i], DigitizedSampleRate, bitsPerSample: 8, channels: 1);
        clipRegistered[i] = true;
    }

    channels = new SoundChannel[ChannelCount];
    channelClipKeys = new string[ChannelCount];
    for (var i = 0; i < channels.Length; i++)
    {
        channels[i] = new SoundChannel();
    }

    logic.DigitizedSoundRequested += Play;
}

private static string ClipKey(int index) => "wolf-digi-" + index;

/// <summary>Plays a digitized sound by its shareware VSWAP number.</summary>
public void Play(int soundNumber)
{
    // ...
    // Prefer an idle channel; otherwise steal round-robin (the
    // original's single digi channel stole unconditionally).
    var channelIndex = -1;
    for (var i = 0; i < channels.Length; i++)
    {
        if (channels[i].State != PlaybackState.Playing)
        {
            channelIndex = i;
            break;
        }
    }

    if (channelIndex < 0)
    {
        channelIndex = nextChannel;
        nextChannel = (nextChannel + 1) % channels.Length;
    }

    var channel = channels[channelIndex];
    var key = ClipKey(soundNumber);
    if (channelClipKeys[channelIndex] != key)
    {
        channel.SetClip(key);
        channelClipKeys[channelIndex] = key;
    }

    channel.Play(volume: volume, pan: 0.0f);
}
```

When the sound has a position in the world, compute pan and distance decay from
the listener once per update rather than modeling a 3D source:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixSound.cs
var dist = MathF.Sqrt(x * x + y * y);
var angle = MathF.Atan2(y, x) - (float)listener.Angle.ToRadian();

// Upstream positions the OpenAL source at (-sin(angle), 0, -cos(angle));
// the equivalent stereo pan is the x component.
channel.Pan = -MathF.Sin(angle);
channel.Volume = 0.01F * masterVolumeDecay * GetDistanceDecay(dist) * info.Volume;
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfSound.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixSound.cs`

**Sharp edges.**

- `LoadFromPcm(...)` takes raw PCM with its own sample rate, bit depth and
  channel count. Rate conversion is built into the channel, so an odd source
  rate needs no resampling step of your own.
- Register clips once, under stable keys; channels only reference the key.
- Remember which clip each channel currently holds and skip `SetClip` when it is
  unchanged.
- Dispose every channel in the backend's `Dispose()`, which the host calls from
  `OnShutdown`. Stop each channel before disposing it, and null the array, so a
  second shutdown pass is harmless.
- A positional model expressed as a 3D source position collapses to a single
  stereo `Pan` value. Record the equivalence in a comment rather than leaving it
  to be re-derived.
- Doom.Brix also keeps one channel outside the game pool for interface sounds,
  and skips pausing voices with a fraction of a second left rather than pausing
  a clip that is about to end anyway.

### Stream synthesized audio through one engine voice

**When you want this.** Continuously generated audio (a synthesizer, a decoder,
a procedural source) rather than fixed clips, fed to the device by a pull
callback.

**The MVVM shape.** Not a view-model concern. One disposable backend class owns
the synthesizer and a single pull-model streaming source built from the
synthesizer's fill method. Simulation state is followed once per tic from the
game loop, never from the audio thread.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfMusic.cs
public WolfMusic(WolfAssets assets, GameSession session)
{
    this.assets = assets;
    this.session = session;
    session.Logic.AdlibSoundRequested += OnAdlibSound;
    stream = new StreamingAudioSource(synth.Generate);
    stream.Start();
}

/// <summary>Called once per tic: follows the session's music selection.</summary>
public void Update()
{
    synth.Volume = session.MusicVolume / 10.0f;

    var track = session.MusicTrack;
    if (track == currentTrack)
    {
        return;
    }

    currentTrack = track;
    if (track >= 0 && track < assets.Audio.MusicCount)
    {
        synth.PlayMusic(assets.Audio.GetMusic(track));
    }
    else
    {
        synth.StopMusic();
    }
}

public void Dispose()
{
    session.Logic.AdlibSoundRequested -= OnAdlibSound;
    stream.Dispose();
}
```

The fill callback's contract is stated on the method that implements it:

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Audio/WolfOplSynth.cs
/// <summary>
/// Fills an interleaved stereo float buffer. Fast and
/// allocation-free; called from the audio thread.
/// </summary>
public void Generate(Span<float> buffer)
{
    lock (gate)
    {
        // ...
    }
}
```

Doom.Brix drives the same shape from a SoundFont synthesizer, and shows the
lock-free variant of the handoff:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixMusic.cs
public CodeBrixMusic(Config config, GameContent content, string soundFontPath)
{
    // ...
    var settings = new SoundFontSynthesizerSettings(MusDecoder.SampleRate);
    settings.BlockSize = MusDecoder.BlockLength;
    settings.EnableReverbAndChorus = config.audio_musiceffect;
    synthesizer = new SoundFontSynthesizer(soundFontPath, settings);

    // Generously sized up front so the audio callback never allocates; grown in
    // the (unexpected) case of a larger device period.
    left = new float[8192];
    right = new float[8192];

    stream = new StreamingAudioSource(FillBuffer);
    currentBgm = Bgm.NONE;
}

private void FillBuffer(Span<float> buffer)
{
    var frames = buffer.Length / 2;

    var decoder = reserved;
    if (decoder == null)
    {
        buffer.Clear();
        return;
    }

    if (!ReferenceEquals(decoder, current))
    {
        synthesizer.Reset();
        current = decoder;
    }

    if (left.Length < frames)
    {
        left = new float[frames];
        right = new float[frames];
    }

    decoder.RenderWaveform(synthesizer, left.AsSpan(0, frames), right.AsSpan(0, frames));

    // ... interleave with a volume gain, clipped to [-1, 1] ...
}
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfMusic.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Audio/WolfOplSynth.cs`
- `Doom.Brix/src/libs/Doom.Brix.Game/CodeBrixMusic.cs`
- `Doom.Brix/src/libs/Doom.Brix.GameEngine/Audio/MusicDecoderFactory.cs`,
  `IMusicDecoder.cs`

**Sharp edges.**

- The fill callback runs on the audio thread and must be fast and
  allocation-free. Preallocate generously; grow only in the unexpected case of a
  larger device period.
- The control methods (play, stop, volume) are called from the game-loop thread.
  Hand state across with a short lock, or with volatile fields plus a
  `ReferenceEquals` check when you want no lock at all.
- Do the track-switch reset inside the callback, at the exact moment the new
  source takes over.
- Compare the requested track against the current one and do nothing when they
  match, so calling `Update()` every tic is free.
- Separate decoding from playback behind an interface and a factory that sniffs
  the source header. That is what lets the whole audio path be exercised
  headless in tests with no audio device.
- A synthesizer that needs a bundled data file should treat it as optional. See
  the packaging blueprint for how Doom.Brix substitutes a null implementation
  when its SoundFont is absent.

### Port an emulator or codec and keep it reviewable against the original

**When you want this.** You are translating a reference C implementation into C#
and need future readers to be able to diff it against the original.

**The MVVM shape.** Not a view-model concern. A file header that names the
upstream project and the exact upstream files, the license, and, most usefully,
a "Porting notes" block listing every deliberate deviation from the original's
shape.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Audio/NukedOpl3.cs
// Ported to C# for Wolfenstein.Brix from Nuked-OPL3
// (github.com/nukeykt/Nuked-OPL3, commit ...), files opl3.c and
// opl3.h. The port is intended to be bit-exact with the original for
// the same register stream (the OPL3_Generate4Ch and stereo-extension
// paths are omitted; the channel-sample-delay quirk is kept enabled,
// matching the original's defaults).
// ...
// Porting notes:
//  - Field, local and table names deliberately mirror the C source
//    (snake_case) to ease side-by-side review against opl3.c.
//  - The C "int16_t *mod" slot modulation-input pointer is replaced by
//    a (mod_source, mod_slot) pair read through SlotMod(); the C
//    "uint8_t *trem" pointer by the tremoloEnabled flag; and the
//    "int16_t *out[4]" channel mix pointers by outSlots (null meaning
//    the always-zero chip->zeromod).
//  - The channel-sample-delay quirk (OPL_QUIRK_CHANNELSAMPLEDELAY) is
//    compiled in, matching the C default when the stereo extension is
//    disabled.
```

**Where to look.**

- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Audio/NukedOpl3.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.GameEngine/Audio/WolfOplSynth.cs`
- `Wolfenstein.Brix/LICENSE_NukedOPL3.txt`
- `Wolfenstein.Brix/THIRD-PARTY-NOTICES.txt`

**Also shown by.** `Doom.Brix/src/libs/Doom.Brix.GameEngine/`, whose vendored
sources keep their upstream per-file copyright headers, with
`Doom.Brix/LICENSE_ManagedDoom.txt` beside them.

**Sharp edges.**

- Name the exact upstream files and revision in the header, so the port can be
  diffed years later.
- Suspending your own naming conventions inside a port is a legitimate trade.
  Say in the header that the naming is deliberate, so a reviewer does not "fix"
  it.
- Name what you left out, not just what you brought over.
- Be careful what you claim about verification. The port header says the port is
  *intended* to be bit-exact for the same register stream, and the OPL test file
  records that bit-exactness is verified separately by a port harness that is
  not in this repository. The tests here assert determinism and audible
  non-silence against the real data chunks.

## Testing

### Set up an xUnit v3 test project for a CodeBrix Platform application

**When you want this.** The standard test project shape for a
CodeBrix.Platform application: one test project per library under `src/libs`,
named `<Library>.Tests` and living under `tests/libs`.

**The MVVM shape.** Not applicable.

**Code.**

```xml
<!-- Adapted from CodeBrix.Samples.Gpl2/Doom.Brix/tests/libs/Doom.Brix.Assets.Tests/Doom.Brix.Assets.Tests.csproj
     (package versions elided; see the project's csproj) -->
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <!-- xUnit.net v3 test projects are self-executing binaries and
       must build as Exe; run via Microsoft.Testing.Platform,
       matching the CodeBrix family test convention. -->
  <OutputType>Exe</OutputType>
  <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="..\..\..\src\libs\Doom.Brix.Assets\Doom.Brix.Assets.csproj" />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="..." />
  <PackageReference Include="SilverAssertions..." Version="..." />
  <PackageReference Include="xunit.runner.visualstudio" Version="...">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="xunit.v3" Version="..." />
</ItemGroup>
```

```json
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/global.json
{
    "test": {
        "runner": "Microsoft.Testing.Platform"
    }
}
```

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Assets/InternalsVisibleTo.cs
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Doom.Brix.Assets.Tests")]
```

Test bodies follow the family style: a `<Class>Tests.cs` file per class,
snake_case method names, `//Arrange` / `//Act` / `//Assert` comments,
SilverAssertions, and `TestContext.Current.CancellationToken` passed to any API
that takes one.

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/tests/libs/Doom.Brix.Assets.Tests/DoomAssetPipelineTests.cs
[Fact]
public void VerifyDownloadedZip_rejects_a_wrong_file_as_a_verification_failure()
{
    //Arrange
    var wrongFile = Path.Combine(root, "doom19s.zip");
    File.WriteAllText(wrongFile, "definitely not the real shareware distribution");

    //Act
    Action act = () => DoomAssetPipeline.VerifyDownloadedZip(wrongFile, TestContext.Current.CancellationToken);

    //Assert
    act.Should().Throw<AssetPipelineException>()
        .Which.Stage.Should().Be(AssetStage.Verifying);
}
```

**Where to look.**

- `Doom.Brix/global.json` and `Wolfenstein.Brix/global.json`
- the csproj files under `Doom.Brix/tests/libs` and
  `Wolfenstein.Brix/tests/libs`
- `Doom.Brix/src/libs/Doom.Brix.Assets/InternalsVisibleTo.cs`

**Also shown by.**
`Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.Assets.Tests/Wolfenstein.Brix.Assets.Tests.csproj`
and `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Assets/InternalsVisibleTo.cs`.

**Sharp edges.**

- `OutputType` must be `Exe`; xUnit v3 test projects are self-executing
  binaries.
- `xunit.runner.visualstudio` is referenced with `PrivateAssets=all`.
- With the Microsoft.Testing.Platform runner selected in `global.json`, a
  solution-level `dotnet test` can report no tests depending on the SDK build.
  Running each test project's built executable directly always works.
- Put an `InternalsVisibleTo.cs` in each library that has internals worth
  testing, and only there. A library whose surface is entirely public does not
  need one, which is why `Wolfenstein.Brix.Settings` and `Doom.Brix.Settings`
  have none.
- Test classes that create temp folders implement `IDisposable` and delete the
  folder best-effort in `Dispose()`.

### Make data dependent tests explain themselves when the data is missing

**When you want this.** Some tests need files you cannot commit (licensed data,
a large model). A machine without them must never look green by accident, and
must never fail mysteriously either.

**The MVVM shape.** Not applicable. One small internal helper per test project
locates the data by walking up from the test binary, and decides the policy.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.GameEngine.Tests/TestWl1.cs
internal static class TestWl1
{
    public static string AssetsFolderPath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(
                    directory.FullName, "Downloaded", "Wolfenstein.Brix_assets");
                if (File.Exists(Path.Combine(candidate, "VSWAP.WL1")))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                "The Wolfenstein 3-D shareware data files were not found. Place the ... " +
                "shareware data files (AUDIOHED/AUDIOT/GAMEMAPS/MAPHEAD/VGADICT/VGAHEAD/" +
                "VGAGRAPH/VSWAP, all .WL1) at {repo root}/Downloaded/Wolfenstein.Brix_assets/ " +
                "- the folder is git-ignored - and run the tests again. The Wolfenstein.Brix " +
                "application's Assets Mode can download and install them for you.");
        }
    }
}
```

The softer variant, for data whose absence is not a coverage loss worth failing
over:

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.Assets.Tests/WolfensteinAssetPipelineTests.cs
static string RequireKnownGoodZip()
{
    var zipPath = FindKnownGoodZip();
    Assert.SkipWhen(zipPath == null,
        "The known-good 1wolf14.zip is not present under the repo's Downloaded folder.");
    return zipPath;
}
```

**Where to look.**

- `Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.GameEngine.Tests/TestWl1.cs`
- `Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.Assets.Tests/WolfensteinAssetPipelineTests.cs`
- the repository `.gitignore` (`Downloaded/*`)

**Also shown by.**
`Doom.Brix/tests/libs/Doom.Brix.GameEngine.Tests/TestWad.cs` (throws with
instructions) and
`Doom.Brix/tests/libs/Doom.Brix.Assets.Tests/DoomAssetPipelineTests.cs`
(`Assert.SkipWhen(...)`).

**Sharp edges.**

- Walk up from `AppContext.BaseDirectory` rather than hard-coding a relative
  path; the test binary's depth below the repository root changes with
  configuration and target framework.
- Probe for a specific *file* inside the candidate folder, not just the folder,
  so a half-populated folder is not accepted.
- Pick one policy per test project and write down why. These applications fail
  loudly for the engine data (those tests prove compatibility, and a silent skip
  would hide a regression) and skip quietly for the downloaded archive.
- Make the message name the exact path, say that the folder is git-ignored, and
  point at the application feature that can fetch the file.
- Keep the folder name in one place if you write more than one such helper. In
  both applications the two helpers look in differently named folders, so a
  machine set up for one is not automatically set up for the other.
- When a vendored component came with an upstream suite, a plain-text coverage
  note beside the tests recording what was ported, what needs data, and what was
  deliberately not ported is worth keeping
  (`Doom.Brix/tests/libs/Doom.Brix.GameEngine.Tests/UPSTREAM-TEST-COVERAGE.txt`).

### Serialize a test assembly that touches process static state

**When you want this.** Code under test has process-wide statics (a cache, a
singleton settings store, a global directory) that make parallel tests flaky.

**The MVVM shape.** Not applicable.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/tests/libs/Doom.Brix.GameEngine.Tests/TestParallelization.cs
using Xunit.Sdk;
using Xunit.v3;

// The vendored engine's DummyData caches (reached through GameContent.CreateDummy)
// are process-static and not thread-safe, so test classes that build dummy
// content must not run concurrently. Serialize the whole assembly ...
// matching the CodeBrix family's no-parallel test convention.
[assembly: Parallelization(Mode = ParallelMode.None)]
//was previously: [assembly: CollectionBehavior(DisableTestParallelization = true)]
```

The process-global settings store gets the same treatment from the other side:
initialized once per test process, into a throwaway folder.

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/tests/libs/Doom.Brix.Game.Tests/SqliteDoomStorageTests.cs
static SqliteDoomStorageTests()
{
    if (!SettingsService.IsInitialized)
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "DoomBrixSqliteStorageTests_" + Guid.NewGuid().ToString("N"));
        SettingsService.Initialize(dir);
    }
}
```

**Where to look.**

- `Doom.Brix/tests/libs/Doom.Brix.GameEngine.Tests/TestParallelization.cs`
- `Doom.Brix/tests/libs/Doom.Brix.Game.Tests/SqliteDoomStorageTests.cs`
- `Doom.Brix/tests/libs/Doom.Brix.GameEngine.Tests/FileDoomStorageTests.cs`

**Sharp edges.**

- The `//was previously:` comment records the attribute this replaced, which is
  the family convention when an API changes under you.
- Have each test in a store-backed class clear the keys it uses in its
  constructor, so method ordering does not matter even with one shared store.
- A test that mutates a global should say in a comment why that is safe: methods
  within a class run sequentially, and no other test reads it.

### Test a hardware input path behind the engine adapter interfaces

**When you want this.** Input handling that depends on a device you cannot
attach on a build machine.

**The MVVM shape.** Write your input reader against the engine's *interfaces*
(`IGamepadAdapter`, `IGamepadManager<T>`) rather than the concrete SDL2 types,
then supply fakes in tests. The host still passes the concrete manager at run
time; the interface exists for the test, not for a second production
implementation.

**Code.**

```csharp
// From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.Game.Tests/WolfGamepadInputTests.cs
/// <summary>
/// A stand-in for a connected controller. The engine's adapter is sealed around a live
/// SDL2 handle, so these drive the interface it implements instead.
/// </summary>
internal sealed class FakeGamepadAdapter : IGamepadAdapter
{
    private readonly HashSet<string> pressed = new HashSet<string>();

    public string GamepadId { get; set; } = "fake-0";
    public IReadOnlyCollection<string> PressedButtons => pressed;
    public GamepadStickState? LeftStick { get; set; }
    public GamepadStickState? RightStick { get; set; }
    public float LeftTrigger { get; set; }
    public float RightTrigger { get; set; }

    public void Press(string button) => pressed.Add(button);
    public void Release(string button) => pressed.Remove(button);
}

internal sealed class FakeGamepadManager : IGamepadManager<IGamepadAdapter>
{
    private readonly List<IGamepadAdapter> adapters = new List<IGamepadAdapter>();

    public IReadOnlyCollection<IGamepadAdapter> ConnectedAdapters => adapters;

    public void Update()
    {
        // The engine drives the real manager; nothing to refresh here.
    }

    public void Connect(IGamepadAdapter adapter) => adapters.Add(adapter);
    public void DisconnectAll() => adapters.Clear();
}
```

**Where to look.**

- `Wolfenstein.Brix/tests/libs/Wolfenstein.Brix.Game.Tests/WolfGamepadInputTests.cs`
- `Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/WolfGamepadInput.cs`

**Also shown by.** `Doom.Brix/tests/libs/Doom.Brix.Game.Tests`, which drives
`DoomGamepadInput` through a hand-written `IGamepadAdapter` fake for
weapon-slot cycling, unit-circle clamping and the menu repeat clock.

**Sharp edges.**

- The reader's constructor must take the *interface*, not the concrete manager;
  the SDL2 manager is sealed around a live handle and cannot be faked.
- Cover the "no controller" case explicitly, asserting that every field of the
  sample is the change-nothing value. That is what the keyboard-only path folds
  in on every tic, so it is the case most likely to regress unnoticed.

## Project layout, packaging and native assets

### Carry every shared package in one Core library and one runtime package per head

**When you want this.** You want to add a package once and have all six heads
get it, with no per-head fan-out to keep in sync.

**The MVVM shape.** Not a view-model concern; it is the packaging rule that
makes one shared Core library possible. Each head declares exactly one platform
runtime package and nothing else.

**Code.**

```xml
<!-- Adapted from CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.Core/Doom.Brix.Core.csproj
     (package IDs and versions elided; see the project's csproj) -->
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting" Version="..." />
  <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="..." />
  <!-- CodeBrix.Platform -->
  <PackageReference Include="..." Version="..." />
  <!-- The Open Sans font package -->
  <PackageReference Include="..." Version="..." />
  <!-- The embedded browser for Assets Mode: the Windows and macOS runtimes have a
       WebView2 built in; the Linux heads get theirs from this add-in (WPE WebKit) -->
  <PackageReference Include="..." Version="..." />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\libs\Doom.Brix.Assets\Doom.Brix.Assets.csproj" />
  <ProjectReference Include="..\libs\Doom.Brix.Game\Doom.Brix.Game.csproj" />
  <ProjectReference Include="..\libs\Doom.Brix.GameEngine\Doom.Brix.GameEngine.csproj" />
  <ProjectReference Include="..\libs\Doom.Brix.Settings\Doom.Brix.Settings.csproj" />
</ItemGroup>
```

```xml
<!-- Adapted from CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.MacOS/Doom.Brix.MacOS.csproj
     (package ID and version elided; see the project's csproj) -->
<!-- EXACTLY ONE platform head package; all other packages come from Doom.Brix.Core -->
<ItemGroup>
  <PackageReference Include="..." Version="..." />
</ItemGroup>
```

**Where to look.**

- `Doom.Brix/src/Doom.Brix.Core/Doom.Brix.Core.csproj`
- the six `Doom.Brix/src/Doom.Brix.<Head>/*.csproj` files

**Also shown by.**
`Wolfenstein.Brix/src/Wolfenstein.Brix.Core/Wolfenstein.Brix.Core.csproj` and
its six head csproj files.

**Sharp edges.**

- The rule is written into every head csproj as a comment, in capitals, because
  adding a second package there is the mistake it prevents.
- Add-ins that need per-head native assets (the WebView add-in here) are still
  referenced once, from Core.
- Only the library that actually uses an add-in should reference it. The game
  engine and SDL2 gamepad packages sit on the `.Game` library, not on Core.
- Keep the dependency direction one way: heads reference Core; Core references
  the `src/libs` projects; the libraries reference each other in one direction
  only. In both applications the parsing and settings libraries reference no UI
  and no platform at all, which is what makes them testable in isolation.

### Set the RootNamespace and conditional compilation defines CodeBrix Platform needs

**When you want this.** Any project that compiles against CodeBrix.Platform, and
any library that ends up with a transitive reference to it.

**The MVVM shape.** Not applicable; build rules.

**Code.**

The application's Core project and every head set the application namespace and
the platform's conditional-compilation symbols:

```xml
<!-- From CodeBrix.Samples.Gpl2/Wolfenstein.Brix/src/Wolfenstein.Brix.Core/Wolfenstein.Brix.Core.csproj -->
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>

  <!-- Match the namespace used by the app code -->
  <RootNamespace>Wolfenstein.Brix</RootNamespace>

  <!-- CodeBrix.Platform needs these for internal conditional compilation -->
  <DefineConstants>$(DefineConstants);HAS_CODEBRIX;HAS_CODEBRIX_WINUI</DefineConstants>
</PropertyGroup>
```

Any *other* library that picks up a (possibly transitive) platform reference
must take a **different** root namespace:

```xml
<!-- From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/Doom.Brix.Game.csproj -->
<!-- Keep a distinct RootNamespace (not the app's "Doom.Brix") so the transitive
     CodeBrix.Platform reference does not generate a duplicate GlobalStaticResources. -->
<RootNamespace>Doom.Brix.Game</RootNamespace>
```

`HAS_CODEBRIX` is also read by application code:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/Doom.Brix.UI/App.xaml.cs
#if HAS_CODEBRIX
    global::CodeBrix.Platform.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
```

**Where to look.**

- `Wolfenstein.Brix/src/Wolfenstein.Brix.Core/Wolfenstein.Brix.Core.csproj`
- `Doom.Brix/src/libs/Doom.Brix.Game/Doom.Brix.Game.csproj`
- `Doom.Brix/src/Doom.Brix.Core/Doom.Brix.Core.csproj`
- any of the six head csproj files in either application

**Sharp edges.**

- The failure mode for a shared root namespace is a build error about a
  duplicate `GlobalStaticResources` type, which does not obviously point at the
  root namespace.
- Libraries that do *not* compile against the platform (`Doom.Brix.Assets`,
  `Doom.Brix.GameEngine`, `Doom.Brix.Settings`, and their Wolfenstein.Brix
  counterparts) deliberately set neither the defines nor a root namespace. Keep
  the build rules where they are needed and nowhere else.
- `EnableWindowsTargeting` on the `net10.0-windows` head is what lets the whole
  solution restore and build on Linux and macOS.

### Ship bundled assets and license notices into every head output

**When you want this.** Your application needs a data file at run time (a
SoundFont, a model, a dictionary), or incorporates vendored code whose notices
must sit next to the binaries, and you do not want the item repeated in six
csproj files.

**The MVVM shape.** Not a view-model concern. `Content` items with
`CopyToOutputDirectory`, and a `Link` into a well-known output subfolder,
declared once in the library nearest the code they cover. Content items flow
transitively through `ProjectReference`, so declaring them once puts them in
every head's output. Consuming code resolves the file under
`AppContext.BaseDirectory` and degrades gracefully when it is absent.

**Code.**

```xml
<!-- From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/Doom.Brix.Game.csproj -->
<!-- The GPLv2 TimGM6mb SoundFont (by Tim Brechbill) ships with the application for
     music playback; without it the game runs music-less. Content items marked
     copy-to-output flow transitively through ProjectReference, so every head's
     output folder gets ThirdPartyAssets/TimGM6mb.sf2 automatically. The
     third-party license texts and the repo's notices file ride along into the
     same output folder, so a distributed build carries its own compliance set. -->
<ItemGroup>
  <Content Include="ThirdPartyAssets\TimGM6mb.sf2">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="..\..\..\LICENSE_ManagedDoom.txt" Link="ThirdPartyAssets\LICENSE_ManagedDoom.txt">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="..\..\..\LICENSE_TimGM6mb.txt" Link="ThirdPartyAssets\LICENSE_TimGM6mb.txt">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="..\..\..\THIRD-PARTY-NOTICES.txt" Link="ThirdPartyAssets\THIRD-PARTY-NOTICES.txt">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

The relative paths reach up out of `src/libs/<Library>/` to the *application*
folder. Each application in this repository owns its own
`THIRD-PARTY-NOTICES.txt` and its own `LICENSE_*.txt` files, sitting beside its
`src/` and `tests/` folders, so a `Content` item never has to leave the
application it belongs to. The repository root keeps a short notices file that
simply points at the per-application ones.

The run-time side treats a bundled data file as optional:

```csharp
// From CodeBrix.Samples.Gpl2/Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs
// Music needs the shipped SoundFont; when the file is missing (e.g. a
// hand-trimmed deployment) the core substitutes its Null implementation
// and the game runs on without music.
var soundFontPath = Path.Combine(AppContext.BaseDirectory, "ThirdPartyAssets", "TimGM6mb.sf2");
music = File.Exists(soundFontPath) ? new CodeBrixMusic(config, content, soundFontPath) : null;
```

**Where to look.**

- `Doom.Brix/src/libs/Doom.Brix.Game/Doom.Brix.Game.csproj`
- `Doom.Brix/src/libs/Doom.Brix.Game/DoomGameHost.cs`
- `Doom.Brix/THIRD-PARTY-NOTICES.txt`,
  `Doom.Brix/LICENSE_ManagedDoom.txt`, `Doom.Brix/LICENSE_TimGM6mb.txt`

**Also shown by.**
`Wolfenstein.Brix/src/libs/Wolfenstein.Brix.Game/Wolfenstein.Brix.Game.csproj`,
which links `Wolfenstein.Brix/LICENSE_CSharpWolfenstein.txt`,
`LICENSE_Wolf3D-iOS.txt`, `LICENSE_NukedOPL3.txt` and
`Wolfenstein.Brix/THIRD-PARTY-NOTICES.txt` into the same `ThirdPartyAssets\`
output folder.

**Sharp edges.**

- The `Link` metadata is what puts a file from outside the project directory
  into a named subfolder of the output rather than at its root.
- `PreserveNewest` rather than `Always` avoids copying on every incremental
  build.
- Declare the items once, in the library closest to the code they cover, and let
  the transitive flow through `ProjectReference` reach all six heads.
- Keep the notices file inside the application folder. A `Content` path that
  reaches out of the application to a shared file breaks silently when either
  end moves, and the compliance copy stops shipping with no build error.
- Give a bundled data file an existence check at the call site and a degraded
  path, so a hand-trimmed deployment loses a feature rather than crashing.

## Not yet covered by a sample

Neither application in this repository demonstrates the following, so look to
the other sample repositories for them:

- Multi-page navigation. Both applications navigate a `Frame` once at startup
  and then switch modes with visibility bindings; there is no second page type,
  no back stack, and no navigation parameter passing.
- `[AffectsCommands]` and `CanExecute` refresh. Neither application has a
  command whose availability changes, so nothing needs refreshing.
- `InvokeOnMainThread`. Cross-thread marshaling is done with the canvas's
  `DispatcherQueue.TryEnqueue(...)` and with `Progress<T>`'s captured
  synchronization context instead.
- `SimpleMessaging`, `SimpleEnum` and `SimpleOsInfo` do not appear anywhere.
- Resolving a service from `SimpleServiceResolver` in a view model. The
  registration lambda is empty in both applications and each view model is
  instantiated by XAML, so nothing actually goes through the container.
- Saving a file through a native save dialog. Only the folder picker is used.
- Binding a collection to a list control (`ListView`, `ItemsRepeater`), item
  templates, and selection.
- Custom or templated controls of your own, and custom drawing on a Skia canvas
  outside the engine's game surface.
- Theming, light and dark switching, and localization.
- A REST or JSON web API client. The only network access is a single file
  download and the embedded browser.
- Document generation (PDF or otherwise), printing, rich text editing, camera
  capture and media playback.
- Unit tests for a view model. Every test project here targets a library under
  `src/libs`; neither Core project has a test project.
