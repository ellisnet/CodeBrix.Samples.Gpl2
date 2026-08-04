// Wolfenstein.Brix — GPLv2 (see the repo LICENSE).
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
using Wolfenstein.Brix.GameEngine.Assets;
using Wolfenstein.Brix.GameEngine.Logic;

namespace Wolfenstein.Brix.Game;

/// <summary>
/// The digitized-sound backend: registers every shareware VSWAP sound
/// (raw 8-bit unsigned mono at ~7000 Hz) with the engine's
/// <see cref="AudioResourceManager"/> once, then plays requests from
/// the game logic on a small pool of <see cref="SoundChannel"/>s
/// (rate conversion is built into the channel). Runs entirely on the
/// game-loop thread.
/// </summary>
internal sealed class WolfSound : IDisposable
{
    private const int ChannelCount = 8;
    private const int DigitizedSampleRate = 7000;

    private readonly SoundChannel[] channels;
    private readonly string[] channelClipKeys;
    private readonly bool[] clipRegistered;
    private int nextChannel;
    private volatile float volume = 1.0f;

    /// <summary>The playback volume for digitized effects (0-1).</summary>
    public float Volume
    {
        get => volume;
        set => volume = Math.Clamp(value, 0.0f, 1.0f);
    }

    public WolfSound(WolfAssets assets, WolfLogic logic)
    {
        var sounds = assets.Vswap.DigitizedSounds;
        clipRegistered = new bool[sounds.Length];
        for (var i = 0; i < sounds.Length; i++)
        {
            if (sounds[i].Length == 0)
            {
                continue;
            }

            AudioResourceManager.Instance.LoadFromPcm(
                ClipKey(i), sounds[i], DigitizedSampleRate, bitsPerSample: 8, channels: 1);
            clipRegistered[i] = true;
        }

        channels = new SoundChannel[ChannelCount];
        channelClipKeys = new string[ChannelCount];
        for (var i = 0; i < channels.Length; i++)
        {
            channels[i] = new SoundChannel();
        }

        logic.DigitizedSoundRequested += Play;
    }

    private static string ClipKey(int index) => "wolf-digi-" + index;

    /// <summary>Plays a digitized sound by its shareware VSWAP number.</summary>
    public void Play(int soundNumber)
    {
        if (soundNumber < 0 || soundNumber >= clipRegistered.Length || !clipRegistered[soundNumber])
        {
            return; // AdLib-only in shareware (Phase 6 covers these).
        }

        // Prefer an idle channel; otherwise steal round-robin (the
        // original's single digi channel stole unconditionally).
        var channelIndex = -1;
        for (var i = 0; i < channels.Length; i++)
        {
            if (channels[i].State != PlaybackState.Playing)
            {
                channelIndex = i;
                break;
            }
        }

        if (channelIndex < 0)
        {
            channelIndex = nextChannel;
            nextChannel = (nextChannel + 1) % channels.Length;
        }

        var channel = channels[channelIndex];
        var key = ClipKey(soundNumber);
        if (channelClipKeys[channelIndex] != key)
        {
            channel.SetClip(key);
            channelClipKeys[channelIndex] = key;
        }

        channel.Play(volume: volume, pan: 0.0f);
    }

    public void Dispose()
    {
        foreach (var channel in channels)
        {
            channel.Dispose();
        }
    }
}
