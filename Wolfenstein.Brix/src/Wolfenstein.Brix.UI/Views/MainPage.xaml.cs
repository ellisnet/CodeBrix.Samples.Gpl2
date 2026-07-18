using CodeBrix.Platform.Simple;
using Wolfenstein.Brix.ViewModels;
using Microsoft.UI.Xaml.Controls;
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
            (DataContext as MainViewModel)?.CanvasFirstStart(GameCanvas);
    }

    private void InitializeBrowser()
    {
        if (_browserInitialized || DataContext is not MainViewModel viewModel) { return; }
        _browserInitialized = true;

        //Assets Mode download policy: the one permitted asset file (1wolf14.zip) is
        //  intercepted here and downloaded/verified/extracted by the .Assets pipeline
        //  instead; any other download-looking link is canceled with an explanation.
        //  Ordinary page navigation passes through untouched.
        Browser.NavigationStarting += (_, args) =>
        {
            if (viewModel.HandleNavigationStarting(args.Uri)) { args.Cancel = true; }
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
