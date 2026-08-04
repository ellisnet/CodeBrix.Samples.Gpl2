// Doom.Brix — GPLv2 (see the repo LICENSE); the CodeBrix.Platform.GameEngine
// replacement for managed-doom's Silk sound backend (a close port of upstream
// SilkSound: same channel model, priorities, distance decay, and pitch rules).
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
using CodeBrix.Audio.Wave;
using CodeBrix.Platform.GameEngine.Audio;
using ManagedDoom;
using ManagedDoom.Audio;

namespace Doom.Brix.Game;

/// <summary>
/// The game core's <see cref="ISound"/> backend: eight game channels plus one UI
/// channel, each a <see cref="SoundChannel"/> voice over the pinned shared audio
/// device. The WAD's 8-bit mono DMX sound lumps are registered once as PCM clips;
/// per play, the channel applies Doom's listener-relative pan, distance-decay
/// volume, and (when enabled) random pitch.
/// </summary>
internal sealed class CodeBrixSound : ISound, IDisposable
{
    private const int GameChannelCount = 8;
    private const string ClipKeyPrefix = "doom_sfx_";

    private static readonly float fastDecay = (float)Math.Pow(0.5, 1.0 / (35 / 5));
    private static readonly float slowDecay = (float)Math.Pow(0.5, 1.0 / 35);

    private static readonly float clipDist = 1200;
    private static readonly float closeDist = 160;
    private static readonly float attenuator = clipDist - closeDist;

    private readonly Config config;

    private readonly bool[] hasClip;
    private readonly float[] amplitudes;

    private readonly DoomRandom random;

    private SoundChannel[] channels;
    private readonly ChannelInfo[] infos;

    private SoundChannel uiChannel;
    private Sfx uiReserved;

    private Mobj listener;

    private float masterVolumeDecay;

    private DateTime lastUpdate;

    public CodeBrixSound(Config config, GameContent content)
    {
        this.config = config;

        config.audio_soundvolume = Math.Clamp(config.audio_soundvolume, 0, MaxVolume);

        hasClip = new bool[DoomInfo.SfxNames.Length];
        amplitudes = new float[DoomInfo.SfxNames.Length];

        if (config.audio_randompitch)
        {
            random = new DoomRandom();
        }

        for (var i = 0; i < DoomInfo.SfxNames.Length; i++)
        {
            var name = "DS" + DoomInfo.SfxNames[i].ToString().ToUpper();
            if (content.Wad.GetLumpNumber(name) == -1)
            {
                continue;
            }

            var samples = GetSamples(content.Wad, name, out var sampleRate, out var sampleCount);
            if (!samples.IsEmpty)
            {
                AudioResourceManager.Instance.LoadFromPcm(
                    ClipKeyPrefix + i, samples.ToArray(), sampleRate, 8);
                hasClip[i] = true;
                amplitudes[i] = GetAmplitude(samples, sampleRate, sampleCount);
            }
        }

        channels = new SoundChannel[GameChannelCount];
        infos = new ChannelInfo[GameChannelCount];
        for (var i = 0; i < channels.Length; i++)
        {
            channels[i] = new SoundChannel();
            infos[i] = new ChannelInfo();
        }

        uiChannel = new SoundChannel();
        uiReserved = Sfx.NONE;

        masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;

        lastUpdate = DateTime.MinValue;
    }

    private static Span<byte> GetSamples(Wad wad, string name, out int sampleRate, out int sampleCount)
    {
        var data = wad.ReadLump(name);

        if (data.Length < 8)
        {
            sampleRate = -1;
            sampleCount = -1;
            return null;
        }

        sampleRate = BitConverter.ToUInt16(data, 2);
        sampleCount = BitConverter.ToInt32(data, 4);

        var offset = 8;
        if (ContainsDmxPadding(data))
        {
            offset += 16;
            sampleCount -= 32;
        }

        if (sampleCount > 0)
        {
            return data.AsSpan(offset, sampleCount);
        }
        else
        {
            return Span<byte>.Empty;
        }
    }

