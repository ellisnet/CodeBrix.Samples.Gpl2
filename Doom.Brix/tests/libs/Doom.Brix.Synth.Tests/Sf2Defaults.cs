using System;
using CodeBrix.Audio.Synth;

namespace Doom.Brix.Synth.Tests;

// The SoundFont 2.04 default generator values, expressed the way the
// CodeBrix.Audio.Synth region properties expose them.
//
// The Polyphone dumps mark an absent generator with "!" or "?", and the tests need
// the value the renderer would use in its place. That used to come from
// InstrumentRegion.Default / PresetRegion.Default, which are internal to
// CodeBrix.Audio (its InternalsVisibleTo names only CodeBrix.Audio.Tests), so the
// defaults are restated here as plain constants. They are the SoundFont 2.04
// defaults - spec section 8.1.3, "Generator Enumerators Defined", column "Default
// Value" - read off the CodeBrix.Audio.Synth region types they stand in for, so the
// two agree by construction. The Polyphone comparisons below fail if they drift.
//
// The two region kinds differ because the SoundFont generator model does: an
// instrument generator is ABSOLUTE, so an absent one falls back to the spec's
// absolute default; a preset generator is RELATIVE and is added to (or, for the
// time and frequency generators, multiplied into) the instrument's value, so an
// absent one must contribute nothing - 0 for the additive generators and 1 for the
// multiplicative ones.
internal static class Sf2Defaults
{
    // 2^(timecents/1200); mirrors SoundFontMath.TimecentsToSeconds.
    internal static double TimecentsToSeconds(double timecents) => Math.Pow(2.0, timecents / 1200.0);

    // 8.176 Hz is MIDI key 0; mirrors SoundFontMath.CentsToHertz.
    internal static double CentsToHertz(double cents) => 8.176 * Math.Pow(2.0, cents / 1200.0);
}

// Absolute defaults - what an instrument region uses for a generator it does not carry.
internal static class InstrumentRegionDefaults
{
    // The envelope and LFO time generators (21-23, 25-30, 33, 35) all default to
    // -12000 timecents, i.e. 1 ms - the shortest representable time.
    private const double MinimumTimecents = -12000.0;

    // initialFilterFc (gen 8) defaults to 13500 absolute cents, i.e. ~19.9 kHz:
    // wide open, so an unset filter does not colour the sample.
    private const double OpenFilterCents = 13500.0;

    public const double InitialAttenuation = 0.0;
    public const double Pan = 0.0;
    public const LoopMode SampleModes = LoopMode.NoLoop;

    // overridingRootKey defaults to -1, which means "use the sample's original
    // pitch"; the default region carries no sample, so that resolves to 0.
    public const double RootKey = 0.0;
    public const double CoarseTune = 0.0;
    public const double FineTune = 0.0;
    public const double ScaleTuning = 100.0;

    public static readonly double InitialFilterCutoffFrequency = Sf2Defaults.CentsToHertz(OpenFilterCents);
    public const double InitialFilterQ = 0.0;

    public static readonly double DelayVolumeEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public static readonly double AttackVolumeEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public static readonly double HoldVolumeEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public static readonly double DecayVolumeEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public const double SustainVolumeEnvelope = 0.0;
    public static readonly double ReleaseVolumeEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public const double KeyNumberToVolumeEnvelopeHold = 0.0;
    public const double KeyNumberToVolumeEnvelopeDecay = 0.0;

    public static readonly double DelayModulationEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public static readonly double AttackModulationEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public static readonly double HoldModulationEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public static readonly double DecayModulationEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public const double SustainModulationEnvelope = 0.0;
    public static readonly double ReleaseModulationEnvelope = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public const double ModulationEnvelopeToPitch = 0.0;
    public const double ModulationEnvelopeToFilterCutoffFrequency = 0.0;
    public const double KeyNumberToModulationEnvelopeHold = 0.0;
    public const double KeyNumberToModulationEnvelopeDecay = 0.0;

    public static readonly double DelayModulationLfo = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public static readonly double FrequencyModulationLfo = Sf2Defaults.CentsToHertz(0.0);
    public const double ModulationLfoToPitch = 0.0;
    public const double ModulationLfoToFilterCutoffFrequency = 0.0;
    public const double ModulationLfoToVolume = 0.0;

    public static readonly double DelayVibratoLfo = Sf2Defaults.TimecentsToSeconds(MinimumTimecents);
    public static readonly double FrequencyVibratoLfo = Sf2Defaults.CentsToHertz(0.0);
    public const double VibratoLfoToPitch = 0.0;

    public const double ExclusiveClass = 0.0;
    public const double ChorusEffectsSend = 0.0;
    public const double ReverbEffectsSend = 0.0;

    public const double StartAddressOffset = 0.0;
    public const double EndAddressOffset = 0.0;
    public const double StartLoopAddressOffset = 0.0;
    public const double EndLoopAddressOffset = 0.0;
}

// Relative defaults - what a preset region contributes for a generator it does not
// carry. Additive generators default to 0; the time and frequency generators are
// multiplying factors, so they default to 1.
internal static class PresetRegionDefaults
{
    public const double InitialAttenuation = 0.0;
    public const double Pan = 0.0;
    public const double CoarseTune = 0.0;
    public const double FineTune = 0.0;
    public const double ScaleTuning = 0.0;

    public const double InitialFilterCutoffFrequency = 1.0;
    public const double InitialFilterQ = 0.0;

    public const double DelayVolumeEnvelope = 1.0;
    public const double AttackVolumeEnvelope = 1.0;
    public const double HoldVolumeEnvelope = 1.0;
    public const double DecayVolumeEnvelope = 1.0;
    public const double SustainVolumeEnvelope = 0.0;
    public const double ReleaseVolumeEnvelope = 1.0;
    public const double KeyNumberToVolumeEnvelopeHold = 0.0;
    public const double KeyNumberToVolumeEnvelopeDecay = 0.0;

    public const double DelayModulationEnvelope = 1.0;
    public const double AttackModulationEnvelope = 1.0;
    public const double HoldModulationEnvelope = 1.0;
    public const double DecayModulationEnvelope = 1.0;
    public const double SustainModulationEnvelope = 0.0;
    public const double ReleaseModulationEnvelope = 1.0;
    public const double ModulationEnvelopeToPitch = 0.0;
    public const double ModulationEnvelopeToFilterCutoffFrequency = 0.0;
    public const double KeyNumberToModulationEnvelopeHold = 0.0;
    public const double KeyNumberToModulationEnvelopeDecay = 0.0;

    public const double DelayModulationLfo = 1.0;
    public const double FrequencyModulationLfo = 1.0;
    public const double ModulationLfoToPitch = 0.0;
    public const double ModulationLfoToFilterCutoffFrequency = 0.0;
    public const double ModulationLfoToVolume = 0.0;

    public const double DelayVibratoLfo = 1.0;
    public const double FrequencyVibratoLfo = 1.0;
    public const double VibratoLfoToPitch = 0.0;

    public const double ChorusEffectsSend = 0.0;
    public const double ReverbEffectsSend = 0.0;
}
