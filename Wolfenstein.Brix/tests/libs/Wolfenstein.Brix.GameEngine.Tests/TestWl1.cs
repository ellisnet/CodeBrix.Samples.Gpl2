using System;
using System.IO;

namespace Wolfenstein.Brix.GameEngine.Tests;

// Locates the shareware .WL1 data set that the data-dependent tests
// run against. The files live in the repo working tree at
// Downloaded/Wolfenstein.Brix_assets/ - present on this machine but
// never committed (the Downloaded folder is git-ignored). On a machine
// where the files have not been procured yet, those tests FAIL with the
// message below; the pure math/logic tests are unaffected.
internal static class TestWl1
{
    public static string AssetsFolderPath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(
                    directory.FullName, "Downloaded", "Wolfenstein.Brix_assets");
                if (File.Exists(Path.Combine(candidate, "VSWAP.WL1")))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException(
                "The Wolfenstein 3-D shareware data files were not found. Place the eight " +
                "shareware v1.4 data files (AUDIOHED/AUDIOT/GAMEMAPS/MAPHEAD/VGADICT/VGAHEAD/" +
                "VGAGRAPH/VSWAP, all .WL1) at {repo root}/Downloaded/Wolfenstein.Brix_assets/ " +
                "- the folder is git-ignored - and run the tests again. The Wolfenstein.Brix " +
                "application's Assets Mode can download and install them for you.");
        }
    }
}