    // Check if the data contains pad bytes.
    // If the first and last 16 samples are the same,
    // the data should contain pad bytes.
    // https://doomwiki.org/wiki/Sound
    private static bool ContainsDmxPadding(byte[] data)
    {
        var sampleCount = BitConverter.ToInt32(data, 4);
        if (sampleCount < 32)
        {
            return false;
        }
        else
        {
            var first = data[8];
            for (var i = 1; i < 16; i++)
            {
                if (data[8 + i] != first)
                {
                    return false;
                }
            }

            var last = data[8 + sampleCount - 1];
            for (var i = 1; i < 16; i++)
            {
                if (data[8 + sampleCount - i - 1] != last)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static float GetAmplitude(Span<byte> samples, int sampleRate, int sampleCount)
    {
        var max = 0;
        if (sampleCount > 0)
        {
            var count = Math.Min(sampleRate / 5, sampleCount);
            for (var t = 0; t < count; t++)
            {
                var a = samples[t] - 128;
                if (a < 0)
                {
                    a = -a;
                }
                if (a > max)
                {
                    max = a;
                }
            }
        }
        return (float)max / 128;
    }

    public void SetListener(Mobj listener)
    {
        this.listener = listener;
    }

    public void Update()
    {
        var now = DateTime.Now;
        if ((now - lastUpdate).TotalSeconds < 0.01)
        {
            // Don't update so frequently (for timedemo).
            return;
        }

        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            var channel = channels[i];

            if (info.Playing != Sfx.NONE)
            {
                if (channel.State != PlaybackState.Stopped)
                {
                    if (info.Type == SfxType.Diffuse)
                    {
                        info.Priority *= slowDecay;
                    }
                    else
                    {
                        info.Priority *= fastDecay;
                    }
                    SetParam(channel, info);
                }
                else
                {
                    info.Playing = Sfx.NONE;
                    if (info.Reserved == Sfx.NONE)
                    {
                        info.Source = null;
                    }
                }
            }

            if (info.Reserved != Sfx.NONE)
            {
                if (info.Playing != Sfx.NONE)
                {
                    channel.Stop();
                }

                channel.SetClip(ClipKeyPrefix + (int)info.Reserved);
                SetParam(channel, info);
                channel.Play(pitch: GetPitch(info.Type, info.Reserved));
                info.Playing = info.Reserved;
                info.Reserved = Sfx.NONE;
            }
        }

        if (uiReserved != Sfx.NONE)
        {
            if (uiChannel.State == PlaybackState.Playing)
            {
                uiChannel.Stop();
            }
            uiChannel.SetClip(ClipKeyPrefix + (int)uiReserved);
            uiChannel.Play(volume: masterVolumeDecay, pan: 0f, pitch: 1f);
            uiReserved = Sfx.NONE;
        }

        lastUpdate = now;
    }

    public void StartSound(Sfx sfx)
    {
        if (!hasClip[(int)sfx])
        {
            return;
        }

        uiReserved = sfx;
    }

    public void StartSound(Mobj mobj, Sfx sfx, SfxType type)
    {
        StartSound(mobj, sfx, type, 100);
    }

    public void StartSound(Mobj mobj, Sfx sfx, SfxType type, int volume)
    {
        if (!hasClip[(int)sfx])
        {
            return;
        }

        var x = (mobj.X - listener.X).ToFloat();
        var y = (mobj.Y - listener.Y).ToFloat();
        var dist = MathF.Sqrt(x * x + y * y);

        float priority;
        if (type == SfxType.Diffuse)
        {
            priority = volume;
        }
        else
        {
            priority = amplitudes[(int)sfx] * GetDistanceDecay(dist) * volume;
        }

        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Source == mobj && info.Type == type)
            {
                info.Reserved = sfx;
                info.Priority = priority;
                info.Volume = volume;
                return;
            }
        }

        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Reserved == Sfx.NONE && info.Playing == Sfx.NONE)
            {
                info.Reserved = sfx;
                info.Priority = priority;
                info.Source = mobj;
                info.Type = type;
                info.Volume = volume;
                return;
            }
        }

        var minPriority = float.MaxValue;
        var minChannel = -1;
        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Priority < minPriority)
            {
                minPriority = info.Priority;
                minChannel = i;
            }
        }
        if (priority >= minPriority)
        {
            var info = infos[minChannel];
            info.Reserved = sfx;
            info.Priority = priority;
            info.Source = mobj;
            info.Type = type;
            info.Volume = volume;
        }
    }

    public void StopSound(Mobj mobj)
    {
        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            if (info.Source == mobj)
            {
                info.LastX = info.Source.X;
                info.LastY = info.Source.Y;
                info.Source = null;
                info.Volume /= 5;
            }
        }
    }

    public void Reset()
    {
        if (random != null)
        {
            random.Clear();
        }

        for (var i = 0; i < infos.Length; i++)
        {
            channels[i].Stop();
            infos[i].Clear();
        }

        listener = null;
    }

    public void Pause()
    {
        for (var i = 0; i < infos.Length; i++)
        {
            var channel = channels[i];

            if (channel.State == PlaybackState.Playing &&
                channel.Duration - channel.Position > TimeSpan.FromMilliseconds(200))
            {
                channel.Pause();
            }
        }
    }

    public void Resume()
    {
        for (var i = 0; i < infos.Length; i++)
        {
            var channel = channels[i];

            if (channel.State == PlaybackState.Paused)
            {
                channel.Resume();
            }
        }
    }

    private void SetParam(SoundChannel channel, ChannelInfo info)
    {
        if (info.Type == SfxType.Diffuse)
        {
            channel.Pan = 0f;
            channel.Volume = 0.01F * masterVolumeDecay * info.Volume;
        }
        else
        {
            Fixed sourceX;
            Fixed sourceY;
            if (info.Source == null)
            {
                sourceX = info.LastX;
                sourceY = info.LastY;
            }
            else
            {
                sourceX = info.Source.X;
                sourceY = info.Source.Y;
            }

            var x = (sourceX - listener.X).ToFloat();
            var y = (sourceY - listener.Y).ToFloat();

            if (Math.Abs(x) < 16 && Math.Abs(y) < 16)
            {
                channel.Pan = 0f;
                channel.Volume = 0.01F * masterVolumeDecay * info.Volume;
            }
            else
            {
                var dist = MathF.Sqrt(x * x + y * y);
                var angle = MathF.Atan2(y, x) - (float)listener.Angle.ToRadian();

                // Upstream positions the OpenAL source at (-sin(angle), 0, -cos(angle));
                // the equivalent stereo pan is the x component.
                channel.Pan = -MathF.Sin(angle);
                channel.Volume = 0.01F * masterVolumeDecay * GetDistanceDecay(dist) * info.Volume;
            }
        }
    }

    private float GetDistanceDecay(float dist)
    {
        if (dist < closeDist)
        {
            return 1F;
        }
        else
        {
            return Math.Max((clipDist - dist) / attenuator, 0F);
        }
    }

    private float GetPitch(SfxType type, Sfx sfx)
    {
        if (random != null)
        {
            if (sfx == Sfx.ITEMUP || sfx == Sfx.TINK || sfx == Sfx.RADIO)
            {
                return 1.0F;
            }
            else if (type == SfxType.Voice)
            {
                return 1.0F + 0.075F * (random.Next() - 128) / 128;
            }
            else
            {
                return 1.0F + 0.025F * (random.Next() - 128) / 128;
            }
        }
        else
        {
            return 1.0F;
        }
    }

    public void Dispose()
    {
        if (channels != null)
        {
            for (var i = 0; i < channels.Length; i++)
            {
                if (channels[i] != null)
                {
                    channels[i].Stop();
                    channels[i].Dispose();
                    channels[i] = null;
                }
            }
            channels = null;
        }

        if (uiChannel != null)
        {
            uiChannel.Dispose();
            uiChannel = null;
        }
    }

    public int MaxVolume => 15;

    public int Volume
    {
        get
        {
            return config.audio_soundvolume;
        }

        set
        {
            config.audio_soundvolume = value;
            masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;
        }
    }

    private class ChannelInfo
    {
        public Sfx Reserved;
        public Sfx Playing;
        public float Priority;

        public Mobj Source;
        public SfxType Type;
        public int Volume;
        public Fixed LastX;
        public Fixed LastY;

        public void Clear()
        {
            Reserved = Sfx.NONE;
            Playing = Sfx.NONE;
            Priority = 0;
            Source = null;
            Type = 0;
            Volume = 0;
            LastX = Fixed.Zero;
            LastY = Fixed.Zero;
        }
    }
}
