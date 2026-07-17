using CodeBrix.Platform.Simple;
using Doom.Brix.Helpers;
using Doom.Brix.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace Doom.Brix;

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

        //The GameEngine's GLOBAL pause: minimizing the window parks the whole
        //  engine (the 35 Hz game loop idles at ~zero CPU and audio suspends);
        //  restoring resumes exactly where it left off, with the pause invisible
        //  to game time. Doom's own in-game pause is separate game logic and
        //  unaffected. Both calls are idempotent, and a pause that lands before
        //  the game host initializes simply starts the loop parked.
        //KNOWN PLATFORM GAP (CodeBrix.Platform 1.0.197.800): the X11 head raises
        //  VisibilityChanged only from VisibilityNotify (obscured-state) events and
        //  ignores UnmapNotify, so minimizing on X11 does not fire this event (and
        //  raises no Activated/Deactivated either) — the game keeps running
        //  minimized there until the platform wires iconification to visibility.
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
