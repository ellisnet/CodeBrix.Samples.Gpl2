using System;
using System.Globalization;
using System.IO;
using System.Linq;
using CodeBrix.Audio.Synth;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Synth.Tests;

// Compares the instrument regions CodeBrix.Audio.Synth parses out of a SoundFont
// against Polyphone's parameter dump of the same file.
//
//was previously: MeltySynthTest's Polyphone instrument parameter comparison
public class InstrumentPolyphoneTests
{
    [Theory]
    [MemberData(nameof(TestSettings.SoundFontNames), MemberType = typeof(TestSettings))]
    public void parameter_check(string soundFontName)
    {
        var soundFont = TestSettings.LoadSoundFont(soundFontName);

        var referenceDataDirectory = Path.Combine(TestSettings.ReferenceDataDirectory, "Polyphone", soundFontName, "Instruments");

        if (Directory.Exists(referenceDataDirectory))
        {
            Run(soundFont, referenceDataDirectory);
        }
        else
        {
            Assert.Skip("The reference data is missing.");
        }
    }

    private void Run(SoundFont soundFont, string referenceDataDirectory)
    {
        var executed = 0;
        var ignored = 0;

        foreach (var instrument in soundFont.Instruments)
        {
            var name = instrument.Name.Replace('/', ' ').Replace(':', ' ');
            var referenceTsvPath = Path.Combine(referenceDataDirectory, name + ".tsv");

            if (File.Exists(referenceTsvPath))
            {
                RunSingleInstrument(referenceTsvPath, instrument);
                executed++;
            }
            else
            {
                ignored++;
            }
        }

        Console.WriteLine("Executed: " + executed);
        Console.WriteLine("Ignored: " + ignored);

        if (ignored > 0)
        {
            Assert.Skip("Some reference data is missing.");
        }
    }

    private void RunSingleInstrument(string referenceTsvPath, SoundFontInstrument instrument)
    {
        var polyphoneRegions = PolyphoneRegion.Read(referenceTsvPath);
        var parsedRegions = instrument.Regions.ToArray();

        polyphoneRegions.Length.Should().Be(parsedRegions.Length);

        foreach (var polyphoneRegion in polyphoneRegions)
        {
            var parsedRegion = parsedRegions.MinBy(x => GetError(polyphoneRegion, x));
            AreEqual(polyphoneRegion, parsedRegion);
        }
    }

