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
using CodeBrix.Platform.GameEngine.Audio;
using Wolfenstein.Brix.GameEngine.Assets;
using Wolfenstein.Brix.GameEngine.Audio;
using Wolfenstein.Brix.GameEngine.Logic;

namespace Wolfenstein.Brix.Game;

/// <summary>
/// The AdLib backend: one <see cref="WolfOplSynth"/> (IMF music plus
/// AdLib sound effects on an emulated OPL chip) streamed through a
/// single pull-model <see cref="StreamingAudioSource"/> voice. The
/// fill callback is fast and allocation-free, as the audio-callback
/// contract requires.
/// </summary>
internal sealed class WolfMusic : IDisposable
{
    private readonly WolfAssets assets;
    private readonly GameSession session;
    private readonly WolfOplSynth synth = new WolfOplSynth();
    private readonly StreamingAudioSource stream;
    private int currentTrack = -1;

    public WolfMusic(WolfAssets assets, GameSession session)
    {
        this.assets = assets;
        this.session = session;
        session.Logic.AdlibSoundRequested += OnAdlibSound;
        stream = new StreamingAudioSource(synth.Generate);
        stream.Start();
    }

    private void OnAdlibSound(int soundNumber)
    {
        if (soundNumber >= 0 && soundNumber < AudioTFile.SoundCount)
        {
            synth.PlaySfx(assets.Audio.GetAdlibSound(soundNumber));
        }
    }

    /// <summary>Called once per tic: follows the session's music selection.</summary>
    public void Update()
    {
        synth.Volume = session.MusicVolume / 10.0f;

        var track = session.MusicTrack;
        if (track == currentTrack)
        {
            return;
        }

        currentTrack = track;
        if (track >= 0 && track < assets.Audio.MusicCount)
        {
            synth.PlayMusic(assets.Audio.GetMusic(track));
        }
        else
        {
            synth.StopMusic();
        }
    }

    public void Dispose()
    {
        session.Logic.AdlibSoundRequested -= OnAdlibSound;
        stream.Dispose();
    }
}
