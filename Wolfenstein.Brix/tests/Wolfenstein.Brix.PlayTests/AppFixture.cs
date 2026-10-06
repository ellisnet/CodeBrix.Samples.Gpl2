using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Platform.PlayTest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Wolfenstein.Brix.Assets;
using Wolfenstein.Brix.Settings;
using Wolfenstein.Brix.ViewModels;
using Wolfenstein.Brix.Views;
using Xunit;

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Wolfenstein.Brix.PlayTests;

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "Wolfenstein application";
}

public sealed class AppFixture : IAsyncLifetime
{
    private readonly AssetsServer _server = new();
    private readonly List<string> _downloads = new();
    private bool _recording;
    public PlayTestApplication Application { get; private set; }
    public MainPage View { get; private set; }
    public MainViewModel Model => (MainViewModel)View.DataContext;
    public string DataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "TestResults", "PlayTestData", Guid.NewGuid().ToString("N"));
    // Game data the application extracts goes to the system temporary folder, never below the repository.
    public string ScratchDirectory { get; } = Path.Combine(Path.GetTempPath(), "Wolfenstein.Brix.PlayTests", Guid.NewGuid().ToString("N"));
    public string TestDirectory { get; private set; }
    public Uri Origin => _server.Origin;
    public string StartUrl => new Uri(Origin, "game/Wolfenstein_3D.html").AbsoluteUri;
    public ConcurrentQueue<string> Requests => _server.Requests;
    // Every address the page asked its browser to open. Only loopback fixture pages are really opened.
    public List<string> Navigations { get; } = new();
    // Pages the current page's real browser finished loading.
    public int CompletedNavigations { get; private set; }

    // The git-ignored Downloaded/Wolfenstein.Brix_assets folder of this repository, found by walking up from
    // the test binary as Wolfenstein.Brix.GameEngine.Tests does; null on a machine without the game data.
    public static string GameDataDirectory { get; } = FindGameData();
    public static bool HasGameData => GameDataDirectory != null;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(DataDirectory);
        _server.Start();
        // The application keeps a store that is already open, so it never touches the user's settings.
        SettingsService.Initialize(Path.Combine(DataDirectory, "settings"));
        Application = await PlayTestApplication.LaunchAsync(() => new App(services =>
            services.AddSingleton(new AssetsBrowserOptions { StartUrl = StartUrl })), new()
        {
            ConfigurationAssembly = typeof(AppFixture).Assembly,
            ArtifactsDirectory = Path.Combine(DataDirectory, "failures"),
        });
    }

    public async Task ResetAsync(ScreenOrientation? orientation, string assetsFolder = null)
    {
        TestDirectory = Path.Combine(DataDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TestDirectory);
        Application.FilePickers.Clear();
        _server.Requests.Clear();
        SettingsService.Set(MainViewModel.AssetsFolderKey, assetsFolder);
        await LoadPageAsync(orientation);
    }

    // A new page reads the remembered folder like a new session of the application.
    public async Task LoadPageAsync(ScreenOrientation? orientation = null)
    {
        Navigations.Clear();
        CompletedNavigations = 0;
        _recording = false;
        // Replacing the page disposes its view model, which shuts down a running game host.
        await Application.Page.SetContentAsync(() =>
        {
            View = new MainPage();
            ((WebView2)View.FindName("Browser")).NavigationCompleted += (_, _) => CompletedNavigations++;
            return View;
        }, orientation);
        foreach (var download in _downloads) DeleteDirectory(Path.GetDirectoryName(download));
        _downloads.Clear();
        if (await Application.EvaluateAsync(() => Model.BrowserAreaVisibility == Visibility.Visible)) await WaitForStartPageAsync();
    }

    // The page wires its browser asynchronously, once the browser area is shown. Record its
    // navigations from then on and let only the loopback fixture through, so no test can open a
    // real website. The start page itself comes from AssetsBrowserOptions.
    public async Task WaitForStartPageAsync()
    {
        await Application.WaitForAsync(() => Model.NavigateToUrl != null, ready => ready, description: "browser bridge");
        if (!_recording)
        {
            _recording = true;
            await Application.EvaluateAsync(() =>
            {
                var navigate = Model.NavigateToUrl;
                Model.NavigateToUrl = url =>
                {
                    Navigations.Add(url);
                    if (url.StartsWith(Origin.AbsoluteUri, StringComparison.Ordinal)) navigate(url);
                };
            });
        }
        // A finishing page load replaces the address bar text, so let the start page settle first.
        await Application.WaitForAsync(() => CompletedNavigations, count => count > 0, description: "start page");
    }

    // Starts an accepted asset download the way the browser's DownloadStarting handler does.
    public async Task<string> AcceptDownloadAsync()
    {
        var target = await Application.EvaluateAsync(() =>
        {
            Model.HandleDownloadStarting(new Uri(Origin, "files/1wolf14.zip").AbsoluteUri, "1wolf14.zip", out var path);
            return path;
        });
        if (target != null) _downloads.Add(target);
        return target;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Application != null)
            {
                try { await Application.EvaluateAsync(() => (View?.DataContext as IDisposable)?.Dispose()); }
                finally { await Application.DisposeAsync(); }
            }
        }
        finally
        {
            SettingsService.Shutdown();
            _server.Dispose();
            foreach (var download in _downloads) DeleteDirectory(Path.GetDirectoryName(download));
            DeleteDirectory(ScratchDirectory);
            // Remove the shared parent too once no other run is using it.
            try { Directory.Delete(Path.GetDirectoryName(ScratchDirectory)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public object FocusedElement() => FocusManager.GetFocusedElement(View.XamlRoot);

    private static void DeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string FindGameData()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Downloaded", "Wolfenstein.Brix_assets");
            if (Directory.Exists(candidate) && WolfensteinAssetPipeline.VerifyInstalledAssets(candidate)) return candidate;
        }
        return null;
    }
}