    private static double GetError(PolyphoneRegion polyphoneRegion, InstrumentRegion parsedRegion)
    {
        if (polyphoneRegion.KeyRange != (parsedRegion.KeyRangeStart + "-" + parsedRegion.KeyRangeEnd))
        {
            return 1000000;
        }

        if (polyphoneRegion.VelocityRange != (parsedRegion.VelocityRangeStart + "-" + parsedRegion.VelocityRangeEnd))
        {
            return 1000000;
        }

        var error = 0.0;
        error += Math.Abs(polyphoneRegion.Attenuation - 0.4 * parsedRegion.InitialAttenuation);
        error += Math.Abs(polyphoneRegion.Pan - parsedRegion.Pan);
        error += Math.Abs(polyphoneRegion.LoopPlayback - (int)parsedRegion.SampleModes);
        // error += Math.Abs(polyphoneRegion.RootKey - parsedRegion.OverridingRootKey);
        error += Math.Abs(polyphoneRegion.TuningSemiTones - parsedRegion.CoarseTune);
        // error += Math.Abs(polyphoneRegion.TuningCents - parsedRegion.FineTune);
        error += Math.Abs(polyphoneRegion.ScaleTuning - parsedRegion.ScaleTuning);
        error += Math.Abs(polyphoneRegion.FilterCutoffHz - parsedRegion.InitialFilterCutoffFrequency);
        error += Math.Abs(polyphoneRegion.FilterResonanceDb - parsedRegion.InitialFilterQ);
        error += Math.Abs(polyphoneRegion.VolEnvDelayS - parsedRegion.DelayVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvAttackS - parsedRegion.AttackVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvHoldS - parsedRegion.HoldVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvDecayS - parsedRegion.DecayVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvSustainDb - parsedRegion.SustainVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvReleaseS - parsedRegion.ReleaseVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.KeyToVolEnvHoldC - parsedRegion.KeyNumberToVolumeEnvelopeHold);
        error += Math.Abs(polyphoneRegion.KeyToVolEnvDecayC - parsedRegion.KeyNumberToVolumeEnvelopeDecay);
        error += Math.Abs(polyphoneRegion.ModEnvDelayS - parsedRegion.DelayModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvAttackS - parsedRegion.AttackModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvHoldS - parsedRegion.HoldModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvDecayS - parsedRegion.DecayModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvSustainP - parsedRegion.SustainModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvReleaseS - parsedRegion.ReleaseModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvToPitchC - parsedRegion.ModulationEnvelopeToPitch);
        error += Math.Abs(polyphoneRegion.ModEnvToFilterC - parsedRegion.ModulationEnvelopeToFilterCutoffFrequency);
        error += Math.Abs(polyphoneRegion.KeyToModEnvHoldC - parsedRegion.KeyNumberToModulationEnvelopeHold);
        error += Math.Abs(polyphoneRegion.KeyToModEnvDecayC - parsedRegion.KeyNumberToModulationEnvelopeDecay);
        error += Math.Abs(polyphoneRegion.ModLfoDelayS - parsedRegion.DelayModulationLfo);
        error += Math.Abs(polyphoneRegion.ModLfoFreqHz - parsedRegion.FrequencyModulationLfo);
        error += Math.Abs(polyphoneRegion.ModLfoToPitchC - parsedRegion.ModulationLfoToPitch);
        error += Math.Abs(polyphoneRegion.ModLftToFilterC - parsedRegion.ModulationLfoToFilterCutoffFrequency);
        error += Math.Abs(polyphoneRegion.ModLftToVolumeDb - parsedRegion.ModulationLfoToVolume);
        error += Math.Abs(polyphoneRegion.VibLfoDelayS - parsedRegion.DelayVibratoLfo);
        error += Math.Abs(polyphoneRegion.VibLfoFreqHz - parsedRegion.FrequencyVibratoLfo);
        error += Math.Abs(polyphoneRegion.VibLfoPitchC - parsedRegion.VibratoLfoToPitch);
        error += Math.Abs(polyphoneRegion.ExclusiveClass - parsedRegion.ExclusiveClass);
        error += Math.Abs(polyphoneRegion.ChorusP - parsedRegion.ChorusEffectsSend);
        error += Math.Abs(polyphoneRegion.ReverbP - parsedRegion.ReverbEffectsSend);

        return error;
    }

