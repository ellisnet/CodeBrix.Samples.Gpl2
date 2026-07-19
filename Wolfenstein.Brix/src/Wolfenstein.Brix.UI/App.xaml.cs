using CodeBrix.Platform.Simple;
using Wolfenstein.Brix.Helpers;
using Wolfenstein.Brix.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace Wolfenstein.Brix;

public partial class App : Application
{
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

    protected Window MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window
        {
            Title = "Wolfenstein.Brix"
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

        //The GameEngine's GLOBAL pause: minimizing the window parks the whole
        //  engine (the 70 Hz game loop idles at ~zero CPU and audio — including
        //  the OPL music stream — suspends); restoring resumes exactly where it
        //  left off, with the pause invisible to game time. The game's own
        //  ESC-menu pause is separate game logic and unaffected. Both calls are
        //  idempotent, and a pause that lands before the game host initializes
        //  simply starts the loop parked.
        //Minimize works on X11 since CodeBrix.Platform 1.0.199.897 (commit
        //  27053da4 wired iconification/_NET_WM_STATE into VisibilityChanged);
        //  earlier versions ignored UnmapNotify and kept the game running while
        //  minimized. Workspace switches deliberately do NOT pause.
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

        MainWindow.Activate();
    }

    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

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
}
