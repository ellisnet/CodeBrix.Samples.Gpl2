# CodeBrix.Samples.Gpl2

This repository holds complete, runnable reference applications for the CodeBrix
family of .NET libraries. Each one is a real application rather than a snippet:
it starts, it does its job, it persists what it should, and it ships the same
way an application you write would ship. They exist to show how the libraries
are meant to be consumed from a CodeBrix.Platform application, so you can open
the file that does the thing you need and copy the shape of it.

This is the classic-game repository: both applications are games, and both run
on CodeBrix.Platform and the CodeBrix.Platform.GameEngine library, which
supplies the fixed-rate game loop, the software framebuffer presenter and the
game surface canvas, the input pollers, and the game audio channels. There is
one codebase per game, and that codebase builds all six CodeBrix.Platform heads:
Win32Skia, WinWpfSkia, LinuxX11, LinuxWayland, LinuxFrameBuffer and MacOS. Each
head is a thin project that supplies only its platform plumbing; one shared
XAML UI and one shared view model drive every head. Libraries are consumed as
packages, never as source references, so each application folder stands on its
own. The engine internals are a plain object graph on the game-loop thread, but
the shell around each game (startup, Assets Mode, the download pipeline,
settings and the page itself) is ordinary MVVM: view models derived from
`SimpleViewModel` own the state and the commands, code-behind stays thin, and
platform capabilities reach the view model through interfaces.

Everything in this repository is licensed under the GNU General Public License,
version 2.

Every application folder has a `README.md`, the detailed guide to that
application, and a `THIRD-PARTY-NOTICES.txt`, its attribution record.
[BLUEPRINTS.md](BLUEPRINTS.md) at the root collects the how-tos mined from all
of the applications here.

## The applications

| Application | What it is | Headline CodeBrix libraries |
| --- | --- | --- |
| [Doom.Brix](Doom.Brix/README.md) | Plays the original id Software DOOM shareware episode as a CodeBrix.Platform desktop application, on an adapted fork of the managed-doom engine with platform backends for video, sound, music and input. | CodeBrix.Platform, CodeBrix.Platform.GameEngine (with its SDL2 add-on), the CodeBrix.Platform.WebView add-in, the CodeBrix.Platform.AppSettings add-in, CodeBrix.Audio, CodeBrix.Compression |
| [Wolfenstein.Brix](Wolfenstein.Brix/README.md) | A playable recreation of id Software's Wolfenstein 3D shareware episode, from the original game logic and data formats, with a bit-exact port of the Nuked OPL3 FM synthesizer for its music and effects. | CodeBrix.Platform, CodeBrix.Platform.GameEngine (with its SDL2 add-on), the CodeBrix.Platform.WebView add-in, the CodeBrix.Platform.AppSettings add-in, CodeBrix.Compression |

## Blueprints

[BLUEPRINTS.md](BLUEPRINTS.md) is a set of how-tos for building
CodeBrix.Platform applications, mined from the applications in this repository.
Each blueprint says when you would want it, shows the real code, and names the
application and file it came from. They are written in the MVVM shape
CodeBrix.Platform is built for: view models derived from `SimpleViewModel` hold
state as bound properties and behavior as `SimpleCommand` commands, with
`[AffectsCommands]` to refresh `CanExecute` and `InvokeOnMainThread` to touch
bound state from another thread; code-behind constructs or resolves the view
model, sets `DataContext` and forwards platform plumbing in a line or two;
platform capabilities arrive through bridge interfaces the page or head
supplies, and the view model degrades gracefully when a head supplies none;
services sit behind interfaces registered with `SimpleServiceResolver` at
startup; and heavy work runs off the UI thread and marshals its results back.

## Building and testing

The .NET 10 SDK is the only prerequisite for building. Each application has its
own solution file in its own folder, `Doom.Brix/Doom.Brix.slnx` and
`Wolfenstein.Brix/Wolfenstein.Brix.slnx`; open the one for the application you
are working on. Both solutions restore and build with the plain .NET SDK on
Linux, macOS and Windows, because the WinWpfSkia head sets
`EnableWindowsTargeting`: the Windows-targeting heads compile anywhere, but they
run only on Windows. Any system library an application wants at run time (for
example the SDL2 runtime for game-controller support on Linux) is stated in that
application's own README.

Neither game ships its game data. On first launch each one opens its Assets
Mode: an embedded browser that downloads the original shareware release,
verifies it against known checksums, and unpacks it into a folder you choose.
Every later launch re-verifies that folder and boots straight into the game.

The test projects use the Microsoft.Testing.Platform runner, selected by each
application's `global.json`, and every test project builds as a self-executing
binary. Under that runner selection a plain solution-level `dotnet test` can
report that zero tests ran, so build the solution and then run each test
project's own executable from its output folder, which always works. The
data-dependent tests expect the shareware files under `Downloaded/<App>_assets/`
at the repository root; `Downloaded/` is gitignored and is never committed. When
the files are absent those tests either fail or skip with a message naming the
exact path and pointing at the application's Assets Mode; the policy differs by
suite, and each application's README gives it exactly. The tests that need no
data always run.

## Third-party notices

Each application folder carries its own `THIRD-PARTY-NOTICES.txt`, the
authoritative record of the third-party code, data and assets that application
adapts, bundles or uses at run time, together with the `LICENSE_*.txt` texts of
the components it adapts. Each game copies those files into a `ThirdPartyAssets`
folder in its build output, so a shipped build carries its own attribution. The
root [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) is a pointer to the
per-application files. Third-party code dependencies are consumed as packages,
and each package carries its own notices.

## License

Everything in this repository is licensed under the GNU General Public License,
version 2 (see [LICENSE](LICENSE)).

Copyright (c) 2026 Jeremy Ellis and contributors
