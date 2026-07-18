using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Doom.Brix.Synth;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Synth.Tests;

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

    private void RunSingleInstrument(string referenceTsvPath, Instrument instrument)
    {
        var polyphoneRegions = PolyphoneRegion.Read(referenceTsvPath);
        var meltyRegions = instrument.Regions.ToArray();

        polyphoneRegions.Length.Should().Be(meltyRegions.Length);

        foreach (var polyphoneRegion in polyphoneRegions)
        {
            var meltyRegion = meltyRegions.MinBy(x => GetError(polyphoneRegion, x));
            AreEqual(polyphoneRegion, meltyRegion);
        }
    }

    private static double GetError(PolyphoneRegion polyphoneRegion, InstrumentRegion meltyRegion)
    {
        if (polyphoneRegion.KeyRange != (meltyRegion.KeyRangeStart + "-" + meltyRegion.KeyRangeEnd))
        {
            return 1000000;
        }

        if (polyphoneRegion.VelocityRange != (meltyRegion.VelocityRangeStart + "-" + meltyRegion.VelocityRangeEnd))
        {
            return 1000000;
        }

        var error = 0.0;
        error += Math.Abs(polyphoneRegion.Attenuation - 0.4 * meltyRegion.InitialAttenuation);
        error += Math.Abs(polyphoneRegion.Pan - meltyRegion.Pan);
        error += Math.Abs(polyphoneRegion.LoopPlayback - (int)meltyRegion.SampleModes);
        // error += Math.Abs(polyphoneRegion.RootKey - meltyRegion.OverridingRootKey);
        error += Math.Abs(polyphoneRegion.TuningSemiTones - meltyRegion.CoarseTune);
        // error += Math.Abs(polyphoneRegion.TuningCents - meltyRegion.FineTune);
        error += Math.Abs(polyphoneRegion.ScaleTuning - meltyRegion.ScaleTuning);
        error += Math.Abs(polyphoneRegion.FilterCutoffHz - meltyRegion.InitialFilterCutoffFrequency);
        error += Math.Abs(polyphoneRegion.FilterResonanceDb - meltyRegion.InitialFilterQ);
        error += Math.Abs(polyphoneRegion.VolEnvDelayS - meltyRegion.DelayVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvAttackS - meltyRegion.AttackVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvHoldS - meltyRegion.HoldVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvDecayS - meltyRegion.DecayVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvSustainDb - meltyRegion.SustainVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.VolEnvReleaseS - meltyRegion.ReleaseVolumeEnvelope);
        error += Math.Abs(polyphoneRegion.KeyToVolEnvHoldC - meltyRegion.KeyNumberToVolumeEnvelopeHold);
        error += Math.Abs(polyphoneRegion.KeyToVolEnvDecayC - meltyRegion.KeyNumberToVolumeEnvelopeDecay);
        error += Math.Abs(polyphoneRegion.ModEnvDelayS - meltyRegion.DelayModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvAttackS - meltyRegion.AttackModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvHoldS - meltyRegion.HoldModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvDecayS - meltyRegion.DecayModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvSustainP - meltyRegion.SustainModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvReleaseS - meltyRegion.ReleaseModulationEnvelope);
        error += Math.Abs(polyphoneRegion.ModEnvToPitchC - meltyRegion.ModulationEnvelopeToPitch);
        error += Math.Abs(polyphoneRegion.ModEnvToFilterC - meltyRegion.ModulationEnvelopeToFilterCutoffFrequency);
        error += Math.Abs(polyphoneRegion.KeyToModEnvHoldC - meltyRegion.KeyNumberToModulationEnvelopeHold);
        error += Math.Abs(polyphoneRegion.KeyToModEnvDecayC - meltyRegion.KeyNumberToModulationEnvelopeDecay);
        error += Math.Abs(polyphoneRegion.ModLfoDelayS - meltyRegion.DelayModulationLfo);
        error += Math.Abs(polyphoneRegion.ModLfoFreqHz - meltyRegion.FrequencyModulationLfo);
        error += Math.Abs(polyphoneRegion.ModLfoToPitchC - meltyRegion.ModulationLfoToPitch);
        error += Math.Abs(polyphoneRegion.ModLftToFilterC - meltyRegion.ModulationLfoToFilterCutoffFrequency);
        error += Math.Abs(polyphoneRegion.ModLftToVolumeDb - meltyRegion.ModulationLfoToVolume);
        error += Math.Abs(polyphoneRegion.VibLfoDelayS - meltyRegion.DelayVibratoLfo);
        error += Math.Abs(polyphoneRegion.VibLfoFreqHz - meltyRegion.FrequencyVibratoLfo);
        error += Math.Abs(polyphoneRegion.VibLfoPitchC - meltyRegion.VibratoLfoToPitch);
        error += Math.Abs(polyphoneRegion.ExclusiveClass - meltyRegion.ExclusiveClass);
        error += Math.Abs(polyphoneRegion.ChorusP - meltyRegion.ChorusEffectsSend);
        error += Math.Abs(polyphoneRegion.ReverbP - meltyRegion.ReverbEffectsSend);

        return error;
    }

    private static void AreEqual(PolyphoneRegion polyphoneRegion, InstrumentRegion meltyRegion)
    {
        polyphoneRegion.KeyRange.Should().Be(meltyRegion.KeyRangeStart + "-" + meltyRegion.KeyRangeEnd);
        polyphoneRegion.VelocityRange.Should().Be(meltyRegion.VelocityRangeStart + "-" + meltyRegion.VelocityRangeEnd);
        polyphoneRegion.Attenuation.ShouldBeApproximately(0.4 * meltyRegion.InitialAttenuation, 0.01);
        polyphoneRegion.Pan.ShouldBeApproximately(meltyRegion.Pan, 0.1);
        polyphoneRegion.LoopPlayback.Should().Be((int)meltyRegion.SampleModes);
        // Assert.That(polyphoneRegion.RootKey, Is.EqualTo(meltyRegion.OverridingRootKey));
        polyphoneRegion.TuningSemiTones.Should().Be(meltyRegion.CoarseTune);
        // Assert.That(polyphoneRegion.TuningCents, Is.EqualTo(meltyRegion.FineTune));
        polyphoneRegion.ScaleTuning.Should().Be(meltyRegion.ScaleTuning);
        polyphoneRegion.FilterCutoffHz.ShouldBeApproximately(meltyRegion.InitialFilterCutoffFrequency, 1);
        polyphoneRegion.FilterResonanceDb.ShouldBeApproximately(meltyRegion.InitialFilterQ, 0.1);
        polyphoneRegion.VolEnvDelayS.ShouldBeApproximately(meltyRegion.DelayVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvAttackS.ShouldBeApproximately(meltyRegion.AttackVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvHoldS.ShouldBeApproximately(meltyRegion.HoldVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvDecayS.ShouldBeApproximately(meltyRegion.DecayVolumeEnvelope, 0.001);
        polyphoneRegion.VolEnvSustainDb.ShouldBeApproximately(meltyRegion.SustainVolumeEnvelope, 0.1);
        polyphoneRegion.VolEnvReleaseS.ShouldBeApproximately(meltyRegion.ReleaseVolumeEnvelope, 0.001);
        polyphoneRegion.KeyToVolEnvHoldC.Should().Be(meltyRegion.KeyNumberToVolumeEnvelopeHold);
        polyphoneRegion.KeyToVolEnvDecayC.Should().Be(meltyRegion.KeyNumberToVolumeEnvelopeDecay);
        polyphoneRegion.ModEnvDelayS.ShouldBeApproximately(meltyRegion.DelayModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvAttackS.ShouldBeApproximately(meltyRegion.AttackModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvHoldS.ShouldBeApproximately(meltyRegion.HoldModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvDecayS.ShouldBeApproximately(meltyRegion.DecayModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvSustainP.ShouldBeApproximately(meltyRegion.SustainModulationEnvelope, 0.1);
        polyphoneRegion.ModEnvReleaseS.ShouldBeApproximately(meltyRegion.ReleaseModulationEnvelope, 0.001);
        polyphoneRegion.ModEnvToPitchC.Should().Be(meltyRegion.ModulationEnvelopeToPitch);
        polyphoneRegion.ModEnvToFilterC.Should().Be(meltyRegion.ModulationEnvelopeToFilterCutoffFrequency);
        polyphoneRegion.KeyToModEnvHoldC.Should().Be(meltyRegion.KeyNumberToModulationEnvelopeHold);
        polyphoneRegion.KeyToModEnvDecayC.Should().Be(meltyRegion.KeyNumberToModulationEnvelopeDecay);
        polyphoneRegion.ModLfoDelayS.ShouldBeApproximately(meltyRegion.DelayModulationLfo, 0.001);
        polyphoneRegion.ModLfoFreqHz.ShouldBeApproximately(meltyRegion.FrequencyModulationLfo, 0.001);
        polyphoneRegion.ModLfoToPitchC.Should().Be(meltyRegion.ModulationLfoToPitch);
        polyphoneRegion.ModLftToFilterC.Should().Be(meltyRegion.ModulationLfoToFilterCutoffFrequency);
        polyphoneRegion.ModLftToVolumeDb.ShouldBeApproximately(meltyRegion.ModulationLfoToVolume, 0.1);
        polyphoneRegion.VibLfoDelayS.ShouldBeApproximately(meltyRegion.DelayVibratoLfo, 0.001);
        polyphoneRegion.VibLfoFreqHz.ShouldBeApproximately(meltyRegion.FrequencyVibratoLfo, 0.001);
        polyphoneRegion.VibLfoPitchC.Should().Be(meltyRegion.VibratoLfoToPitch);
        polyphoneRegion.ExclusiveClass.Should().Be(meltyRegion.ExclusiveClass);
        polyphoneRegion.ChorusP.ShouldBeApproximately(meltyRegion.ChorusEffectsSend, 0.1);
        polyphoneRegion.ReverbP.ShouldBeApproximately(meltyRegion.ReverbEffectsSend, 0.1);
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
                region.Attenuation = GetValue(localValues[2], globalValues[2], InstrumentRegion.Default.InitialAttenuation);
                region.Pan = GetValue(localValues[3], globalValues[3], InstrumentRegion.Default.Pan);
                region.LoopPlayback = GetValue(localValues[4], globalValues[4], (int)InstrumentRegion.Default.SampleModes);

                region.RootKey = GetValue(localValues[6], globalValues[6], InstrumentRegion.Default.RootKey);
                region.TuningSemiTones = GetValue(localValues[7], globalValues[7], InstrumentRegion.Default.CoarseTune);
                region.TuningCents = GetValue(localValues[8], globalValues[8], InstrumentRegion.Default.FineTune);
                region.ScaleTuning = GetValue(localValues[9], globalValues[9], InstrumentRegion.Default.ScaleTuning);
                region.FilterCutoffHz = GetValue(localValues[10], globalValues[10], InstrumentRegion.Default.InitialFilterCutoffFrequency);
                region.FilterResonanceDb = GetValue(localValues[11], globalValues[11], InstrumentRegion.Default.InitialFilterQ);
                region.VolEnvDelayS = GetValue(localValues[12], globalValues[12], InstrumentRegion.Default.DelayVolumeEnvelope);
                region.VolEnvAttackS = GetValue(localValues[13], globalValues[13], InstrumentRegion.Default.AttackVolumeEnvelope);
                region.VolEnvHoldS = GetValue(localValues[14], globalValues[14], InstrumentRegion.Default.HoldVolumeEnvelope);
                region.VolEnvDecayS = GetValue(localValues[15], globalValues[15], InstrumentRegion.Default.DecayVolumeEnvelope);
                region.VolEnvSustainDb = GetValue(localValues[16], globalValues[16], InstrumentRegion.Default.SustainVolumeEnvelope);
                region.VolEnvReleaseS = GetValue(localValues[17], globalValues[17], InstrumentRegion.Default.ReleaseVolumeEnvelope);
                region.KeyToVolEnvHoldC = GetValue(localValues[18], globalValues[18], InstrumentRegion.Default.KeyNumberToVolumeEnvelopeHold);
                region.KeyToVolEnvDecayC = GetValue(localValues[19], globalValues[19], InstrumentRegion.Default.KeyNumberToVolumeEnvelopeDecay);
                region.ModEnvDelayS = GetValue(localValues[20], globalValues[20], InstrumentRegion.Default.DelayModulationEnvelope);
                region.ModEnvAttackS = GetValue(localValues[21], globalValues[21], InstrumentRegion.Default.AttackModulationEnvelope);
                region.ModEnvHoldS = GetValue(localValues[22], globalValues[22], InstrumentRegion.Default.HoldModulationEnvelope);
                region.ModEnvDecayS = GetValue(localValues[23], globalValues[23], InstrumentRegion.Default.DecayModulationEnvelope);
                region.ModEnvSustainP = GetValue(localValues[24], globalValues[24], InstrumentRegion.Default.SustainModulationEnvelope);
                region.ModEnvReleaseS = GetValue(localValues[25], globalValues[25], InstrumentRegion.Default.ReleaseModulationEnvelope);
                region.ModEnvToPitchC = GetValue(localValues[26], globalValues[26], InstrumentRegion.Default.ModulationEnvelopeToPitch);
                region.ModEnvToFilterC = GetValue(localValues[27], globalValues[27], InstrumentRegion.Default.ModulationEnvelopeToFilterCutoffFrequency);
                region.KeyToModEnvHoldC = GetValue(localValues[28], globalValues[28], InstrumentRegion.Default.KeyNumberToModulationEnvelopeHold);
                region.KeyToModEnvDecayC = GetValue(localValues[29], globalValues[29], InstrumentRegion.Default.KeyNumberToModulationEnvelopeDecay);
                region.ModLfoDelayS = GetValue(localValues[30], globalValues[30], InstrumentRegion.Default.DelayModulationLfo);
                region.ModLfoFreqHz = GetValue(localValues[31], globalValues[31], InstrumentRegion.Default.FrequencyModulationLfo);
                region.ModLfoToPitchC = GetValue(localValues[32], globalValues[32], InstrumentRegion.Default.ModulationLfoToPitch);
                region.ModLftToFilterC = GetValue(localValues[33], globalValues[33], InstrumentRegion.Default.ModulationLfoToFilterCutoffFrequency);
                region.ModLftToVolumeDb = GetValue(localValues[34], globalValues[34], InstrumentRegion.Default.ModulationLfoToVolume);
                region.VibLfoDelayS = GetValue(localValues[35], globalValues[35], InstrumentRegion.Default.DelayVibratoLfo);
                region.VibLfoFreqHz = GetValue(localValues[36], globalValues[36], InstrumentRegion.Default.FrequencyVibratoLfo);
                region.VibLfoPitchC = GetValue(localValues[37], globalValues[37], InstrumentRegion.Default.VibratoLfoToPitch);
                region.ExclusiveClass = GetValue(localValues[38], globalValues[38], InstrumentRegion.Default.ExclusiveClass);
                region.ChorusP = GetValue(localValues[39], globalValues[39], InstrumentRegion.Default.ChorusEffectsSend);
                region.ReverbP = GetValue(localValues[40], globalValues[40], InstrumentRegion.Default.ReverbEffectsSend);

                region.SampleStartOffset = GetValue(localValues[44], globalValues[44], InstrumentRegion.Default.StartAddressOffset);
                region.SampleEndOffset = GetValue(localValues[45], globalValues[45], InstrumentRegion.Default.EndAddressOffset);

                region.LoopStartOffset = GetValue(localValues[47], globalValues[47], InstrumentRegion.Default.StartLoopAddressOffset);
                region.LoopEndOffset = GetValue(localValues[48], globalValues[48], InstrumentRegion.Default.EndLoopAddressOffset);

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
