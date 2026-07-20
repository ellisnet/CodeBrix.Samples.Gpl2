// Doom.Brix — GPLv2 (see the repo LICENSE); the gamepad half of the game core's
// user input. Nothing here is ported from managed-doom, which has no controller
// support; the weapon-cycling slot order does mirror the slot numbering that
// PlayerBehavior applies to TicCmdButtons.Change.
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
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Platform.GameEngine.Input.Gamepad;
using CodeBrix.Platform.GameEngine.Sdl2.Gamepad;
using ManagedDoom;

namespace Doom.Brix.Game;

/// <summary>
/// One tic's worth of gamepad input, in the same terms <see cref="CodeBrixUserInput.BuildTicCmd"/>
/// already works in: analog axes normalized to [-1, 1] and buttons as held state.
/// </summary>
internal readonly struct DoomGamepadSample
{
    /// <summary>Forward movement, positive forward.</summary>
    public float Forward { get; init; }

    /// <summary>Strafe movement, positive right.</summary>
    public float Side { get; init; }

    /// <summary>Turn, positive right (the direction the key_turnright binding turns).</summary>
    public float Turn { get; init; }

    /// <summary>The fire button (right trigger).</summary>
    public bool Fire { get; init; }

    /// <summary>The use/open button (A).</summary>
    public bool Use { get; init; }

    /// <summary>The run modifier (left trigger).</summary>
    public bool Run { get; init; }

    /// <summary>
    /// The weapon the shoulder buttons selected this tic, as a ONE-BASED slot number
    /// (1-7), or 0 for no change.
    /// </summary>
    /// <remarks>
    /// One-based deliberately: <c>default</c> is what a tic with no controller connected
    /// produces, and it has to mean "change nothing". Zero-based would make the default
    /// sample ask for slot 0 — the fist — on every tic of a keyboard-only game.
    /// </remarks>
    public int WeaponSlot { get; init; }
}

/// <summary>
/// The menu cursor's repeat clock: a fresh direction fires at once, a held one fires
/// again after a delay and then at a steady rate. Doom's own menus have no key repeat —
/// the keyboard path deliberately drops the OS's repeats — so without this a held stick
/// or D-pad would move the cursor exactly one line.
/// </summary>
internal sealed class DoomMenuRepeatClock
{
    private readonly int delayTics;
    private readonly int intervalTics;
    private DoomKey heldKey = DoomKey.Unknown;
    private int heldTics;

    public DoomMenuRepeatClock(int delayTics, int intervalTics)
    {
        this.delayTics = delayTics;
        this.intervalTics = intervalTics;
    }

    /// <summary>
    /// Advances one tic and reports whether the cursor should move.
    /// </summary>
    /// <param name="key">
    /// The direction being held, or <see cref="DoomKey.Unknown"/> for none.
    /// </param>
    public bool Tick(DoomKey key)
    {
        if (key == DoomKey.Unknown)
        {
            Reset();
            return false;
        }

        if (key != heldKey)
        {
            heldKey = key;
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
        heldKey = DoomKey.Unknown;
        heldTics = 0;
    }
}

/// <summary>
/// Reads the connected controller once per tic and turns it into two things: a
/// <see cref="DoomGamepadSample"/> that the tic command folds in, and synthesized
/// <see cref="DoomKey"/> edges for the menus. Always on when a controller is present —
/// there is no setting to enable — and completely inert when there is not, so the game
/// stays a keyboard-and-mouse game that a controller happens to also drive.
/// </summary>
/// <remarks>
/// <para>
/// The mapping: left stick moves and strafes, right stick X turns (its Y is ignored),
/// right trigger fires, left trigger runs, A uses/opens, the shoulder buttons cycle
/// weapons, Start opens and closes the menu, and Back toggles the automap.
/// </para>
/// <para>
/// In the menus the D-pad and the left stick move the cursor (with a repeat delay), A is
/// Enter and B is Escape. Those nav edges are synthesized ONLY while a menu is up or the
/// game is not in play: <c>AutoMap.DoEvent</c> treats the arrow keys as press-and-hold
/// pan, so a repeating synthesized KeyDown reaching it would pan the map forever.
/// </para>
/// </remarks>
internal sealed class DoomGamepadInput
{
    // Deadzones. The sticks are read through the engine's WithDeadzone(), which is a
    // radial gate on the whole stick rather than a per-axis one; a resting stick reads
    // 1500-2900 raw units of drift, so anything below this is noise.
    private const float MoveDeadzone = 0.20f;
    private const float TurnDeadzone = 0.20f;

    // A trigger is an analog axis; half-pull is the press.
    private const float TriggerThreshold = 0.5f;

