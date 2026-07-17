using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Platform.GameEngine.Host.Rendering;
using CodeBrix.Platform.Simple;
using Doom.Brix.Assets;
using Doom.Brix.Assets.Models;
using Doom.Brix.Game;
using Doom.Brix.Settings;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;

namespace Doom.Brix.ViewModels;

/// <summary>
/// Drives the whole Doom.Brix main page. The page has two modes, toggled by
/// visibility: <b>Assets Mode</b> (pick an assets folder, then browse the web
/// in the embedded WebView to download doom19s.zip — the one file the browser
/// is allowed to download, which the .Assets pipeline verifies and extracts)
/// and <b>Game Mode</b> (the game canvas: <see cref="DoomGameHost"/> runs the
/// vendored managed-doom engine against the verified assets folder). Startup
/// goes straight to Game Mode when the settings.sqlite-remembered folder
/// already holds a verified DOOM1.WAD.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel
{
    /// <summary>The settings.sqlite key holding the user's chosen assets folder.</summary>
    public const string AssetsFolderKey = "Doom.Brix.Settings.AssetsFolder";

    private string _assetsFolder;
    private bool _isGameMode;
    private GameSurfaceCanvas _gameCanvas;
    private DoomGameHost _gameHost;
    private volatile bool _canvasHasFocus;
    private bool _isDownloading;
    private double _downloadProgress;
    private string _downloadStageText = string.Empty;
    private string _addressText = DoomAssetCatalog.DefaultBrowseUrl;
    private string _currentPageUrl = DoomAssetCatalog.DefaultBrowseUrl;
    private bool _hasNavigated;

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

    #region | Bindable properties |

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

    /// <summary>The Game Mode view's visibility.</summary>
    public Visibility GameModeVisibility => GetVisibility(IsGameMode);

    /// <summary>The Assets Mode view's visibility.</summary>
    public Visibility AssetsModeVisibility => GetVisibility(!IsGameMode);

    /// <summary>Whether the user has a (still existing) assets folder chosen.</summary>
    public bool HasAssetsFolder => !string.IsNullOrWhiteSpace(_assetsFolder);

    /// <summary>The folder button's caption: an invitation, or the chosen path.</summary>
    public string AssetsFolderLabel => HasAssetsFolder ? _assetsFolder : "Choose assets folder…";

    /// <summary>The folder-selection setup panel's visibility (Assets Mode, no folder yet).</summary>
    public Visibility FolderSetupVisibility => GetVisibility(!IsGameMode && !HasAssetsFolder);

    /// <summary>The browser area's visibility (Assets Mode with a folder chosen).</summary>
    public Visibility BrowserAreaVisibility => GetVisibility(!IsGameMode && HasAssetsFolder);

    /// <summary>The address bar's text.</summary>
    public string AddressText
    {
        get => _addressText;
        set => SetProperty(ref _addressText, value);
    }

    /// <summary>Whether the asset download/verify/extract pipeline is running.</summary>
    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            SetProperty(ref _isDownloading, value);
            NotifyPropertyChanged(nameof(DownloadOverlayVisibility));
        }
    }

    /// <summary>The download progress overlay's visibility.</summary>
    public Visibility DownloadOverlayVisibility => GetVisibility(IsDownloading);

    /// <summary>The current pipeline stage, e.g. <c>Downloading doom19s.zip…</c>.</summary>
    public string DownloadStageText
    {
        get => _downloadStageText;
        private set => SetProperty(ref _downloadStageText, value);
    }

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

    private void NotifyModeProperties()
    {
        NotifyPropertyChanged(nameof(GameModeVisibility));
        NotifyPropertyChanged(nameof(AssetsModeVisibility));
        NotifyPropertyChanged(nameof(HasAssetsFolder));
        NotifyPropertyChanged(nameof(AssetsFolderLabel));
        NotifyPropertyChanged(nameof(FolderSetupVisibility));
        NotifyPropertyChanged(nameof(BrowserAreaVisibility));
    }

    #endregion

    #region | Commands and their implementations |

    private SimpleCommand _pickFolderCommand;

    /// <summary>Opens the folder picker to choose where the game assets live.</summary>
    public SimpleCommand PickFolderCommand => _pickFolderCommand ??=
        new SimpleCommand((Func<object, Task>)(_ => PickFolderAsync()));

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

    private SimpleCommand _goCommand;

    /// <summary>Navigates the embedded browser to the address bar's URL.</summary>
    public SimpleCommand GoCommand => _goCommand ??= new SimpleCommand(NavigateToAddress);

    private void NavigateToAddress()
    {
        var url = NormalizeUrl(AddressText);
        if (url == null) { return; }

        _hasNavigated = true;
        NavigateToUrl?.Invoke(url);
    }

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

    #endregion

    #region | Assets folder |

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

    #endregion

    #region | Game hosting |

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

    #endregion

    #region | Embedded browser bridge (wired by MainPage code-behind) |

    /// <summary>Navigates the embedded WebView; set by the page's code-behind.</summary>
    public Action<string> NavigateToUrl { get; set; }

    /// <summary>Called by the code-behind once the WebView bridge is wired.</summary>
    public void OnBrowserReady() => EnsureBrowserStarted();

    private void EnsureBrowserStarted()
    {
        if (_hasNavigated || IsGameMode || !HasAssetsFolder || NavigateToUrl == null) { return; }

        _hasNavigated = true;
        NavigateToUrl(DoomAssetCatalog.DefaultBrowseUrl);
    }

    /// <summary>Tracks the page the user is on (for the address bar and the download Referer).</summary>
    public void SetCurrentBrowserUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) { return; }

        _currentPageUrl = url;
        AddressText = url;
    }

    /// <summary>
    /// The Assets Mode download policy, called for every navigation the
    /// WebView starts. Returns true when the navigation must be CANCELED:
    /// the permitted asset file (handed to the .Assets pipeline instead) and
    /// any other download-looking link (refused with an explanation).
    /// Ordinary page navigation returns false and passes through.
    /// </summary>
    public bool HandleNavigationStarting(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) { return false; }

        if (AssetUrlClassifier.IsAssetFileUrl(url))
        {
            if (!IsDownloading)
            {
                _ = InstallAssetsAsync(url);
            }
            return true;
        }

        if (AssetUrlClassifier.IsDownloadLikeUrl(url))
        {
            _ = ShowUnrecognizedDownloadCanceledAsync();
            return true;
        }

        return false;
    }

    #endregion

    #region | Asset installation pipeline |

    private async Task InstallAssetsAsync(string url)
    {
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStageText = $"Downloading {DoomAssetCatalog.AssetFileName}…";
        try
        {
            var progress = new Progress<AssetProgress>(OnAssetProgress);
            await DoomAssetPipeline.InstallFromUrlAsync(url, _currentPageUrl, _assetsFolder,
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
            await ShowInstallFailedAsync(AssetStage.Downloading);
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

    private async Task ShowUnrecognizedDownloadCanceledAsync()
    {
        using var alert = CreateDialog(
            "The attempt to download an unrecognized file was canceled, "
            + $"please download a file named '{DoomAssetCatalog.AssetFileName}'.",
            "Download Canceled");
        _ = await alert.ShowAsync();
    }

    #endregion
}
