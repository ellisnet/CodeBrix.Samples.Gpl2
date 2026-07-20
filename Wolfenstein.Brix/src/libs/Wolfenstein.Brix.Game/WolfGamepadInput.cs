// Wolfenstein.Brix — GPLv2 (see the repo LICENSE); the gamepad half of the
// per-tic input, following Doom.Brix's DoomGamepadInput. The original has no
// controller support, so nothing here is ported; the weapon-cycling rules do
// mirror PlayerLogic.ChangeWeapon, which silently refuses a weapon that is not
// owned or has no ammo.
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
using System.Linq;
using CodeBrix.Platform.GameEngine.Input.Gamepad;
using CodeBrix.Platform.GameEngine.Sdl2.Gamepad;
using Wolfenstein.Brix.GameEngine.Logic;

namespace Wolfenstein.Brix.Game;

/// <summary>
/// One tic's worth of gamepad input: analog axes normalized to [-1, 1], buttons as held
/// state, and the menu edges the shell consumes.
/// </summary>
internal readonly struct WolfGamepadSample
{
    /// <summary>Forward movement, positive forward.</summary>
    public float Forward { get; init; }

    /// <summary>Strafe movement, positive right.</summary>
    public float Side { get; init; }

    /// <summary>Turn, positive right (clockwise, the direction the right-arrow key turns).</summary>
    public float Turn { get; init; }

    /// <summary>The attack button (right trigger).</summary>
    public bool Fire { get; init; }

    /// <summary>The use button (A).</summary>
    public bool Use { get; init; }

    /// <summary>The run modifier (left trigger).</summary>
    public bool Run { get; init; }

    /// <summary>The weapon slot the shoulder buttons selected (1-4), or 0 for no change.</summary>
    public int WeaponSlot { get; init; }

    /// <summary>Menu cursor up (edge).</summary>
    public bool MenuUp { get; init; }

    /// <summary>Menu cursor down (edge).</summary>
    public bool MenuDown { get; init; }

    /// <summary>Menu activate (edge, from A).</summary>
    public bool MenuActivate { get; init; }

    /// <summary>Menu back / open menu (edge, from B and Start).</summary>
    public bool MenuBack { get; init; }
}

/// <summary>
/// The menu cursor's repeat clock: a fresh direction fires at once, a held one fires
/// again after a delay and then at a steady rate. The keyboard path raises one edge per
/// press, so without this a held stick or D-pad would move the cursor exactly one line.
/// </summary>
internal sealed class WolfMenuRepeatClock
{
    private readonly int delayTics;
    private readonly int intervalTics;
    private int heldDirection;
    private int heldTics;

    public WolfMenuRepeatClock(int delayTics, int intervalTics)
    {
        this.delayTics = delayTics;
        this.intervalTics = intervalTics;
    }

    /// <summary>Advances one tic and reports whether the cursor should move.</summary>
    /// <param name="direction">
    /// The direction being held (<see cref="WolfGamepadInput.NavUp"/> /
    /// <see cref="WolfGamepadInput.NavDown"/>), or
    /// <see cref="WolfGamepadInput.NavNone"/> for none.
    /// </param>
    public bool Tick(int direction)
    {
        if (direction == WolfGamepadInput.NavNone)
        {
            Reset();
            return false;
        }

        if (direction != heldDirection)
        {
            heldDirection = direction;
            heldTics = 0;
            return true;
        }

        heldTics++;
        if (heldTics < delayTics)
        {
            return false;
        }

        return (heldTics - delayTics) % intervalTics == 0;
    }

    /// <summary>Forgets the held direction, so the next one fires immediately.</summary>
    public void Reset()
    {
        heldDirection = WolfGamepadInput.NavNone;
        heldTics = 0;
    }
}

