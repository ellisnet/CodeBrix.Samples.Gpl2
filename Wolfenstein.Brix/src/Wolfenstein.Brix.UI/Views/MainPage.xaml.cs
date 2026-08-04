using CodeBrix.Platform.Simple;
using Wolfenstein.Brix.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System;

namespace Wolfenstein.Brix.Views;

public sealed partial class MainPage : Page
{
    private bool _browserInitialized;

    public MainPage()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);
        };

        this.InitializeComponent();

        //The embedded browser belongs to Assets Mode only. Skipping its
        //  initialization in Game Mode both saves the WebView startup cost and
        //  keeps its native focus proxy from stealing keyboard focus from the
        //  game canvas.
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel { IsGameMode: false })
            {
                InitializeBrowser();
            }
        };

        //Fires (on the UI thread) once the game canvas first renders with a real
        //  size — with the canvas inside the Game Mode grid, that is when Game
        //  Mode first becomes visible. The view model boots the game host then.
        GameCanvas.FirstStarted += (_, _) =>
        {
            (DataContext as MainViewModel)?.CanvasFirstStart(GameCanvas);
            FocusGameCanvas();
        };

        //Keys reach the game only while the game surface holds keyboard focus,
        //  and clicking the canvas moves focus off it — KeyDown then routes to
        //  the focused element's ancestors, never the canvas, and the keyboard
        //  goes dead until Tab restores focus. Hand focus straight back after
        //  every click. handledEventsToo: true because the press may already be
        //  marked handled.
        GameCanvas.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler((_, _) => FocusGameCanvas()),
            handledEventsToo: true);
    }

    //Called by the app when the window is activated. The canvas keeps keyboard
    //  focus across a click (handled above) and gets it on first start, but
    //  nothing restored it when the window itself was deactivated and activated
    //  again — alt-tabbing away and back, or raising the window from another
    //  application, left the canvas unfocused and the keyboard silently dead
    //  until the player clicked. Game Mode only: in Assets Mode the embedded
    //  browser owns the keyboard, and stealing focus would break typing in it.
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

    private async void InitializeBrowser()
    {
        if (_browserInitialized || DataContext is not MainViewModel viewModel) { return; }
        _browserInitialized = true;

        //Assets Mode download policy (DownloadStarting, CodeBrix.Platform
        //  1.0.203.162+): the one permitted asset file (1wolf14.zip) — recognized
        //  by the download's suggested file name or URL, whichever mirror the
        //  user found it on — is downloaded by the browser into a temp target
        //  the .Assets pipeline verifies and extracts; any other download is
        //  canceled with an explanation. Page navigation is never intercepted.
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

        //Use CoreWebView2.Source (the authoritative current URL after redirects / user
        //  navigation); the XAML Browser.Source property does not reliably reflect those.
        Browser.NavigationCompleted += (_, _) =>
            viewModel.SetCurrentBrowserUrl(Browser.CoreWebView2?.Source ?? Browser.Source?.AbsoluteUri);

        viewModel.NavigateToUrl = url =>
        {
            if (!string.IsNullOrWhiteSpace(url)) { Browser.Source = new Uri(url); }
        };

        viewModel.OnBrowserReady();
    }

    //Pressing Enter in the address bar navigates, just like clicking GO.
    private void AddressBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter
            && DataContext is MainViewModel { GoCommand: var go }
            && go.CanExecute(null))
        {
            go.Execute(null);
            e.Handled = true;
        }
    }
}
