# CodeBrix.Samples.Gpl2

Classic-game sample applications for the CodeBrix.Platform application
framework. Everything in this repository is licensed under the GNU
General Public License version 2 (see [LICENSE](LICENSE)); the
third-party components each application incorporates are itemized in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Both games run on **CodeBrix.Platform** (the cross-platform XAML
application framework) and **CodeBrix.Platform.GameEngine** (its game
add-on: a fixed-rate game loop, a software-framebuffer presenter, a
cross-platform keyboard/input pump, and game audio channels). One
codebase per game builds six desktop heads: Linux X11, Linux Wayland,
Linux frame buffer, macOS, Windows Win32-Skia and Windows WPF-Skia.

Neither application ships any game data. On first launch each opens
its **Assets Mode** — an embedded browser that lets you download the
original shareware release from the web; the download is verified
against known checksums and unpacked into a folder you choose, and the
game then boots from those files on every later launch.

## Doom.Brix

The 1993 id Software DOOM (shareware episode 1, *Knee-Deep in the
Dead*), running on an adapted fork of the
[managed-doom](https://github.com/sinshu/managed-doom) engine — a
faithful C# translation of Linux Doom — with CodeBrix backends for
video, sound, music and input. Music plays through the included
MeltySynth SoundFont synthesizer adaptation and the bundled TimGM6mb
SoundFont. The game's
35 Hz logic, demo-compatibility behavior and save logic follow the
original; configuration and save games persist in the application's
single portable `settings.sqlite`.

## Wolfenstein.Brix

The 1992 id Software Wolfenstein 3D (shareware episode 1, *Escape from
Wolfenstein*), built from three ingredients: the
[csharp-wolfenstein](https://github.com/JamesRandall/csharp-wolfenstein)
raycaster (forked/adapted, MIT), a fresh translation of the game logic from
id's GPL [Wolf3D iOS](https://github.com/id-Software/Wolf3D-iOS)
source release, and a bit-exact C# port of the
[Nuked OPL3](https://github.com/nukeykt/Nuked-OPL3) FM synthesizer for
the AdLib music and effects. The game runs at the original 70 Hz tic
rate with the original's fixed-point math, enemy state machines,
damage tables and random-number table; digitized sounds play from the
shareware VSWAP, music and AdLib effects from AUDIOT through the OPL
emulator. Title screen, menus, difficulty select, intermissions,
high scores and eight save slots are all recreated from the shareware's
own graphics; saves, configuration and high scores persist in
`settings.sqlite`.

## Building

Each game is a self-contained solution (`Doom.Brix/Doom.Brix.slnx`,
`Wolfenstein.Brix/Wolfenstein.Brix.slnx`) that restores and builds
with the plain .NET SDK on Linux, macOS and Windows:

```
dotnet build Wolfenstein.Brix/Wolfenstein.Brix.slnx
dotnet run --project Wolfenstein.Brix/src/Wolfenstein.Brix.LinuxX11
```

The test suites run with `dotnet test` per test project; the
data-dependent tests expect the shareware files in
`Downloaded/<App>_assets/` (never committed) and fail with
instructions when they are absent.
