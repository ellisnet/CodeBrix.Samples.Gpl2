using System;
using System.IO;

namespace Doom.Brix.GameEngine.Tests;

// Locates the shareware DOOM1.WAD that the WAD-dependent tests run
// against. The file lives in the repo working tree at
// Downloaded/Doom.Brix_assets/DOOM1.WAD — present on this machine but
// never committed (the Downloaded folder is git-ignored). On a machine
// where the file has not been procured yet, those tests FAIL with the
// message below; the pure math/logic tests are unaffected.
internal static class TestWad
{
    public static string Doom1SharewarePath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(
                    directory.FullName, "Downloaded", "Doom.Brix_assets", "DOOM1.WAD");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                "The shareware DOOM1.WAD was not found. Place the Doom v1.9 shareware " +
                "DOOM1.WAD (md5 f0cefca49926d00903cf57551d901abe) at " +
                "{repo root}/Downloaded/Doom.Brix_assets/DOOM1.WAD — the folder is " +
                "git-ignored — and run the tests again. The Doom.Brix application's " +
                "Assets Mode can download and install it for you.");
        }
    }
}
