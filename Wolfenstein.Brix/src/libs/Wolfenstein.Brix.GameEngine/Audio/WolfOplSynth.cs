//
// Copyright (C) 2004 Michael Liebscher
// Copyright (C) 1992 Id Software, Inc.
// Copyright (c) 2026 Jeremy Ellis and contributors
//
// The IMF music sequencer and AdLib sound-effect driver are translated
// for Wolfenstein.Brix from the Wolf3D iOS v2.1 GPL source
// (github.com/id-Software/Wolf3D-iOS, commit d7fff51d), file
// wolf3d/wolfextractor/adlib/adlib.c (ADLIB_SetFXInst,
// ADLIB_DecodeSound, ADLIB_LoadMusic, ADLIB_UpdateMusic): music events
// run on the original 700 Hz clock (63 samples per tick at 44100 Hz),
// sound effects on the original 140 Hz clock (every fifth music tick),
// both driving one OPL2-compatible chip.
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

namespace Wolfenstein.Brix.GameEngine.Audio;

/// <summary>
/// The AdLib synthesizer: one emulated OPL chip playing the IMF music
/// chunks and the AdLib sound effects from AUDIOT, mixed into a single
/// stereo stream. <see cref="Generate"/> runs on the audio thread; the
/// Play/Stop control methods may be called from the game-loop thread
/// (a short lock hands the state across).
/// </summary>
public sealed class WolfOplSynth
{
    /// <summary>The output sample rate the synth is created for.</summary>
    public const int SampleRate = 44100;

    // The original clocks: music events at 700 Hz (63 samples per tick
    // at 44100), sound-effect ticks at 140 Hz (every 5th music tick).
    private const int SamplesPerMusicTick = SampleRate / 700;
    private const int MusicTicksPerSfxTick = 5;

    // AdLib register numbers (alChar etc. in the original).
    private const int RegChar = 0x20;
    private const int RegScale = 0x40;
    private const int RegAttack = 0x60;
    private const int RegSus = 0x80;
    private const int RegWave = 0xE0;
    private const int RegFreqL = 0xA0;
    private const int RegFreqH = 0xB0;
    private const int RegFeedCon = 0xC0;

    // Channel 0's operator register offsets (modifiers[0]/carriers[0]).
    private const int Modifier0 = 0;
    private const int Carrier0 = 3;

    private readonly object gate = new object();
    private readonly NukedOpl3 chip = new NukedOpl3();
    private readonly short[] tickBuffer = new short[2];

    // Music state (sqHack* in the original).
    private byte[] musicChunk;
    private int musicOffset;
    private int musicBytesLeft;
    private uint musicTime;
    private uint musicTickCount;
    private bool musicLoop;

    // Sound-effect state.
    private byte[] sfxChunk;
    private int sfxOffset;
    private int sfxBytesLeft;
    private int sfxBlock;
    private int sfxPriority = -1;

    private int musicTickPhase;
    private int sampleInTick;
    private float volume = 1.0f;

    /// <summary>Creates the synth with a silent chip.</summary>
    public WolfOplSynth()
    {
        chip.Reset(SampleRate);
    }

    /// <summary>The output volume applied to the generated stream (0-1).</summary>
    public float Volume
    {
        get => volume;
        set => volume = Math.Clamp(value, 0.0f, 1.0f);
    }

    /// <summary>
    /// Starts an IMF music chunk (a 16-bit data length followed by
    /// reg/value/delay events), replacing any current track.
    /// </summary>
    public void PlayMusic(byte[] imfChunk, bool loop = true)
    {
        lock (gate)
        {
            musicChunk = imfChunk;
            musicLoop = loop;
            RestartMusicLocked();
        }
    }

    /// <summary>Stops the music (sound effects keep playing).</summary>
    public void StopMusic()
    {
        lock (gate)
        {
            musicChunk = null;
            musicBytesLeft = 0;
            SilenceMusicChannelsLocked();
        }
    }

    /// <summary>
    /// Starts an AUDIOT AdLib effect (32-bit length, 16-bit priority,
    /// 16-byte instrument, block byte, then 140 Hz pitch bytes) when
    /// its priority is at least the currently playing effect's.
    /// </summary>
    public void PlaySfx(byte[] audiotChunk)
    {
        if (audiotChunk == null || audiotChunk.Length < 24)
        {
            return;
        }

        var length = BitConverter.ToInt32(audiotChunk, 0);
        int priority = BitConverter.ToUInt16(audiotChunk, 4);
        if (length <= 0 || 23 + length > audiotChunk.Length)
        {
            return;
        }

        lock (gate)
        {
            if (sfxBytesLeft > 0 && priority < sfxPriority)
            {
                return; // A more important effect is still playing.
            }

            sfxChunk = audiotChunk;
            sfxOffset = 23;
            sfxBytesLeft = length;
            sfxPriority = priority;
            sfxBlock = ((audiotChunk[22] & 7) << 2) | 0x20;

            // Set channel 0's instrument (ADLIB_SetFXInst).
            var instrument = audiotChunk.AsSpan(6, 16);
            chip.WriteReg(RegFreqL, 0);
            chip.WriteReg(RegFreqH, 0);
            chip.WriteReg(Modifier0 + RegChar, instrument[0]);
            chip.WriteReg(Modifier0 + RegScale, instrument[2]);
            chip.WriteReg(Modifier0 + RegAttack, instrument[4]);
            chip.WriteReg(Modifier0 + RegSus, instrument[6]);
            chip.WriteReg(Modifier0 + RegWave, instrument[8]);
            chip.WriteReg(Carrier0 + RegChar, instrument[1]);
            chip.WriteReg(Carrier0 + RegScale, instrument[3]);
            chip.WriteReg(Carrier0 + RegAttack, instrument[5]);
            chip.WriteReg(Carrier0 + RegSus, instrument[7]);
            chip.WriteReg(Carrier0 + RegWave, instrument[9]);
            chip.WriteReg(RegFeedCon, 0);
        }
    }