    private static void AreEqual(PolyphoneRegion polyphoneRegion, InstrumentRegion parsedRegion)
    {
        polyphoneRegion.KeyRange.Should().Be(parsedRegion.KeyRangeStart + "-" + parsedRegion.KeyRangeEnd);
        polyphoneRegion.VelocityRange.Should().Be(parsedRegion.VelocityRangeStart + "-" + parsedRegion.VelocityRangeEnd);
        polyphoneRegion.Attenuation.ShouldBeApproximately(0.4 * parsedRegion.InitialAttenuation, 0.01);
        polyphoneRegion.Pan.ShouldBeApproximately(parsedRegion.Pan, 0.1);
        polyphoneRegion.LoopPlayback.Should().Be((int)parsedRegion.SampleModes);
        // Assert.That(polyphoneRegion.RootKey, Is.EqualTo(parsedRegion.OverridingRootKey));
        polyphoneRegion.TuningSemiTones.Should().Be(parsedRegion.CoarseTune);
        // Assert.That(polyphoneRegion.TuningCents, Is.EqualTo(parsedRegion.FineTune));
        polyphoneRegion.ScaleTuning.Should().Be(parsedRegion.ScaleTuning);
        polyphoneRegion.FilterCutoffHz.ShouldBeApproximately(parsedRegion.InitialFilterCutoffFrequency, 1);
        polyphoneRegion.FilterResonanceDb.ShouldBeApproximately(parsedRegion.InitialFilterQ, 0.1);
        polyphoneRegion.VolEnvDelayS.ShouldBeApproximately(parsedRegion.DelayVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvAttackS.ShouldBeApproximately(parsedRegion.AttackVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvHoldS.ShouldBeApproximately(parsedRegion.HoldVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvDecayS.ShouldBeApproximately(parsedRegion.DecayVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvSustainDb.ShouldBeApproximately(parsedRegion.SustainVolumeEnvelope, 0.1);
        polyphoneRegion.VolEnvReleaseS.ShouldBeApproximately(parsedRegion.ReleaseVolumeEnvelope, 0.001);
        polyphoneRegion.KeyToVolEnvHoldC.Should().Be(parsedRegion.KeyNumberToVolumeEnvelopeHold);
        polyphoneRegion.KeyToVolEnvDecayC.Should().Be(parsedRegion.KeyNumberToVolumeEnvelopeDecay);
        polyphoneRegion.ModEnvDelayS.ShouldBeApproximately(parsedRegion.DelayModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvAttackS.ShouldBeApproximately(parsedRegion.AttackModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvHoldS.ShouldBeApproximately(parsedRegion.HoldModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvDecayS.ShouldBeApproximately(parsedRegion.DecayModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvSustainP.ShouldBeApproximately(parsedRegion.SustainModulationEnvelope, 0.1);
        polyphoneRegion.ModEnvReleaseS.ShouldBeApproximately(parsedRegion.ReleaseModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvToPitchC.Should().Be(parsedRegion.ModulationEnvelopeToPitch);
        polyphoneRegion.ModEnvToFilterC.Should().Be(parsedRegion.ModulationEnvelopeToFilterCutoffFrequency);
        polyphoneRegion.KeyToModEnvHoldC.Should().Be(parsedRegion.KeyNumberToModulationEnvelopeHold);
        polyphoneRegion.KeyToModEnvDecayC.Should().Be(parsedRegion.KeyNumberToModulationEnvelopeDecay);
        polyphoneRegion.ModLfoDelayS.ShouldBeApproximately(parsedRegion.DelayModulationLfo, 0.001);
        polyphoneRegion.ModLfoFreqHz.ShouldBeApproximately(parsedRegion.FrequencyModulationLfo, 0.001);
        polyphoneRegion.ModLfoToPitchC.Should().Be(parsedRegion.ModulationLfoToPitch);
        polyphoneRegion.ModLftToFilterC.Should().Be(parsedRegion.ModulationLfoToFilterCutoffFrequency);
        polyphoneRegion.ModLftToVolumeDb.ShouldBeApproximately(parsedRegion.ModulationLfoToVolume, 0.1);
        polyphoneRegion.VibLfoDelayS.ShouldBeApproximately(parsedRegion.DelayVibratoLfo, 0.001);
        polyphoneRegion.VibLfoFreqHz.ShouldBeApproximately(parsedRegion.FrequencyVibratoLfo, 0.001);
        polyphoneRegion.VibLfoPitchC.Should().Be(parsedRegion.VibratoLfoToPitch);
        polyphoneRegion.ExclusiveClass.Should().Be(parsedRegion.ExclusiveClass);
        polyphoneRegion.ChorusP.ShouldBeApproximately(parsedRegion.ChorusEffectsSend, 0.1);
        polyphoneRegion.ReverbP.ShouldBeApproximately(parsedRegion.ReverbEffectsSend, 0.1);
    }



    private class PolyphoneRegion
    {
        public string KeyRange;
        public string VelocityRange;
        public double Attenuation;
        public double Pan;
        public double LoopPlayback;
        public double RootKey;
        public double TuningSemiTones;
        public double TuningCents;
        public double ScaleTuning;
        public double FilterCutoffHz;
        public double FilterResonanceDb;
        public double VolEnvDelayS;
        public double VolEnvAttackS;
        public double VolEnvHoldS;
        public double VolEnvDecayS;
        public double VolEnvSustainDb;
        public double VolEnvReleaseS;
        public double KeyToVolEnvHoldC;
        public double KeyToVolEnvDecayC;
        public double ModEnvDelayS;
        public double ModEnvAttackS;
        public double ModEnvHoldS;
        public double ModEnvDecayS;
        public double ModEnvSustainP;
        public double ModEnvReleaseS;
        public double ModEnvToPitchC;
        public double ModEnvToFilterC;
        public double KeyToModEnvHoldC;
        public double KeyToModEnvDecayC;
        public double ModLfoDelayS;
        public double ModLfoFreqHz;
        public double ModLfoToPitchC;
        public double ModLftToFilterC;
        public double ModLftToVolumeDb;
        public double VibLfoDelayS;
        public double VibLfoFreqHz;
        public double VibLfoPitchC;
        public double ExclusiveClass;
        public double ChorusP;
        public double ReverbP;
        public double SampleStartOffset;
        public double SampleEndOffset;
        public double LoopStartOffset;
        public double LoopEndOffset;

        public static PolyphoneRegion[] Read(string tsvPath)
        {
            var tsv = File.ReadLines(tsvPath).Select(line => line.Split('\t')).ToArray();

            var globalValues = tsv.Select(row => row[0]).ToArray();

            var regionCount = tsv[0].Length - 1;

            var regions = new PolyphoneRegion[regionCount];

            for (var i = 0; i < regions.Length; i++)
            {
                var col = i + 1;

                var localValues = tsv.Select(row => row[col]).ToArray();

                var region = new PolyphoneRegion();

                region.KeyRange = GetRange(localValues[0], globalValues[0]);
                region.VelocityRange = GetRange(localValues[1], globalValues[1]);
                region.Attenuation = GetValue(localValues[2], globalValues[2], InstrumentRegionDefaults.InitialAttenuation);
                region.Pan = GetValue(localValues[3], globalValues[3], InstrumentRegionDefaults.Pan);
                region.LoopPlayback = GetValue(localValues[4], globalValues[4], (int)InstrumentRegionDefaults.SampleModes);

                region.RootKey = GetValue(localValues[6], globalValues[6], InstrumentRegionDefaults.RootKey);
                region.TuningSemiTones = GetValue(localValues[7], globalValues[7], InstrumentRegionDefaults.CoarseTune);
                region.TuningCents = GetValue(localValues[8], globalValues[8], InstrumentRegionDefaults.FineTune);
                region.ScaleTuning = GetValue(localValues[9], globalValues[9], InstrumentRegionDefaults.ScaleTuning);
                region.FilterCutoffHz = GetValue(localValues[10], globalValues[10], InstrumentRegionDefaults.InitialFilterCutoffFrequency);
                region.FilterResonanceDb = GetValue(localValues[11], globalValues[11], InstrumentRegionDefaults.InitialFilterQ);
                region.VolEnvDelayS = GetValue(localValues[12], globalValues[12], InstrumentRegionDefaults.DelayVolumeEnvelope);
                region.VolEnvAttackS = GetValue(localValues[13], globalValues[13], InstrumentRegionDefaults.AttackVolumeEnvelope);
                region.VolEnvHoldS = GetValue(localValues[14], globalValues[14], InstrumentRegionDefaults.HoldVolumeEnvelope);
                region.VolEnvDecayS = GetValue(localValues[15], globalValues[15], InstrumentRegionDefaults.DecayVolumeEnvelope);
                region.VolEnvSustainDb = GetValue(localValues[16], globalValues[16], InstrumentRegionDefaults.SustainVolumeEnvelope);
                region.VolEnvReleaseS = GetValue(localValues[17], globalValues[17], InstrumentRegionDefaults.ReleaseVolumeEnvelope);
                region.KeyToVolEnvHoldC = GetValue(localValues[18], globalValues[18], InstrumentRegionDefaults.KeyNumberToVolumeEnvelopeHold);
                region.KeyToVolEnvDecayC = GetValue(localValues[19], globalValues[19], InstrumentRegionDefaults.KeyNumberToVolumeEnvelopeDecay);
                region.ModEnvDelayS = GetValue(localValues[20], globalValues[20], InstrumentRegionDefaults.DelayModulationEnvelope);
                region.ModEnvAttackS = GetValue(localValues[21], globalValues[21], InstrumentRegionDefaults.AttackModulationEnvelope);
                region.ModEnvHoldS = GetValue(localValues[22], globalValues[22], InstrumentRegionDefaults.HoldModulationEnvelope);
                region.ModEnvDecayS = GetValue(localValues[23], globalValues[23], InstrumentRegionDefaults.DecayModulationEnvelope);
                region.ModEnvSustainP = GetValue(localValues[24], globalValues[24], InstrumentRegionDefaults.SustainModulationEnvelope);
                region.ModEnvReleaseS = GetValue(localValues[25], globalValues[25], InstrumentRegionDefaults.ReleaseModulationEnvelope);
                region.ModEnvToPitchC = GetValue(localValues[26], globalValues[26], InstrumentRegionDefaults.ModulationEnvelopeToPitch);
                region.ModEnvToFilterC = GetValue(localValues[27], globalValues[27], InstrumentRegionDefaults.ModulationEnvelopeToFilterCutoffFrequency);
                region.KeyToModEnvHoldC = GetValue(localValues[28], globalValues[28], InstrumentRegionDefaults.KeyNumberToModulationEnvelopeHold);
                region.KeyToModEnvDecayC = GetValue(localValues[29], globalValues[29], InstrumentRegionDefaults.KeyNumberToModulationEnvelopeDecay);
                region.ModLfoDelayS = GetValue(localValues[30], globalValues[30], InstrumentRegionDefaults.DelayModulationLfo);
                region.ModLfoFreqHz = GetValue(localValues[31], globalValues[31], InstrumentRegionDefaults.FrequencyModulationLfo);
                region.ModLfoToPitchC = GetValue(localValues[32], globalValues[32], InstrumentRegionDefaults.ModulationLfoToPitch);
                region.ModLftToFilterC = GetValue(localValues[33], globalValues[33], InstrumentRegionDefaults.ModulationLfoToFilterCutoffFrequency);
                region.ModLftToVolumeDb = GetValue(localValues[34], globalValues[34], InstrumentRegionDefaults.ModulationLfoToVolume);
                region.VibLfoDelayS = GetValue(localValues[35], globalValues[35], InstrumentRegionDefaults.DelayVibratoLfo);
                region.VibLfoFreqHz = GetValue(localValues[36], globalValues[36], InstrumentRegionDefaults.FrequencyVibratoLfo);
                region.VibLfoPitchC = GetValue(localValues[37], globalValues[37], InstrumentRegionDefaults.VibratoLfoToPitch);
                region.ExclusiveClass = GetValue(localValues[38], globalValues[38], InstrumentRegionDefaults.ExclusiveClass);
                region.ChorusP = GetValue(localValues[39], globalValues[39], InstrumentRegionDefaults.ChorusEffectsSend);
                region.ReverbP = GetValue(localValues[40], globalValues[40], InstrumentRegionDefaults.ReverbEffectsSend);

                region.SampleStartOffset = GetValue(localValues[44], globalValues[44], InstrumentRegionDefaults.StartAddressOffset);
                region.SampleEndOffset = GetValue(localValues[45], globalValues[45], InstrumentRegionDefaults.EndAddressOffset);

                region.LoopStartOffset = GetValue(localValues[47], globalValues[47], InstrumentRegionDefaults.StartLoopAddressOffset);
                region.LoopEndOffset = GetValue(localValues[48], globalValues[48], InstrumentRegionDefaults.EndLoopAddressOffset);

                regions[i] = region;
            }

            return regions;
        }

        private static string GetRange(string local, string global)
        {
            if (local == "!" || local == "?")
            {
                if (global == "!" || global == "?")
                {
                    return "0-127";
                }
                else
                {
                    return FormatRange(global);
                }
            }
            else
            {
                return FormatRange(local);
            }
        }

        private static string FormatRange(string range)
        {
            if (range.Contains('-'))
            {
                return range;
            }
            else
            {
                return range + "-" + range;
            }
        }

        private static double GetValue(string local, string global, double defaultValue)
        {
            if (local == "!" || local == "?")
            {
                if (global == "!" || global == "?")
                {
                    return defaultValue;
                }
                else
                {
                    return double.Parse(global, CultureInfo.InvariantCulture);
                }
            }
            else
            {
                return double.Parse(local, CultureInfo.InvariantCulture);
            }
        }
    }
}
