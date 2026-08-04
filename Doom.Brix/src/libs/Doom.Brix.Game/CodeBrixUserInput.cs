// Doom.Brix — GPLv2 (see the repo LICENSE); the CodeBrix.Platform.GameEngine
// replacement for managed-doom's Silk user-input backend. BuildTicCmd ports
// upstream SilkUserInput.BuildTicCmd (keyboard and mouse paths).
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
using CodeBrix.Platform.GameEngine.Host.Input.Mouse;
using CodeBrix.Platform.GameEngine.Input;
using CodeBrix.Platform.GameEngine.Input.Keyboard;
using CodeBrix.Platform.GameEngine.Input.Mouse;
using CodeBrix.Platform.GameEngine.Sdl2.Gamepad;
using ManagedDoom;
using ManagedDoom.UserInput;

namespace Doom.Brix.Game;

/// <summary>
/// The game core's <see cref="IUserInput"/> backend. Three complementary paths, all on
/// the game-loop thread: key EVENTS (menus, typing, weapon changes) arrive from the
/// <see cref="KeyboardEventPoller"/> during the host's per-tic input poll and queue up
/// for <see cref="PostPendingEventsTo"/>; held-key STATE for movement
/// (<see cref="BuildTicCmd"/>) polls the lock-free keyboard adapter directly; and MOUSE
/// look/buttons come from the host's <see cref="RelativeMouseSession"/> (per-tic
/// deltas while the core has the mouse grabbed) plus the engine's mouse adapter
/// (button state). Doom's core decides when to grab/release via
/// <see cref="GrabMouse"/>/<see cref="ReleaseMouse"/>.
/// </summary>
/// <remarks>
/// A fourth path joins the other three when a controller is connected: see
/// <see cref="DoomGamepadInput"/>, which supplies analog movement to
/// <see cref="BuildTicCmd"/> and synthesized menu key edges to
/// <see cref="PostPendingEventsTo"/>.
/// </remarks>
internal sealed class CodeBrixUserInput : IUserInput, IDisposable
{
    private readonly Config config;
    private readonly IKeyboardAdapter keyboard;
    private readonly RelativeMouseSession mouseSession;
    private readonly IMouseAdapter mouse;
    private readonly DoomGamepadInput gamepad;
    private readonly Queue<QueuedKey> pendingKeys = new Queue<QueuedKey>();
    private readonly bool[] weaponKeys = new bool[7];
    private int turnHeld;

    private readonly struct QueuedKey
    {
        public QueuedKey(DoomKey key, bool down)
        {
            Key = key;
            Down = down;
        }

        public DoomKey Key { get; }
        public bool Down { get; }
    }

    public CodeBrixUserInput(Config config, RelativeMouseSession mouseSession, SdlGamepadManager gamepadManager)
    {
        this.config = config;
        this.mouseSession = mouseSession;
        gamepad = new DoomGamepadInput(gamepadManager);

        var poller = KeyboardEventPoller.Instance;
        if (poller == null)
        {
            throw new InvalidOperationException(
                "The keyboard event poller is not available; construct CodeBrixUserInput from OnLoadContent (after the host wired the input adapters).");
        }

        keyboard = poller.Adapter;
        poller.StartMonitoringAllKeys();
        poller.KeyDown += OnKeyEvent;

        mouse = MouseEventPoller.Instance?.Adapter;
    }

    private void OnKeyEvent(KeyDownEventArgs args)
    {
        // Doom does its own key repeat; only true edges are forwarded.
        if (args.KeyAction == KeyAction.Repeated)
        {
            return;
        }

        var key = DoomKeys.ToDoomKey(args.KeyCode);
        if (key == DoomKey.Unknown)
        {
            return;
        }

        // Raised on the game-loop thread (during InputPump.PollNow), the same thread
        // that drains the queue in PostPendingEventsTo — no synchronization needed.
        pendingKeys.Enqueue(new QueuedKey(key, args.KeyAction == KeyAction.Pressed));
    }

