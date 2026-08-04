using CodeBrix.Platform.UI.Hosting;
using CodeBrix.Platform.UI.Runtime.Skia.Wpf;
using System;

namespace Wolfenstein.Brix;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        var host = CodeBrixPlatformHostBuilder.Create()
            .App(() => new App())
            //The game presents a frame every tic (70 Hz) and each present schedules a paint on the
            //  UI thread. Under the WPF head's default RenderFirst scheduling those paints are
            //  posted at DispatcherPriority.Render, which outranks the Input tier WPF delivers key
            //  events on — so on a device where one paint takes longer than the 14.3 ms tic period
            //  the Render queue never empties, and keyboard input is starved outright rather than
            //  merely delayed: the game keeps rendering but never responds, sitting on the title
            //  screen forever (the title screen has no timeout). Seen on a Windows-on-ARM64 SQ1 at
            //  window sizes from 1024x640 up; Doom.Brix escapes it only because 35 Hz leaves a gap.
            //  InputFair posts the dispatcher pump on the Input tier instead, so paints and key
            //  events share one FIFO queue and interleave.
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
