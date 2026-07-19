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
internal sealed class CodeBrixUserInput : IUserInput, IDisposable
{
    private readonly Config config;
    private readonly IKeyboardAdapter keyboard;
    private readonly RelativeMouseSession mouseSession;
    private readonly IMouseAdapter mouse;
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

    public CodeBrixUserInput(Config config, RelativeMouseSession mouseSession)
    {
        this.config = config;
        this.mouseSession = mouseSession;

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

        for (var i = 0; i < weaponKeys.Length; i++)
        {
            weaponKeys[i] = keyboard.IsDown(0x31 + i); // The top-row 1..7 keys.
        }

        cmd.Clear();

        var strafe = keyStrafe;
        var speed = keyRun ? 1 : 0;
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

        if (keyFire)
        {
            cmd.Buttons |= TicCmdButtons.Attack;
        }

        if (keyUse)
        {
            cmd.Buttons |= TicCmdButtons.Use;
        }

        for (var i = 0; i < weaponKeys.Length; i++)
        {
            if (weaponKeys[i])
            {
                cmd.Buttons |= TicCmdButtons.Change;
                cmd.Buttons |= (byte)(i << TicCmdButtons.WeaponShift);
                break;
            }
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