    // The left stick doubles as a menu D-pad, so it needs a firmer gate than movement
    // does: a cursor that jumps on stick drift is worse than one that needs a push.
    private const float MenuStickThreshold = 0.6f;

    // Menu cursor repeat, in tics at Doom's 35 Hz: about a third of a second before the
    // first repeat, then roughly nine a second.
    private const int MenuRepeatDelayTics = 12;
    private const int MenuRepeatIntervalTics = 4;

    // Doom's seven weapon slots, in the order the number keys select them. Slot 0 is
    // Fist (PlayerBehavior promotes it to Chainsaw when that is owned) and slot 2 is
    // Shotgun (promoted to the super shotgun in Doom II).
    internal const int WeaponSlotCount = 7;

    private readonly IGamepadManager<IGamepadAdapter> manager;
    private readonly Queue<DoomKey> pendingMenuKeys = new Queue<DoomKey>();
    private readonly DoomMenuRepeatClock menuRepeat =
        new DoomMenuRepeatClock(MenuRepeatDelayTics, MenuRepeatIntervalTics);

    // Held state as of the previous tic, for edge detection. The engine's poller could
    // raise ButtonDown events instead, but its registrations are per-GamepadId and a
    // controller that sleeps and wakes comes back with a NEW id — polling the adapter
    // list every tic sidesteps that re-registration entirely.
    private bool prevA;
    private bool prevB;
    private bool prevStart;
    private bool prevBack;
    private bool prevLeftShoulder;
    private bool prevRightShoulder;

    public DoomGamepadInput(IGamepadManager<IGamepadAdapter> gamepadManager)
    {
        manager = gamepadManager;
    }

    /// <summary>This tic's analog and button state; default (all zero) when no pad is present.</summary>
    public DoomGamepadSample Sample { get; private set; }

