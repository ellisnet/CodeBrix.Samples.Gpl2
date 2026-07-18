// Doom.Brix — GPLv2 (see the repo LICENSE); the CodeBrix.Platform.GameEngine
// replacement for managed-doom's Silk music backend (a port of upstream
// SilkMusic's MusStream onto a pull-model StreamingAudioSource).
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//

using System;
using CodeBrix.Platform.GameEngine.Audio;
using ManagedDoom;
using ManagedDoom.Audio;
using Doom.Brix.Synth;

namespace Doom.Brix.Game;

/// <summary>
/// The game core's <see cref="IMusic"/> backend: WAD music lumps decode through the
/// extracted MUS/MIDI decoders and render through MeltySynth (with the shipped
/// TimGM6mb SoundFont) inside the fill callback of one endless
/// <see cref="StreamingAudioSource"/> voice. The callback runs on the audio thread;
/// MeltySynth renders faster than real time, matching upstream usage.
/// </summary>
internal sealed class CodeBrixMusic : IMusic, IDisposable
{
    private readonly Config config;
    private readonly Wad wad;

    private readonly Synthesizer synthesizer;
    private StreamingAudioSource stream;

    private float[] left;
    private float[] right;

    private volatile IMusicDecoder current;
    private volatile IMusicDecoder reserved;

    private Bgm currentBgm;
    private bool started;

    public CodeBrixMusic(Config config, GameContent content, string soundFontPath)
    {
        this.config = config;
        wad = content.Wad;

        config.audio_musicvolume = Math.Clamp(config.audio_musicvolume, 0, MaxVolume);

        var settings = new SynthesizerSettings(MusDecoder.SampleRate);
        settings.BlockSize = MusDecoder.BlockLength;
        settings.EnableReverbAndChorus = config.audio_musiceffect;
        synthesizer = new Synthesizer(soundFontPath, settings);

        // Generously sized up front so the audio callback never allocates; grown in
        // the (unexpected) case of a larger device period.
        left = new float[8192];
        right = new float[8192];

        stream = new StreamingAudioSource(FillBuffer);
        currentBgm = Bgm.NONE;
    }

    public void StartMusic(Bgm bgm, bool loop)
    {
        if (bgm == currentBgm)
        {
            return;
        }

        var lump = "D_" + DoomInfo.BgmNames[(int)bgm].ToString().ToUpper();
        var data = wad.ReadLump(lump);
        reserved = MusicDecoderFactory.Create(data, loop);

        if (!started)
        {
            stream.Start();
            started = true;
        }

        currentBgm = bgm;
    }

    private void FillBuffer(Span<float> buffer)
    {
        var frames = buffer.Length / 2;

        var decoder = reserved;
        if (decoder == null)
        {
            buffer.Clear();
            return;
        }

        if (!ReferenceEquals(decoder, current))
        {
            synthesizer.Reset();
            current = decoder;
        }

        if (left.Length < frames)
        {
            left = new float[frames];
            right = new float[frames];
        }

        decoder.RenderWaveform(synthesizer, left.AsSpan(0, frames), right.AsSpan(0, frames));

        // Upstream scales by 32768 * (2 * volume / MaxVolume) and hard-clips to the
        // short range; in the float pipeline that is a 2x gain clipped to [-1, 1].
        var a = 2.0F * config.audio_musicvolume / MaxVolume;
        var pos = 0;
        for (var t = 0; t < frames; t++)
        {
            buffer[pos++] = Math.Clamp(a * left[t], -1F, 1F);
            buffer[pos++] = Math.Clamp(a * right[t], -1F, 1F);
        }
    }

    public int MaxVolume => 15;

    public int Volume
    {
        get
        {
            return config.audio_musicvolume;
        }

        set
        {
            config.audio_musicvolume = value;
        }
    }

    public void Dispose()
    {
        if (stream != null)
        {
            stream.Dispose();
            stream = null;
        }
    }
}