/// <summary>
/// Reads the connected controller once per tic. Always on when a controller is present —
/// there is no setting to enable — and completely inert when there is not, so the game
/// stays a keyboard game that a controller happens to also drive.
/// </summary>
/// <remarks>
/// <para>
/// The mapping: left stick moves and strafes, right stick X turns (its Y is ignored),
/// right trigger attacks, left trigger runs, A uses, the shoulder buttons cycle weapons,
/// and Start opens and closes the menu. Back is unmapped — Wolfenstein has no automap
/// for it to toggle.
/// </para>
/// <para>
/// In the menus the D-pad and the left stick move the cursor (with a repeat delay), A
/// activates and B goes back. Those edges are produced whatever is on screen, exactly as
/// the keyboard's are: only the non-playing screens read them.
/// </para>
/// </remarks>
internal sealed class WolfGamepadInput
{
    /// <summary>The direction token for a released cursor.</summary>
    internal const int NavNone = 0;

    /// <summary>The direction token for the cursor moving up.</summary>
    internal const int NavUp = 1;

    /// <summary>The direction token for the cursor moving down.</summary>
    internal const int NavDown = -1;

    // Deadzones. The sticks are read through the engine's WithDeadzone(), a radial gate
    // on the whole stick; a resting stick drifts by 1500-2900 raw units.
    private const float MoveDeadzone = 0.20f;
    private const float TurnDeadzone = 0.20f;

    // A trigger is an analog axis; half-pull is the press.
    private const float TriggerThreshold = 0.5f;

    // The left stick doubles as a menu D-pad, so it needs a firmer gate than movement
    // does: a cursor that jumps on stick drift is worse than one that needs a push.
    private const float MenuStickThreshold = 0.6f;

    // Menu cursor repeat, in tics at Wolfenstein's 70 Hz: about a third of a second
    // before the first repeat, then roughly nine a second.
    private const int MenuRepeatDelayTics = 24;
    private const int MenuRepeatIntervalTics = 8;

    internal const int WeaponSlotCount = 4;

    private readonly IGamepadManager<IGamepadAdapter> manager;
    private readonly WolfMenuRepeatClock menuRepeat =
        new WolfMenuRepeatClock(MenuRepeatDelayTics, MenuRepeatIntervalTics);

    // Held state as of the previous tic, for edge detection. The engine's poller could
    // raise ButtonDown events instead, but its registrations are per-GamepadId and a
    // controller that sleeps and wakes comes back with a NEW id — polling the adapter
    // list every tic sidesteps that re-registration entirely.
    private bool prevA;
    private bool prevB;
    private bool prevStart;
    private bool prevLeftShoulder;
    private bool prevRightShoulder;

    public WolfGamepadInput(IGamepadManager<IGamepadAdapter> gamepadManager)
    {
        manager = gamepadManager;
    }

    /// <summary>
    /// Samples the controller for this tic. Returns an all-default sample (every fold-in
    /// a no-op) when no controller is connected.
    /// </summary>
    /// <param name="player">The player, for weapon cycling; null outside a game.</param>
    public WolfGamepadSample Sample(PlayerState player)
    {
        IGamepadAdapter pad = FirstConnectedPad();
        if (pad == null)
        {
            ResetHeldState();
            return default;
        }

        GamepadStickState move = pad.LeftStick?.WithDeadzone(MoveDeadzone) ?? default;
        GamepadStickState look = pad.RightStick?.WithDeadzone(TurnDeadzone) ?? default;

        // Magnitude legitimately exceeds 1 on a corner-held stick (up to about 1.25 on a
        // real pad, because X and Y are clamped independently). Scaling movement by the
        // raw components would hand out that extra as diagonal speed, which is the
        // classic diagonal-speed-boost bug; shrink the vector back onto the unit circle.
        (float forward, float side) = ClampToUnitCircle(move.Y, move.X);

        bool a = pad.PressedButtons.Contains(SdlGamepadButtons.A);
        bool b = pad.PressedButtons.Contains(SdlGamepadButtons.B);
        bool start = pad.PressedButtons.Contains(SdlGamepadButtons.Start);
        bool leftShoulder = pad.PressedButtons.Contains(SdlGamepadButtons.LeftShoulder);
        bool rightShoulder = pad.PressedButtons.Contains(SdlGamepadButtons.RightShoulder);

        var weaponSlot = 0;
        if (rightShoulder && !prevRightShoulder)
        {
            weaponSlot = NextWeaponSlot(player, forward: true);
        }
        else if (leftShoulder && !prevLeftShoulder)
        {
            weaponSlot = NextWeaponSlot(player, forward: false);
        }

        int navDirection = ReadNavigationDirection(pad, move);
        bool navFired = menuRepeat.Tick(navDirection);

        var sample = new WolfGamepadSample
        {
            Forward = forward,
            Side = side,
            Turn = look.X,
            Fire = pad.RightTrigger > TriggerThreshold,
            Use = a,
            Run = pad.LeftTrigger > TriggerThreshold,
            WeaponSlot = weaponSlot,
            MenuUp = navFired && navDirection == NavUp,
            MenuDown = navFired && navDirection == NavDown,
            MenuActivate = a && !prevA,
            MenuBack = (b && !prevB) || (start && !prevStart),
        };

        prevA = a;
        prevB = b;
        prevStart = start;
        prevLeftShoulder = leftShoulder;
        prevRightShoulder = rightShoulder;

        return sample;
    }