    /// <summary>
    /// Samples the controller for this tic and queues any menu key edges it produced.
    /// Call once per tic, before the queue is drained and before the tic command is built.
    /// </summary>
    /// <param name="menuNavigationLive">
    /// Whether synthesized cursor keys are safe to raise right now — see
    /// <see cref="IsMenuNavigationLive"/>.
    /// </param>
    /// <param name="player">The console player, for weapon cycling; null outside a game.</param>
    public void BeginTic(bool menuNavigationLive, Player player)
    {
        IGamepadAdapter pad = FirstConnectedPad();
        if (pad == null)
        {
            Sample = default;
            ResetHeldState();
            return;
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
        bool back = pad.PressedButtons.Contains(SdlGamepadButtons.Back);
        bool leftShoulder = pad.PressedButtons.Contains(SdlGamepadButtons.LeftShoulder);
        bool rightShoulder = pad.PressedButtons.Contains(SdlGamepadButtons.RightShoulder);

        var weaponSlot = 0;
        if (rightShoulder && !prevRightShoulder)
        {
            weaponSlot = NextWeaponSlot(player, forward: true) + 1;
        }
        else if (leftShoulder && !prevLeftShoulder)
        {
            weaponSlot = NextWeaponSlot(player, forward: false) + 1;
        }

        Sample = new DoomGamepadSample
        {
            Forward = forward,
            Side = side,
            Turn = look.X,
            Fire = pad.RightTrigger > TriggerThreshold,
            Use = a,
            Run = pad.LeftTrigger > TriggerThreshold,
            WeaponSlot = weaponSlot,
        };

        // Start and Back are the two buttons that mean the same thing whatever is on
        // screen, so they are never gated: Escape opens the menu (and backs out of it),
        // Tab toggles the automap.
        if (start && !prevStart)
        {
            pendingMenuKeys.Enqueue(DoomKey.Escape);
        }

        if (back && !prevBack)
        {
            pendingMenuKeys.Enqueue(DoomKey.Tab);
        }

        if (menuNavigationLive)
        {
            if (a && !prevA)
            {
                pendingMenuKeys.Enqueue(DoomKey.Enter);
            }

            if (b && !prevB)
            {
                pendingMenuKeys.Enqueue(DoomKey.Escape);
            }

            DoomKey navKey = ReadNavigationKey(pad, move);
            if (menuRepeat.Tick(navKey))
            {
                pendingMenuKeys.Enqueue(navKey);
            }
        }
        else
        {
            menuRepeat.Reset();
        }

        prevA = a;
        prevB = b;
        prevStart = start;
        prevBack = back;
        prevLeftShoulder = leftShoulder;
        prevRightShoulder = rightShoulder;
    }

    /// <summary>
    /// Posts this tic's synthesized key edges to the game core. Each is a complete
    /// KeyDown/KeyUp pair: the menus act on the down edge, and the paired up edge keeps
    /// anything that tracks held keys from being left holding one down.
    /// </summary>
    public void PostPendingEventsTo(ManagedDoom.Doom doom)
    {
        while (TryDequeueMenuKey(out DoomKey key))
        {
            doom.PostEvent(new DoomEvent(EventType.KeyDown, key));
            doom.PostEvent(new DoomEvent(EventType.KeyUp, key));
        }
    }

    /// <summary>Takes the next queued key edge, if any.</summary>
    internal bool TryDequeueMenuKey(out DoomKey key)
    {
        if (pendingMenuKeys.Count == 0)
        {
            key = DoomKey.Unknown;
            return false;
        }

        key = pendingMenuKeys.Dequeue();
        return true;
    }

    /// <summary>Drops any queued edges and forgets held state (the core's input reset).</summary>
    public void Reset()
    {
        pendingMenuKeys.Clear();
        ResetHeldState();
    }

    /// <summary>
    /// Whether synthesized cursor keys are safe to raise: only while a menu is up or the
    /// game is not in play. In play they would reach <c>World.DoEvent</c>, where the
    /// automap reads the arrow keys as press-and-hold pan.
    /// </summary>
    internal static bool IsMenuNavigationLive(ManagedDoom.Doom doom)
        => doom.Menu.Active || doom.State != DoomState.Game;

    private IGamepadAdapter FirstConnectedPad()
    {
        if (manager == null)
        {
            return null;
        }

        // Player one is whichever pad enumerates first. Doom is single-player here, so
        // there is nothing to assign a second one to.
        foreach (IGamepadAdapter adapter in manager.ConnectedAdapters)
        {
            return adapter;
        }

        return null;
    }

    private void ResetHeldState()
    {
        prevA = prevB = prevStart = prevBack = prevLeftShoulder = prevRightShoulder = false;
        menuRepeat.Reset();
    }

    // The D-pad and the left stick are one input. Vertical wins a diagonal, because
    // every Doom menu is a vertical list and left/right only adjusts the slider on it.
    private static DoomKey ReadNavigationKey(IGamepadAdapter pad, GamepadStickState move)
    {
        if (pad.PressedButtons.Contains(SdlGamepadButtons.DPadUp) || move.Y >= MenuStickThreshold)
        {
            return DoomKey.Up;
        }

        if (pad.PressedButtons.Contains(SdlGamepadButtons.DPadDown) || move.Y <= -MenuStickThreshold)
        {
            return DoomKey.Down;
        }

        if (pad.PressedButtons.Contains(SdlGamepadButtons.DPadLeft) || move.X <= -MenuStickThreshold)
        {
            return DoomKey.Left;
        }

        if (pad.PressedButtons.Contains(SdlGamepadButtons.DPadRight) || move.X >= MenuStickThreshold)
        {
            return DoomKey.Right;
        }

        return DoomKey.Unknown;
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

    private static int NextWeaponSlot(Player player, bool forward)
        => player == null ? -1 : NextWeaponSlot(SlotOf(player.ReadyWeapon), player.WeaponOwned, forward);

    /// <summary>
    /// Finds the next occupied weapon slot in cycle order, or -1 when there is nothing
    /// else to switch to.
    /// </summary>
    /// <param name="currentSlot">The slot the ready weapon sits in.</param>
    /// <param name="weaponOwned">Ownership indexed by <see cref="WeaponType"/>.</param>
    /// <param name="forward">True to cycle up the slots, false to cycle down.</param>
    internal static int NextWeaponSlot(int currentSlot, bool[] weaponOwned, bool forward)
    {
        int step = forward ? 1 : WeaponSlotCount - 1;

        for (var i = 1; i < WeaponSlotCount; i++)
        {
            int slot = (currentSlot + (step * i)) % WeaponSlotCount;
            if (OwnsSlot(weaponOwned, slot))
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>Maps a weapon to the slot number that selects it (the number-key slots).</summary>
    internal static int SlotOf(WeaponType weapon) => weapon switch
    {
        WeaponType.Chainsaw => (int)WeaponType.Fist,
        WeaponType.SuperShotgun => (int)WeaponType.Shotgun,
        _ => (int)weapon,
    };

    /// <summary>
    /// Tests whether a slot has anything in it. Slots 0 and 2 hold two weapons each, and
    /// the fist is always owned, so slot 0 is always available.
    /// </summary>
    internal static bool OwnsSlot(bool[] weaponOwned, int slot)
    {
        if (slot == (int)WeaponType.Fist)
        {
            return weaponOwned[(int)WeaponType.Fist] || weaponOwned[(int)WeaponType.Chainsaw];
        }

        if (slot == (int)WeaponType.Shotgun)
        {
            return weaponOwned[(int)WeaponType.Shotgun] || weaponOwned[(int)WeaponType.SuperShotgun];
        }

        return weaponOwned[slot];
    }
}