    /// <summary>
    /// Forwards the key edges gathered by this tic's input poll to the game core
    /// (menus, save-name typing, and gameplay key events). Call once per tic, before
    /// <c>doom.Update()</c>.
    /// </summary>
    public void PostPendingEventsTo(ManagedDoom.Doom doom)
    {
        while (pendingKeys.Count > 0)
        {
            var queued = pendingKeys.Dequeue();
            doom.PostEvent(new DoomEvent(
                queued.Down ? EventType.KeyDown : EventType.KeyUp, queued.Key));
        }

        // The controller is sampled here, at the front of the tic, so the sample that
        // BuildTicCmd reads later in the same tic is this tic's — the keyboard adapter
        // it reads alongside was refreshed moments ago by the same input poll.
        gamepad.BeginTic(
            DoomGamepadInput.IsMenuNavigationLive(doom),
            doom.Game?.World?.ConsolePlayer);
        gamepad.PostPendingEventsTo(doom);
    }

    public void BuildTicCmd(TicCmd cmd)
    {
        var keyForward = IsPressed(config.key_forward);
        var keyBackward = IsPressed(config.key_backward);
        var keyStrafeLeft = IsPressed(config.key_strafeleft);
        var keyStrafeRight = IsPressed(config.key_straferight);
        var keyTurnLeft = IsPressed(config.key_turnleft);
        var keyTurnRight = IsPressed(config.key_turnright);
        var keyFire = IsPressed(config.key_fire);
        var keyUse = IsPressed(config.key_use);
        var keyRun = IsPressed(config.key_run);
        var keyStrafe = IsPressed(config.key_strafe);

        // Sampled at the front of this tic by PostPendingEventsTo; all zero when no
        // controller is connected, which makes every fold-in below a no-op.
        var pad = gamepad.Sample;

        for (var i = 0; i < weaponKeys.Length; i++)
        {
            weaponKeys[i] = keyboard.IsDown(0x31 + i); // The top-row 1..7 keys.
        }

        cmd.Clear();

        var strafe = keyStrafe;
        var speed = keyRun || pad.Run ? 1 : 0;
        var forward = 0;
        var side = 0;

        if (config.game_alwaysrun)
        {
            speed = 1 - speed;
        }

        if (keyTurnLeft || keyTurnRight)
        {
            turnHeld++;
        }
        else
        {
            turnHeld = 0;
        }

        int turnSpeed;
        if (turnHeld < PlayerBehavior.SlowTurnTics)
        {
            turnSpeed = 2;
        }
        else
        {
            turnSpeed = speed;
        }

        if (strafe)
        {
            if (keyTurnRight)
            {
                side += PlayerBehavior.SideMove[speed];
            }
            if (keyTurnLeft)
            {
                side -= PlayerBehavior.SideMove[speed];
            }
        }
        else
        {
            if (keyTurnRight)
            {
                cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
            }
            if (keyTurnLeft)
            {
                cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
            }
        }

        if (keyForward)
        {
            forward += PlayerBehavior.ForwardMove[speed];
        }
        if (keyBackward)
        {
            forward -= PlayerBehavior.ForwardMove[speed];
        }

        if (keyStrafeLeft)
        {
            side -= PlayerBehavior.SideMove[speed];
        }
        if (keyStrafeRight)
        {
            side += PlayerBehavior.SideMove[speed];
        }

        // The controller's analog axes, folded in on the same terms as the keys: the
        // left stick moves and strafes, the right stick's X turns. Analog needs no
        // equivalent of the keyboard's slow-turn ramp (turnSpeed above) — a partly
        // pushed stick IS the slow turn — so it scales by speed directly. The right
        // stick's Y is deliberately unused; Doom has no look axis to spend it on.
        if (pad.Forward != 0)
        {
            forward += (int)MathF.Round(pad.Forward * PlayerBehavior.ForwardMove[speed]);
        }
        if (pad.Side != 0)
        {
            side += (int)MathF.Round(pad.Side * PlayerBehavior.SideMove[speed]);
        }
        if (pad.Turn != 0)
        {
            cmd.AngleTurn -= (short)(int)MathF.Round(pad.Turn * PlayerBehavior.AngleTurn[speed]);
        }

        if (keyFire || pad.Fire)
        {
            cmd.Buttons |= TicCmdButtons.Attack;
        }

        if (keyUse || pad.Use)
        {
            cmd.Buttons |= TicCmdButtons.Use;
        }

        var weaponSlot = -1;
        for (var i = 0; i < weaponKeys.Length; i++)
        {
            if (weaponKeys[i])
            {
                weaponSlot = i;
                break;
            }
        }

        // A number key wins over the shoulder buttons: it names one weapon, where the
        // shoulders only say "the next one". The sample's slot is one-based so that a
        // default (no controller) sample means "change nothing".
        if (weaponSlot < 0 && pad.WeaponSlot > 0)
        {
            weaponSlot = pad.WeaponSlot - 1;
        }

        if (weaponSlot >= 0)
        {
            cmd.Buttons |= TicCmdButtons.Change;
            cmd.Buttons |= (byte)(weaponSlot << TicCmdButtons.WeaponShift);
        }

        // Mouse look, as upstream: sensitivity-scaled per-tic deltas turn (or
        // strafe) horizontally and move vertically; y is dropped when the
        // config disables it.
        if (mouseSession != null && mouseSession.IsActive)
        {
            var (deltaX, deltaY) = mouseSession.ConsumeDelta();
            if (config.mouse_disableyaxis)
            {
                deltaY = 0;
            }

            var ms = 0.5F * config.mouse_sensitivity;
            var mx = (int)MathF.Round(ms * deltaX);
            var my = (int)MathF.Round(ms * -deltaY);
            forward += my;
            if (strafe)
            {
                side += mx * 2;
            }
            else
            {
                cmd.AngleTurn -= (short)(mx * 0x8);
            }
        }

        if (forward > PlayerBehavior.MaxMove)
        {
            forward = PlayerBehavior.MaxMove;
        }
        else if (forward < -PlayerBehavior.MaxMove)
        {
            forward = -PlayerBehavior.MaxMove;
        }
        if (side > PlayerBehavior.MaxMove)
        {
            side = PlayerBehavior.MaxMove;
        }
        else if (side < -PlayerBehavior.MaxMove)
        {
            side = -PlayerBehavior.MaxMove;
        }

        cmd.ForwardMove += (sbyte)forward;
        cmd.SideMove += (sbyte)side;
    }