    private IGamepadAdapter FirstConnectedPad()
    {
        if (manager == null)
        {
            return null;
        }

        // Player one is whichever pad enumerates first. Wolfenstein is single-player
        // here, so there is nothing to assign a second one to.
        foreach (IGamepadAdapter adapter in manager.ConnectedAdapters)
        {
            return adapter;
        }

        return null;
    }

    private void ResetHeldState()
    {
        prevA = prevB = prevStart = prevLeftShoulder = prevRightShoulder = false;
        menuRepeat.Reset();
    }

    // Every Wolfenstein menu is a vertical list, so only up and down are read; the
    // D-pad and the left stick are one input.
    private static int ReadNavigationDirection(IGamepadAdapter pad, GamepadStickState move)
    {
        if (pad.PressedButtons.Contains(SdlGamepadButtons.DPadUp) || move.Y >= MenuStickThreshold)
        {
            return NavUp;
        }

        if (pad.PressedButtons.Contains(SdlGamepadButtons.DPadDown) || move.Y <= -MenuStickThreshold)
        {
            return NavDown;
        }

        return NavNone;
    }

    /// <summary>
    /// Shrinks a stick vector whose magnitude exceeds 1 back onto the unit circle,
    /// leaving everything inside it untouched.
    /// </summary>
    internal static (float Y, float X) ClampToUnitCircle(float y, float x)
    {
        float magnitude = MathF.Sqrt((x * x) + (y * y));
        return magnitude > 1f ? (y / magnitude, x / magnitude) : (y, x);
    }

    /// <summary>
    /// Finds the next selectable weapon slot (1-4) in cycle order, or 0 when there is
    /// nothing to switch to. Mirrors PlayerLogic.ChangeWeapon's rules — an unowned
    /// weapon, or any weapon but the knife with no ammo, is skipped rather than
    /// selected and silently refused.
    /// </summary>
    /// <param name="player">The player, or null outside a game.</param>
    /// <param name="forward">True to cycle up the slots, false to cycle down.</param>
    internal static int NextWeaponSlot(PlayerState player, bool forward)
    {
        if (player == null)
        {
            return 0;
        }

        var current = (int)player.CurrentWeapon;
        int step = forward ? 1 : WeaponSlotCount - 1;

        for (var i = 1; i < WeaponSlotCount; i++)
        {
            int weapon = (current + (step * i)) % WeaponSlotCount;
            if (IsSelectable(player, weapon))
            {
                return weapon + 1;
            }
        }

        return 0;
    }

    /// <summary>Tests whether a weapon (0-3) can be switched to right now.</summary>
    internal static bool IsSelectable(PlayerState player, int weapon)
    {
        if (player.Ammo == 0 && (Weapon)weapon != Weapon.Knife)
        {
            return false;
        }

        var itemFlag = (PlayerItems)((int)PlayerItems.Weapon1 << weapon);
        return (player.Items & itemFlag) != 0;
    }
}
