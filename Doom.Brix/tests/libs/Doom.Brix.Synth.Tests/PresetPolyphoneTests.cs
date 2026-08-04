using System;
using System.Globalization;
using System.IO;
using System.Linq;
using CodeBrix.Audio.Synth;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Synth.Tests;

// Compares the preset regions CodeBrix.Audio.Synth parses out of a SoundFont against
// Polyphone's parameter dump of the same file.
//
//was previously: MeltySynthTest's Polyphone preset parameter comparison
public class PresetPolyphoneTests
{
    [Theory]
    [MemberData(nameof(TestSettings.SoundFontNames), MemberType = typeof(TestSettings))]
    public void parameter_check(string soundFontName)
    {
        var soundFont = TestSettings.LoadSoundFont(soundFontName);

        var referenceDataDirectory = Path.Combine(TestSettings.ReferenceDataDirectory, "Polyphone", soundFontName, "Presets");

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

        foreach (var preset in soundFont.Presets)
        {
            var name = preset.BankNumber.ToString("000") + " " + preset.PatchNumber.ToString("000") + " " + preset.Name.Replace('/', ' ').Replace(':', ' ');
            var referenceTsvPath = Path.Combine(referenceDataDirectory, name + ".tsv");

            if (File.Exists(referenceTsvPath))
            {
                RunSinglePreset(referenceTsvPath, preset);
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

    private void RunSinglePreset(string referenceTsvPath, Preset preset)
    {
        var polyphoneRegions = PolyphoneRegion.Read(referenceTsvPath);
        var parsedRegions = preset.Regions.ToArray();

        polyphoneRegions.Length.Should().Be(parsedRegions.Length);

        foreach (var polyphoneRegion in polyphoneRegions)
        {
            var parsedRegion = parsedRegions.MinBy(x => GetError(polyphoneRegion, x));
            AreEqual(polyphoneRegion, parsedRegion);
        }
    }

    private static double GetError(PolyphoneRegion polyphoneRegion, PresetRegion parsedRegion)
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
        error += Math.Abs(polyphoneRegion.TuningSemiTones - parsedRegion.CoarseTune);
        error += Math.Abs(polyphoneRegion.TuningCents - parsedRegion.FineTune);
        error += Math.Abs(polyphoneRegion.ScaleTuning - parsedRegion.ScaleTuning);
        error += Math.Abs(polyphoneRegion.FilterCutoffX - parsedRegion.InitialFilterCutoffFrequency);
        error += Math.Abs(polyphoneRegion.FilterResonanceX - parsedRegion.InitialFilterQ);
        error += Math.Abs(polyphoneRegion.VolEnvDelayX - parsedRegion.DelayVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvAttackX - parsedRegion.AttackVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvHoldX - parsedRegion.HoldVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvDecayX - parsedRegion.DecayVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvSustainDb - parsedRegion.SustainVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvReleaseX - parsedRegion.ReleaseVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.KeyToVolEnvHoldC - parsedRegion.KeyNumberToVolumeEnvelopeHold);
        error += Math.Abs(polyphoneRegion.KeyToVolEnvDecayC - parsedRegion.KeyNumberToVolumeEnvelopeDecay);
        error += Math.Abs(polyphoneRegion.ModEnvDelayX - parsedRegion.DelayModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvAttackX - parsedRegion.AttackModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvHoldX - parsedRegion.HoldModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvDecayX - parsedRegion.DecayModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvSustainP - parsedRegion.SustainModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvReleaseX - parsedRegion.ReleaseModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvToPitchC - parsedRegion.ModulationEnvelopeToPitch);
        error += Math.Abs(polyphoneRegion.ModEnvToFilterC - parsedRegion.ModulationEnvelopeToFilterCutoffFrequency);
        error += Math.Abs(polyphoneRegion.KeyToModEnvHoldC - parsedRegion.KeyNumberToModulationEnvelopeHold);
        error += Math.Abs(polyphoneRegion.KeyToModEnvDecayC - parsedRegion.KeyNumberToModulationEnvelopeDecay);
        error += Math.Abs(polyphoneRegion.ModLfoDelayX - parsedRegion.DelayModulationLfo);
        error += Math.Abs(polyphoneRegion.ModLfoFreqX - parsedRegion.FrequencyModulationLfo);
        error += Math.Abs(polyphoneRegion.ModLfoToPitchC - parsedRegion.ModulationLfoToPitch);
        error += Math.Abs(polyphoneRegion.ModLftToFilterC - parsedRegion.ModulationLfoToFilterCutoffFrequency);
        error += Math.Abs(polyphoneRegion.ModLftToVolumeDb - parsedRegion.ModulationLfoToVolume);
        error += Math.Abs(polyphoneRegion.VibLfoDelayX - parsedRegion.DelayVibratoLfo);
        error += Math.Abs(polyphoneRegion.VibLfoFreqX - parsedRegion.FrequencyVibratoLfo);
        error += Math.Abs(polyphoneRegion.VibLfoPitchC - parsedRegion.VibratoLfoToPitch);
        error += Math.Abs(polyphoneRegion.ChorusP - parsedRegion.ChorusEffectsSend);
        error += Math.Abs(polyphoneRegion.ReverbP - parsedRegion.ReverbEffectsSend);

        return error;
    }

    private static void AreEqual(PolyphoneRegion polyphoneRegion, PresetRegion parsedRegion)
    {
        polyphoneRegion.KeyRange.Should().Be(parsedRegion.KeyRangeStart + "-" + parsedRegion.KeyRangeEnd);
        polyphoneRegion.VelocityRange.Should().Be(parsedRegion.VelocityRangeStart + "-" + parsedRegion.VelocityRangeEnd);
        polyphoneRegion.Attenuation.ShouldBeApproximately(0.4 * parsedRegion.InitialAttenuation, 0.01);
        polyphoneRegion.Pan.ShouldBeApproximately(parsedRegion.Pan, 0.1);
        polyphoneRegion.TuningSemiTones.Should().Be(parsedRegion.CoarseTune);
        polyphoneRegion.TuningCents.Should().Be(parsedRegion.FineTune);
        polyphoneRegion.ScaleTuning.Should().Be(parsedRegion.ScaleTuning);
        polyphoneRegion.FilterCutoffX.ShouldBeApproximately(parsedRegion.InitialFilterCutoffFrequency, 0.001);
        polyphoneRegion.FilterResonanceX.ShouldBeApproximately(parsedRegion.InitialFilterQ, 0.1);
        polyphoneRegion.VolEnvDelayX.ShouldBeApproximately(parsedRegion.DelayVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvAttackX.ShouldBeApproximately(parsedRegion.AttackVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvHoldX.ShouldBeApproximately(parsedRegion.HoldVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvDecayX.ShouldBeApproximately(parsedRegion.DecayVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvSustainDb.ShouldBeApproximately(parsedRegion.SustainVolumeEnvelope, 0.1);
        polyphoneRegion.VolEnvReleaseX.ShouldBeApproximately(parsedRegion.ReleaseVolumeEnvelope, 0.001);
        polyphoneRegion.KeyToVolEnvHoldC.Should().Be(parsedRegion.KeyNumberToVolumeEnvelopeHold);
        polyphoneRegion.KeyToVolEnvDecayC.Should().Be(parsedRegion.KeyNumberToVolumeEnvelopeDecay);
        polyphoneRegion.ModEnvDelayX.ShouldBeApproximately(parsedRegion.DelayModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvAttackX.ShouldBeApproximately(parsedRegion.AttackModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvHoldX.ShouldBeApproximately(parsedRegion.HoldModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvDecayX.ShouldBeApproximately(parsedRegion.DecayModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvSustainP.ShouldBeApproximately(parsedRegion.SustainModulationEnvelope, 0.1);
        polyphoneRegion.ModEnvReleaseX.ShouldBeApproximately(parsedRegion.ReleaseModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvToPitchC.Should().Be(parsedRegion.ModulationEnvelopeToPitch);
        polyphoneRegion.ModEnvToFilterC.Should().Be(parsedRegion.ModulationEnvelopeToFilterCutoffFrequency);
        polyphoneRegion.KeyToModEnvHoldC.Should().Be(parsedRegion.KeyNumberToModulationEnvelopeHold);
        polyphoneRegion.KeyToModEnvDecayC.Should().Be(parsedRegion.KeyNumberToModulationEnvelopeDecay);
        polyphoneRegion.ModLfoDelayX.ShouldBeApproximately(parsedRegion.DelayModulationLfo, 0.001);
        polyphoneRegion.ModLfoFreqX.ShouldBeApproximately(parsedRegion.FrequencyModulationLfo, 0.001);
        polyphoneRegion.ModLfoToPitchC.Should().Be(parsedRegion.ModulationLfoToPitch);
        polyphoneRegion.ModLftToFilterC.Should().Be(parsedRegion.ModulationLfoToFilterCutoffFrequency);
        polyphoneRegion.ModLftToVolumeDb.ShouldBeApproximately(parsedRegion.ModulationLfoToVolume, 0.1);
        polyphoneRegion.VibLfoDelayX.ShouldBeApproximately(parsedRegion.DelayVibratoLfo, 0.001);
        polyphoneRegion.VibLfoFreqX.ShouldBeApproximately(parsedRegion.FrequencyVibratoLfo, 0.001);
        polyphoneRegion.VibLfoPitchC.Should().Be(parsedRegion.VibratoLfoToPitch);
        polyphoneRegion.ChorusP.ShouldBeApproximately(parsedRegion.ChorusEffectsSend, 0.1);
        polyphoneRegion.ReverbP.ShouldBeApproximately(parsedRegion.ReverbEffectsSend, 0.1);
    }



    private class PolyphoneRegion
    {
        public string KeyRange;
        public string VelocityRange;
        public double Attenuation;
        public double Pan;
        public double TuningSemiTones;
        public double TuningCents;
        public double ScaleTuning;
        public double FilterCutoffX;
        public double FilterResonanceX;
        public double VolEnvDelayX;
        public double VolEnvAttackX;
        public double VolEnvHoldX;
        public double VolEnvDecayX;
        public double VolEnvSustainDb;
        public double VolEnvReleaseX;
        public double KeyToVolEnvHoldC;
        public double KeyToVolEnvDecayC;
        public double ModEnvDelayX;
        public double ModEnvAttackX;
        public double ModEnvHoldX;
        public double ModEnvDecayX;
        public double ModEnvSustainP;
        public double ModEnvReleaseX;
        public double ModEnvToPitchC;
        public double ModEnvToFilterC;
        public double KeyToModEnvHoldC;
        public double KeyToModEnvDecayC;
        public double ModLfoDelayX;
        public double ModLfoFreqX;
        public double ModLfoToPitchC;
        public double ModLftToFilterC;
        public double ModLftToVolumeDb;
        public double VibLfoDelayX;
        public double VibLfoFreqX;
        public double VibLfoPitchC;
        public double ChorusP;
        public double ReverbP;

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
                region.Attenuation = GetValue(localValues[2], globalValues[2], PresetRegionDefaults.InitialAttenuation);
                region.Pan = GetValue(localValues[3], globalValues[3], PresetRegionDefaults.Pan);
                region.TuningSemiTones = GetValue(localValues[4], globalValues[4], PresetRegionDefaults.CoarseTune);
                region.TuningCents = GetValue(localValues[5], globalValues[5], PresetRegionDefaults.FineTune);
                region.ScaleTuning = GetValue(localValues[6], globalValues[6], PresetRegionDefaults.ScaleTuning);
                region.FilterCutoffX = GetValue(localValues[7], globalValues[7], PresetRegionDefaults.InitialFilterCutoffFrequency);
                region.FilterResonanceX = GetValue(localValues[8], globalValues[8], PresetRegionDefaults.InitialFilterQ);
                region.VolEnvDelayX = GetValue(localValues[9], globalValues[9], PresetRegionDefaults.DelayVolumeEnvelope);
                region.VolEnvAttackX = GetValue(localValues[10], globalValues[10], PresetRegionDefaults.AttackVolumeEnvelope);
                region.VolEnvHoldX = GetValue(localValues[11], globalValues[11], PresetRegionDefaults.HoldVolumeEnvelope);
                region.VolEnvDecayX = GetValue(localValues[12], globalValues[12], PresetRegionDefaults.DecayVolumeEnvelope);
                region.VolEnvSustainDb = GetValue(localValues[13], globalValues[13], PresetRegionDefaults.SustainVolumeEnvelope);
                region.VolEnvReleaseX = GetValue(localValues[14], globalValues[14], PresetRegionDefaults.ReleaseVolumeEnvelope);
                region.KeyToVolEnvHoldC = GetValue(localValues[15], globalValues[15], PresetRegionDefaults.KeyNumberToVolumeEnvelopeHold);
                region.KeyToVolEnvDecayC = GetValue(localValues[16], globalValues[16], PresetRegionDefaults.KeyNumberToVolumeEnvelopeDecay);
                region.ModEnvDelayX = GetValue(localValues[17], globalValues[17], PresetRegionDefaults.DelayModulationEnvelope);
                region.ModEnvAttackX = GetValue(localValues[18], globalValues[18], PresetRegionDefaults.AttackModulationEnvelope);
                region.ModEnvHoldX = GetValue(localValues[19], globalValues[19], PresetRegionDefaults.HoldModulationEnvelope);
                region.ModEnvDecayX = GetValue(localValues[20], globalValues[20], PresetRegionDefaults.DecayModulationEnvelope);
                region.ModEnvSustainP = GetValue(localValues[21], globalValues[21], PresetRegionDefaults.SustainModulationEnvelope);
                region.ModEnvReleaseX = GetValue(localValues[22], globalValues[22], PresetRegionDefaults.ReleaseModulationEnvelope);
                region.ModEnvToPitchC = GetValue(localValues[23], globalValues[23], PresetRegionDefaults.ModulationEnvelopeToPitch);
                region.ModEnvToFilterC = GetValue(localValues[24], globalValues[24], PresetRegionDefaults.ModulationEnvelopeToFilterCutoffFrequency);
                region.KeyToModEnvHoldC = GetValue(localValues[25], globalValues[25], PresetRegionDefaults.KeyNumberToModulationEnvelopeHold);
                region.KeyToModEnvDecayC = GetValue(localValues[26], globalValues[26], PresetRegionDefaults.KeyNumberToModulationEnvelopeDecay);
                region.ModLfoDelayX = GetValue(localValues[27], globalValues[27], PresetRegionDefaults.DelayModulationLfo);
                region.ModLfoFreqX = GetValue(localValues[28], globalValues[28], PresetRegionDefaults.FrequencyModulationLfo);
                region.ModLfoToPitchC = GetValue(localValues[29], globalValues[29], PresetRegionDefaults.ModulationLfoToPitch);
                region.ModLftToFilterC = GetValue(localValues[30], globalValues[30], PresetRegionDefaults.ModulationLfoToFilterCutoffFrequency);
                region.ModLftToVolumeDb = GetValue(localValues[31], globalValues[31], PresetRegionDefaults.ModulationLfoToVolume);
                region.VibLfoDelayX = GetValue(localValues[32], globalValues[32], PresetRegionDefaults.DelayVibratoLfo);
                region.VibLfoFreqX = GetValue(localValues[33], globalValues[33], PresetRegionDefaults.FrequencyVibratoLfo);
                region.VibLfoPitchC = GetValue(localValues[34], globalValues[34], PresetRegionDefaults.VibratoLfoToPitch);
                region.ChorusP = GetValue(localValues[35], globalValues[35], PresetRegionDefaults.ChorusEffectsSend);
                region.ReverbP = GetValue(localValues[36], globalValues[36], PresetRegionDefaults.ReverbEffectsSend);

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
