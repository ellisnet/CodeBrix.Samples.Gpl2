// Wolfenstein.Brix — GPLv2 (see the repo LICENSE); the keyboard tracker
// follows Doom.Brix's CodeBrixUserInput pattern: key EVENTS (menus,
// name typing) queue up from the poller during the per-tic input poll,
// while held-key STATE for movement polls the lock-free adapter.
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
using CodeBrix.Platform.GameEngine.Input;
using CodeBrix.Platform.GameEngine.Input.Keyboard;
using CodeBrix.Platform.GameEngine.Sdl2.Gamepad;
using Wolfenstein.Brix.GameEngine.Logic;

namespace Wolfenstein.Brix.Game;

/// <summary>
/// Builds the per-tic <see cref="SessionInput"/>: held-key state for
/// movement (arrows/WASD, Shift run, Space use, Ctrl fire, 1-4
/// weapons) plus edge events for the menus (arrows, Enter, Escape,
/// typed characters, Backspace).
/// </summary>
/// <remarks>
/// A connected controller feeds the same input alongside the keyboard; see
/// <see cref="WolfGamepadInput"/>.
/// </remarks>
internal sealed class WolfUserInput : IDisposable
{
    private const int VkBack = 0x08;
    private const int VkEnter = 0x0D;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkEscape = 0x1B;
    private const int VkSpace = 0x20;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;
    private const int VkZero = 0x30;
    private const int VkOne = 0x31;
    private const int VkA = 0x41;
    private const int VkD = 0x44;
    private const int VkS = 0x53;
    private const int VkW = 0x57;
    private const int VkZ = 0x5A;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkOemPeriod = 0xBE;
    private const int VkOemMinus = 0xBD;

    // The original's BASEMOVE/RUNMOVE times MOVESCALE.
    private const int WalkMove = 35 * 150;
    private const int RunMove = 70 * 150;

    // Keyboard turning: three degrees per tic walking, five running.
    private const int TurnFine = 3 * WolfMath.Ang1;
    private const int RunTurnFine = 5 * WolfMath.Ang1;

    private readonly IKeyboardAdapter keyboard;
    private readonly GameSession session;
    private readonly WolfGamepadInput gamepad;

    // Edges gathered by the poller during this tic's input poll; both
    // sides run on the game-loop thread, so no synchronization needed.
    private bool edgeUp;
    private bool edgeDown;
    private bool edgeActivate;
    private bool edgeBack;
    private bool edgeBackspace;
    private char typedChar;

    public WolfUserInput(GameSession gameSession, SdlGamepadManager gamepadManager)
    {
        session = gameSession;
        gamepad = new WolfGamepadInput(gamepadManager);

        var poller = KeyboardEventPoller.Instance;
        if (poller == null)
        {
            throw new InvalidOperationException(
                "The keyboard event poller is not available; construct WolfUserInput from OnLoadContent (after the host wired the input adapters).");
        }

        keyboard = poller.Adapter;
        poller.StartMonitoringAllKeys();
        poller.KeyDown += OnKeyEvent;
    }

    private void OnKeyEvent(KeyDownEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed)
        {
            return;
        }

        switch (args.KeyCode)
        {
            case VkUp:
                edgeUp = true;
                break;
            case VkDown:
                edgeDown = true;
                break;
            case VkEnter:
            case VkSpace:
                edgeActivate = true;
                break;
            case VkEscape:
                edgeBack = true;
                break;
            case VkBack:
                edgeBackspace = true;
                break;
            case >= VkA and <= VkZ:
                typedChar = (char)('A' + (args.KeyCode - VkA));
                break;
            case >= VkZero and <= VkZero + 9:
                typedChar = (char)('0' + (args.KeyCode - VkZero));
                break;
            case VkOemPeriod:
                typedChar = '.';
                break;
            case VkOemMinus:
                typedChar = '-';
                break;
        }
    }

    /// <summary>Samples held keys and the controller, and drains this tic's edges into one input.</summary>
    public SessionInput BuildInput()
    {
        // All-default when no controller is connected, which makes every fold-in below
        // a no-op.
        var pad = gamepad.Sample(session?.Logic?.Player);

        var run = keyboard.IsDown(VkShift) || keyboard.IsDown(VkLShift) || keyboard.IsDown(VkRShift) || pad.Run;
        var move = run ? RunMove : WalkMove;
        var turn = run ? RunTurnFine : TurnFine;

        var game = default(PlayerTicCommand);

        if (keyboard.IsDown(VkUp) || keyboard.IsDown(VkW))
        {
            game.ForwardMove += move;
        }

        if (keyboard.IsDown(VkDown) || keyboard.IsDown(VkS))
        {
            game.ForwardMove -= move;
        }

        if (keyboard.IsDown(VkD))
        {
            game.SideMove += move;
        }

        if (keyboard.IsDown(VkA))
        {
            game.SideMove -= move;
        }

        if (keyboard.IsDown(VkLeft))
        {
            game.AngleTurn += turn;
        }

        if (keyboard.IsDown(VkRight))
        {
            game.AngleTurn -= turn;
        }

        // The controller's analog axes, folded in on the same terms as the keys: the
        // left stick moves and strafes, the right stick's X turns. Positive AngleTurn is
        // counter-clockwise, so pushing the stick right subtracts. The right stick's Y
        // is deliberately unused; Wolfenstein has no look axis to spend it on.
        game.ForwardMove += (int)MathF.Round(pad.Forward * move);
        game.SideMove += (int)MathF.Round(pad.Side * move);
        game.AngleTurn -= (int)MathF.Round(pad.Turn * turn);

        game.Attack = keyboard.IsDown(VkControl) ||
            keyboard.IsDown(VkLControl) || keyboard.IsDown(VkRControl) || pad.Fire;
        game.Use = keyboard.IsDown(VkSpace) || pad.Use;

        for (var slot = 0; slot < 4; slot++)
        {
            if (keyboard.IsDown(VkOne + slot))
            {
                game.WeaponSlot = slot + 1;
                break;
            }
        }

        // A number key wins over the shoulder buttons: it names one weapon, where the
        // shoulders only say "the next one".
        if (game.WeaponSlot == 0)
        {
            game.WeaponSlot = pad.WeaponSlot;
        }

        var input = new SessionInput
        {
            Game = game,
            MenuUp = edgeUp || pad.MenuUp,
            MenuDown = edgeDown || pad.MenuDown,
            MenuActivate = edgeActivate || pad.MenuActivate,
            MenuBack = edgeBack || pad.MenuBack,
            Backspace = edgeBackspace,
            TypedChar = typedChar,
        };

        edgeUp = edgeDown = edgeActivate = edgeBack = edgeBackspace = false;
        typedChar = '\0';
        return input;
    }

    public void Dispose()
    {
        if (KeyboardEventPoller.Instance is KeyboardEventPoller poller)
        {
            poller.KeyDown -= OnKeyEvent;
        }
    }
}
