using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Doom.Brix.Synth;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Synth.Tests;

public class LfoTinySoundFontTests
{
    [Fact]
    public void lfo_d_03_f_50()
    {
        var path = Path.Combine(TestSettings.ReferenceDataDirectory, "TinySoundFont", "Lfo", "D03_F50.csv");
        var expected = File.ReadLines(path).Select(line => float.Parse(line.Split(',')[1], CultureInfo.InvariantCulture)).ToArray();
        expected.Length.Should().Be(1000);

        var synthesizer = new Synthesizer(TestSettings.LoadSoundFont("TimGM6mb"), 44100);
        var lfo = new Lfo(synthesizer);

        lfo.Start(0.3F, 5.0F);

        for (var i = 0; i < expected.Length; i++)
        {
            lfo.Process();

            lfo.Value.ShouldBeApproximately(expected[i], 2.5E-2);
        }
    }

    [Fact]
    public void lfo_d_00_f_70()
    {
        var path = Path.Combine(TestSettings.ReferenceDataDirectory, "TinySoundFont", "Lfo", "D00_F70.csv");
        var expected = File.ReadLines(path).Select(line => float.Parse(line.Split(',')[1], CultureInfo.InvariantCulture)).ToArray();
        expected.Length.Should().Be(1000);

        var synthesizer = new Synthesizer(TestSettings.LoadSoundFont("TimGM6mb"), 44100);
        var lfo = new Lfo(synthesizer);

        lfo.Start(0.0F, 7.0F);

        for (var i = 0; i < expected.Length; i++)
        {
            lfo.Process();

            lfo.Value.ShouldBeApproximately(expected[i], 2.5E-2);
        }
    }
}