    private bool IsPressed(KeyBinding binding)
    {
        foreach (var key in binding.Keys)
        {
            foreach (var code in DoomKeys.ToVirtualKeyCodes(key))
            {
                if (keyboard.IsDown(code))
                {
                    return true;
                }
            }
        }

        if (mouse != null && mouseSession != null && mouseSession.IsActive)
        {
            foreach (var mouseButton in binding.MouseButtons)
            {
                var mapped = ToMouseButton(mouseButton);
                if (mapped != MouseButton.None && mouse.PressedButtons.Contains(mapped))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static MouseButton ToMouseButton(DoomMouseButton button) => button switch
    {
        DoomMouseButton.Mouse1 => MouseButton.Left,
        DoomMouseButton.Mouse2 => MouseButton.Right,
        DoomMouseButton.Mouse3 => MouseButton.Middle,
        _ => MouseButton.None,
    };

    public void Reset()
    {
        pendingKeys.Clear();
        mouseSession?.ConsumeDelta();
        gamepad.Reset();
    }

    // Doom's core drives these from its own grab policy (in-game with focus =>
    // grab; menus/paused/unfocused => release). On a platform head without
    // relative-mouse support the session stays inactive (it logs once) and the
    // game simply runs keyboard-only.
    public void GrabMouse() => mouseSession?.Begin();

    public void ReleaseMouse() => mouseSession?.End();

    public int MaxMouseSensitivity => 15;

    public int MouseSensitivity
    {
        get => config.mouse_sensitivity;
        set => config.mouse_sensitivity = value;
    }

    public void Dispose()
    {
        if (KeyboardEventPoller.Instance is KeyboardEventPoller poller)
        {
            poller.KeyDown -= OnKeyEvent;
        }
    }
}