[Collection(AppCollection.Name)]
public abstract class WolfensteinTest(AppFixture fixture) : PageTest(fixture.Application), IAsyncLifetime
{
    protected AppFixture Fixture { get; } = fixture;
    protected MainViewModel Model => Fixture.Model;
    public virtual async ValueTask InitializeAsync()
    {
        var test = (Xunit.v3.IXunitTest)TestContext.Current.Test;
        test.Traits.TryGetValue(PlayTestOrientationAttribute.CaseTraitName, out var orientations);
        await Fixture.ResetAsync(PlayTestOrientationAttribute.Resolve(test.TestMethod.Method, orientations));
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    protected Locator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
    protected Locator Dialog => Page.GetByRole(AriaRole.Dialog);
    protected Locator ChooseFolder => Page.GetByTestId("ChooseFolderButton");
    protected Locator FolderButton => Page.GetByTestId("AssetsFolderButton");
    protected Locator Address => Page.GetByTestId("AddressBox");
    protected Locator Go => Page.GetByTestId("GoButton");
    protected Locator Browser => Page.GetByTestId("Browser");
    protected Locator Overlay => Page.GetByTestId("DownloadOverlay");
    protected Locator Canvas => Page.GetByTestId("GameCanvas");

    protected Task WaitAsync<T>(Func<T> read, Func<T, bool> ready) => Fixture.Application.WaitForAsync(read, ready);
    protected string TestPath(string name) => Path.Combine(Fixture.TestDirectory, name);

    protected async Task<string> ChooseFolderAsync(Locator button, string name = "assets")
    {
        var folder = TestPath(name);
        Directory.CreateDirectory(folder);
        Fixture.Application.FilePickers.EnqueueFolder(folder);
        await button.ClickAsync();
        await Expect(FolderButton).ToHaveTextAsync(folder);
        await Fixture.WaitForStartPageAsync();
        return folder;
    }

    protected async Task CloseDialogAsync()
    {
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "OK", Exact = true }).ClickAsync();
        await Expect(Dialog).ToHaveCountAsync(0);
    }
}

// The embedded browser's start page and downloads come from here. No Internet access is needed.
// Binding port zero avoids races between test processes choosing an available port.
internal sealed class AssetsServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private Task _loop;
    public Uri Origin { get; private set; }
    public ConcurrentQueue<string> Requests { get; } = new();
    public void Start()
    {
        _listener.Start();
        Origin = new Uri("http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/");
        _loop = ServeAsync();
    }
    private async Task ServeAsync()
    {
        var requests = new List<Task>();
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                // WebKit can open a speculative connection without sending a request.
                requests.Add(ServeClientAsync(client));
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
        finally { await Task.WhenAll(requests); }
    }
    private async Task ServeClientAsync(TcpClient client)
    {
        using (client)
        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            var line = await reader.ReadLineAsync(_stop.Token);
            if (string.IsNullOrEmpty(line)) return;
            var target = line.Split(' ')[1];
            Requests.Enqueue(target);
            while (true)
            {
                var requestHeader = await reader.ReadLineAsync(_stop.Token);
                if (requestHeader == null) return;
                if (requestHeader.Length == 0) break;
            }
            string header;
            byte[] body;
            if (target.StartsWith("/files/", StringComparison.Ordinal))
            {
                // Not the authentic 1wolf14.zip: an accepted download fails verification.
                body = Encoding.ASCII.GetBytes("This is not the Wolfenstein 3D shareware archive.");
                header = "HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Disposition: attachment; filename=\""
                    + Path.GetFileName(target) + "\"\r\n";
            }
            else
            {
                var html = "<!doctype html><html><head><meta charset='utf-8'><title>Offline classicdosgames fixture</title>"
                    + "<style>body{font:24px sans-serif;margin:48px;background:#fff;color:#202122}a{color:#36c}</style></head><body>"
                    + "<h1>Wolfenstein 3D</h1><p>Downloads:</p>"
                    + "<p><a id='asset' href='/files/1wolf14.zip'>1wolf14.zip</a></p>"
                    + "<p><a id='other' href='/files/setup.exe'>setup.exe</a></p></body></html>";
                body = Encoding.UTF8.GetBytes(html);
                header = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n";
            }
            header += "Content-Length: " + body.Length + "\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header), _stop.Token);
            await stream.WriteAsync(body, _stop.Token);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        // Page replacement can abandon a connection while its response is being sent.
        catch (IOException) { }
        catch (SocketException) { }
    }
    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        try { _loop?.GetAwaiter().GetResult(); }
        finally { _stop.Dispose(); }
    }
}
