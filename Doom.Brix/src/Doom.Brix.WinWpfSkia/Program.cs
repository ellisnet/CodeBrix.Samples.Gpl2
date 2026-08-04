using CodeBrix.Platform.UI.Hosting;
using CodeBrix.Platform.UI.Runtime.Skia.Wpf;
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
            //The game presents a frame every tic and each present schedules a paint on the UI
            //  thread. Under the WPF head's default RenderFirst scheduling those paints are posted
            //  at DispatcherPriority.Render, which outranks the Input tier WPF delivers key events
            //  on — so on a device where one paint takes longer than the tic period the Render
            //  queue never empties and keyboard input is starved outright. Doom's 35 Hz leaves just
            //  enough of a gap to squeak by where Wolfenstein.Brix's 70 Hz does not, but "just
            //  barely keeping up" is not a margin worth relying on: a bigger window or a slower
            //  device closes it. InputFair posts the dispatcher pump on the Input tier instead, so
            //  paints and key events share one FIFO queue and interleave.
            .UseWindowsWpf(wpf => wpf.DispatcherScheduling(WpfDispatcherScheduling.InputFair))
            .UseDirectSkiaCanvasMode()
            .Build();

        if (host is WpfHost wpfHost)
        {
            wpfHost.RenderSurfaceType = RenderSurfaceType.Software;
        }

        host.Run();
    }
}
