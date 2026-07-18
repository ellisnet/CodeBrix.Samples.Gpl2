using System;
using System.Collections.Generic;
using System.IO;
using Doom.Brix.Synth;

namespace Doom.Brix.Synth.Tests;

// Shared fixtures for the SoundFont-dependent tests.
//
// Scoped to TimGM6mb only: its SoundFont (.sf2) is committed to the repo at
// Doom.Brix/src/libs/Doom.Brix.Game/ThirdPartyAssets/TimGM6mb.sf2, and its
// derived reference data is GPL-2, so it is the only SoundFont that has BOTH a
// clear redistribution license AND an in-repo .sf2 (the tests never download
// anything). The upstream MeltySynth suite also exercised Arachno, GeneralUser GS
// and SGM-V2.01; those were dropped because their data could not be redistributed
// and/or their .sf2 files are not present in the repo.
//
//was previously: MeltySynthTest.TestSettings (SoundFonts / LightSoundFonts arrays)
public static class TestSettings
{
    // xUnit [MemberData] source: the names of the SoundFonts under test. Each test
    // loads the SoundFont itself via LoadSoundFont(name) rather than receiving a
    // (non-serializable) SoundFont instance through MemberData.
    public static IEnumerable<object[]> SoundFontNames()
    {
        yield return new object[] { "TimGM6mb" };
    }

    // Loads a committed SoundFont by name from the game's ThirdPartyAssets folder.
    public static SoundFont LoadSoundFont(string name) => new SoundFont(FindSoundFontPath(name));

    // Walks up from the test output directory to the committed .sf2 (the same
    // approach Doom.Brix.GameEngine.Tests uses to locate TimGM6mb.sf2).
    public static string FindSoundFontPath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                directory.FullName, "Doom.Brix", "src", "libs", "Doom.Brix.Game",
                "ThirdPartyAssets", name + ".sf2");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"The committed SoundFont '{name}.sf2' was not found; TimGM6mb.sf2 lives at "
            + "Doom.Brix/src/libs/Doom.Brix.Game/ThirdPartyAssets/TimGM6mb.sf2.");
    }

    // Root of the copied reference test vectors (copied to the output directory).
    public static string ReferenceDataDirectory => Path.Combine(AppContext.BaseDirectory, "ReferenceData");
}
