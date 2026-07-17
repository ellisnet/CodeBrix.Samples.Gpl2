//
// Copyright (C) 1993-1996 Id Software, Inc.
// Copyright (C) 2019-2020 Nobuaki Tanaka
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
using System.IO;
using MeltySynth;

namespace ManagedDoom.Audio;

// Extracted from the private MidiDecoder class nested in upstream's
// Silk/SilkMusic.cs; the decoding logic is unchanged.
public sealed class MidiDecoder : IMusicDecoder
{
    public static readonly byte[] MidiHeader = new byte[]
    {
        (byte)'M',
        (byte)'T',
        (byte)'h',
        (byte)'d'
    };

    private MidiFile midi;
    private MidiFileSequencer sequencer;

    private bool loop;

    public MidiDecoder(byte[] data, bool loop)
    {
        midi = new MidiFile(new MemoryStream(data));

        this.loop = loop;
    }

    public void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right)
    {
        if (sequencer == null)
        {
            sequencer = new MidiFileSequencer(synthesizer);
            sequencer.Play(midi, loop);
        }

        sequencer.Render(left, right);
    }
}