    /// <summary>True while an AdLib effect is still playing.</summary>
    public bool IsSfxPlaying
    {
        get
        {
            lock (gate)
            {
                return sfxBytesLeft > 0;
            }
        }
    }

    /// <summary>
    /// Fills an interleaved stereo float buffer. Fast and
    /// allocation-free; called from the audio thread.
    /// </summary>
    public void Generate(Span<float> buffer)
    {
        lock (gate)
        {
            for (var i = 0; i + 1 < buffer.Length; i += 2)
            {
                if (sampleInTick == 0)
                {
                    RunMusicTickLocked();
                    if (musicTickPhase == 0)
                    {
                        RunSfxTickLocked();
                    }

                    musicTickPhase = (musicTickPhase + 1) % MusicTicksPerSfxTick;
                }

                chip.GenerateResampled(tickBuffer, 0);

                // The chip's raw output is quiet next to the digitized
                // effects; boost like the classic mixers do, clamped.
                const float gain = 4.0f / 32768.0f;
                buffer[i] = Math.Clamp(tickBuffer[0] * gain * volume, -1.0f, 1.0f);
                buffer[i + 1] = Math.Clamp(tickBuffer[1] * gain * volume, -1.0f, 1.0f);

                sampleInTick++;
                if (sampleInTick >= SamplesPerMusicTick)
                {
                    sampleInTick = 0;
                }
            }
        }
    }

    private void RestartMusicLocked()
    {
        if (musicChunk == null || musicChunk.Length < 2)
        {
            musicBytesLeft = 0;
            return;
        }

        musicBytesLeft = BitConverter.ToUInt16(musicChunk, 0);
        musicOffset = 2;
        musicTime = 0;
        musicTickCount = 0;
        if (musicBytesLeft > musicChunk.Length - 2)
        {
            musicBytesLeft = musicChunk.Length - 2;
        }

        SilenceMusicChannelsLocked();
    }

    private void SilenceMusicChannelsLocked()
    {
        // Key off channels 1-8 (channel 0 belongs to sound effects).
        for (var channel = 1; channel <= 8; channel++)
        {
            chip.WriteReg((ushort)(RegFreqH + channel), 0);
        }
    }

    /// <summary>One 700 Hz music tick (the ADLIB_UpdateMusic inner loop).</summary>
    private void RunMusicTickLocked()
    {
        if (musicBytesLeft <= 0)
        {
            return;
        }

        while (musicBytesLeft > 0 && musicTime <= musicTickCount)
        {
            var reg = musicChunk[musicOffset];
            var value = musicChunk[musicOffset + 1];
            var delay = BitConverter.ToUInt16(musicChunk, musicOffset + 2);
            musicOffset += 4;
            musicBytesLeft -= 4;
            musicTime = musicTickCount + delay;

            // Channel 0 belongs to the sound effects while one plays;
            // skip the music's writes to it so effects are not cut.
            var isChannel0 = reg == RegFreqL || reg == RegFreqH || reg == RegFeedCon ||
                reg == RegChar + Modifier0 || reg == RegChar + Carrier0 ||
                reg == RegScale + Modifier0 || reg == RegScale + Carrier0 ||
                reg == RegAttack + Modifier0 || reg == RegAttack + Carrier0 ||
                reg == RegSus + Modifier0 || reg == RegSus + Carrier0 ||
                reg == RegWave + Modifier0 || reg == RegWave + Carrier0;
            if (!(isChannel0 && sfxBytesLeft > 0))
            {
                chip.WriteReg(reg, value);
            }
        }

        musicTickCount++;

        if (musicBytesLeft <= 0 && musicLoop)
        {
            RestartMusicLocked();
        }
    }

    /// <summary>One 140 Hz sound-effect tick (the ADLIB_DecodeSound loop body).</summary>
    private void RunSfxTickLocked()
    {
        if (sfxBytesLeft <= 0)
        {
            return;
        }

        var pitch = sfxChunk[sfxOffset++];
        sfxBytesLeft--;

        if (pitch == 0)
        {
            chip.WriteReg(RegFreqH, 0);
        }
        else
        {
            chip.WriteReg(RegFreqL, pitch);
            chip.WriteReg(RegFreqH, (byte)sfxBlock);
        }

        if (sfxBytesLeft == 0)
        {
            chip.WriteReg(RegFreqH, 0);
            sfxPriority = -1;
        }
    }
}
