# Wolfenstein.Brix.PlayTests

Serialized xUnit v3 UI tests run the shared Wolfenstein.Brix application XAML and its real view model through CodeBrix.Platform.PlayTest, with the real embedded browser. Assertions use SilverAssertions and PlayTest's retrying `Expect(...)`. Nullable and implicit usings are disabled explicitly in the project.

From `Wolfenstein.Brix/`:

```bash
dotnet test --project tests/Wolfenstein.Brix.PlayTests/Wolfenstein.Brix.PlayTests.csproj -c Release
```

Visible landscape preview (the default orientation, with 250 ms between actions):

```bash
CODEBRIX_PLAYTEST_HEADED=1 \
  dotnet test --project tests/Wolfenstein.Brix.PlayTests/Wolfenstein.Brix.PlayTests.csproj -c Release
```

Visible portrait/dark preview:

```bash
CODEBRIX_PLAYTEST_HEADED=1 CODEBRIX_PLAYTEST_ORIENTATION=portrait \
CODEBRIX_PLAYTEST_THEME=dark \
  dotnet test --project tests/Wolfenstein.Brix.PlayTests/Wolfenstein.Brix.PlayTests.csproj -c Release
```

Set `CODEBRIX_PLAYTEST_SLOWMO` to override the delay, including `0` for full speed. Headless runs default to zero. The complete set of PlayTest switches, environment variables and project properties is documented in the PlayTest package's `AGENT-README.txt`.

Coverage: first-run folder setup, scripted folder picks and cancellation, the remembered folder restored or asked for again, changing the folder, the start page, address normalization with GO and Enter, invalid addresses, the download policy (unrecognized downloads, the accepted download's "GET PSYCHED!" overlay and progress, a second download while one runs, verification and download failures), real links in the embedded browser starting downloads, the address bar following finished navigations, the application's own palette under either OS theme, and portrait layout.

Game Mode coverage is opt-in: it needs the verified shareware `.WL1` files (and, for the install test, `1wolf14.zip`) in this repository's git-ignored `Downloaded/Wolfenstein.Brix_assets` folder, found by walking up from the test binary. Without them those tests skip. With them they cover starting in Game Mode from a verified folder, picking such a folder, the canvas taking and regaining keyboard focus, non-blank game frames, and the known-good zip installing into a fresh folder. The game runs live with sound, so these tests check mode, focus and pixels only, and never choose the game's Quit.

## Isolation and limits

One serialized application fixture launches the application once with an `AssetsBrowserOptions` start page on a loopback HTTP server, so the real WPE WebKit browser on Linux, Edge WebView2 on Windows or WKWebView on macOS never opens a real website. The fixture also records every address the page asks its browser to open and lets only loopback addresses through. Settings use an isolated `settings.sqlite` below the test output's `TestResults/PlayTestData`; the suite never uses your normal Wolfenstein.Brix settings. Each test gets a fresh page; replacing the page disposes its view model, which shuts down a running game host. Folders the install test extracts game data into live in the system temporary folder and are removed when the run ends.

The download-policy tests that need no browser call the view model's download handlers directly, as the page's `DownloadStarting` handler does. Failure screenshots remain under that run's `failures` directory.
