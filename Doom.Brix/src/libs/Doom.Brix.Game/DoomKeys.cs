// Doom.Brix — GPLv2 (see the repo LICENSE); derived from managed-doom's
// Silk key mapping.
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
using ManagedDoom;

namespace Doom.Brix.Game;

/// <summary>
/// Translates between the platform's Windows virtual-key codes (the key codes the
/// GameEngine's keyboard events and <c>IsDown</c> polling speak) and managed-doom's
/// <see cref="DoomKey"/> values, in both directions.
/// </summary>
internal static class DoomKeys
{
    // Windows virtual-key codes that have no named Windows.System.VirtualKey member.
    private const int VkOemSemicolon = 0xBA;  // ;:
    private const int VkOemPlus = 0xBB;       // =+
    private const int VkOemComma = 0xBC;      // ,<
    private const int VkOemMinus = 0xBD;      // -_
    private const int VkOemPeriod = 0xBE;     // .>
    private const int VkOemQuestion = 0xBF;   // /?
    private const int VkOemTilde = 0xC0;      // `~
    private const int VkOemOpenBracket = 0xDB;   // [{
    private const int VkOemBackslash = 0xDC;     // \|
    private const int VkOemCloseBracket = 0xDD;  // ]}
    private const int VkOemQuote = 0xDE;         // '"

    private static readonly int[][] virtualKeysByDoomKey = BuildVirtualKeyTable();

    /// <summary>
    /// Maps a platform key code to the <see cref="DoomKey"/> the game core understands,
    /// or <see cref="DoomKey.Unknown"/> for keys Doom has no use for.
    /// </summary>
    public static DoomKey ToDoomKey(int virtualKeyCode)
    {
        // Letters and the two digit rows are contiguous ranges.
        if (virtualKeyCode >= 0x41 && virtualKeyCode <= 0x5A)
        {
            return DoomKey.A + (virtualKeyCode - 0x41);
        }
        if (virtualKeyCode >= 0x30 && virtualKeyCode <= 0x39)
        {
            return DoomKey.Num0 + (virtualKeyCode - 0x30);
        }
        if (virtualKeyCode >= 0x60 && virtualKeyCode <= 0x69)
        {
            return DoomKey.Numpad0 + (virtualKeyCode - 0x60);
        }
        if (virtualKeyCode >= 0x70 && virtualKeyCode <= 0x7E)
        {
            return DoomKey.F1 + (virtualKeyCode - 0x70);
        }

        switch (virtualKeyCode)
        {
            case 0x1B: return DoomKey.Escape;
            case 0x0D: return DoomKey.Enter;
            case 0x20: return DoomKey.Space;
            case 0x09: return DoomKey.Tab;
            case 0x08: return DoomKey.Backspace;

            case 0x25: return DoomKey.Left;
            case 0x26: return DoomKey.Up;
            case 0x27: return DoomKey.Right;
            case 0x28: return DoomKey.Down;

            case 0x21: return DoomKey.PageUp;
            case 0x22: return DoomKey.PageDown;
            case 0x23: return DoomKey.End;
            case 0x24: return DoomKey.Home;
            case 0x2D: return DoomKey.Insert;
            case 0x2E: return DoomKey.Delete;
            case 0x13: return DoomKey.Pause;

            // Generic and sided modifier codes; different heads report either form.
            case 0x10: case 0xA0: return DoomKey.LShift;
            case 0xA1: return DoomKey.RShift;
            case 0x11: case 0xA2: return DoomKey.LControl;
            case 0xA3: return DoomKey.RControl;
            case 0x12: case 0xA4: return DoomKey.LAlt;
            case 0xA5: return DoomKey.RAlt;
            case 0x5B: return DoomKey.LSystem;
            case 0x5C: return DoomKey.RSystem;
            case 0x5D: return DoomKey.Menu;

            case 0x6A: return DoomKey.Multiply;
            case 0x6B: return DoomKey.Add;
            case 0x6D: return DoomKey.Subtract;
            case 0x6F: return DoomKey.Divide;

            // The top-row minus maps to Subtract, matching upstream's Silk mapping
            // (Doom's screen-size and gamma adjustments listen for Subtract/Equal).
            case VkOemMinus: return DoomKey.Subtract;
            case VkOemPlus: return DoomKey.Equal;
            case VkOemSemicolon: return DoomKey.Semicolon;
            case VkOemComma: return DoomKey.Comma;
            case VkOemPeriod: return DoomKey.Period;
            case VkOemQuestion: return DoomKey.Slash;
            case VkOemTilde: return DoomKey.Tilde;
            case VkOemOpenBracket: return DoomKey.LBracket;
            case VkOemBackslash: return DoomKey.Backslash;
            case VkOemCloseBracket: return DoomKey.RBracket;
            case VkOemQuote: return DoomKey.Quote;

            default: return DoomKey.Unknown;
        }
    }

    /// <summary>
    /// The platform key codes that count as the given <see cref="DoomKey"/> being held,
    /// for the per-tic <c>IsDown</c> polling path. Several Doom keys listen on more than
    /// one code (sided + generic modifiers, numpad + top-row minus).
    /// </summary>
    public static int[] ToVirtualKeyCodes(DoomKey key)
    {
        var index = (int)key;
        return index >= 0 && index < virtualKeysByDoomKey.Length
            ? virtualKeysByDoomKey[index]
            : Array.Empty<int>();
    }

    private static int[][] BuildVirtualKeyTable()
    {
        var table = new int[(int)DoomKey.Count][];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = Array.Empty<int>();
        }

        for (var letter = 0; letter < 26; letter++)
        {
            table[(int)DoomKey.A + letter] = new[] { 0x41 + letter };
        }
        for (var digit = 0; digit < 10; digit++)
        {
            table[(int)DoomKey.Num0 + digit] = new[] { 0x30 + digit };
            table[(int)DoomKey.Numpad0 + digit] = new[] { 0x60 + digit };
        }
        for (var f = 0; f < 15; f++)
        {
            table[(int)DoomKey.F1 + f] = new[] { 0x70 + f };
        }

        table[(int)DoomKey.Escape] = new[] { 0x1B };
        table[(int)DoomKey.Enter] = new[] { 0x0D };
        table[(int)DoomKey.Space] = new[] { 0x20 };
        table[(int)DoomKey.Tab] = new[] { 0x09 };
        table[(int)DoomKey.Backspace] = new[] { 0x08 };

        table[(int)DoomKey.Left] = new[] { 0x25 };
        table[(int)DoomKey.Up] = new[] { 0x26 };
        table[(int)DoomKey.Right] = new[] { 0x27 };
        table[(int)DoomKey.Down] = new[] { 0x28 };

        table[(int)DoomKey.PageUp] = new[] { 0x21 };
        table[(int)DoomKey.PageDown] = new[] { 0x22 };
        table[(int)DoomKey.End] = new[] { 0x23 };
        table[(int)DoomKey.Home] = new[] { 0x24 };
        table[(int)DoomKey.Insert] = new[] { 0x2D };
        table[(int)DoomKey.Delete] = new[] { 0x2E };
        table[(int)DoomKey.Pause] = new[] { 0x13 };

        table[(int)DoomKey.LShift] = new[] { 0xA0, 0x10 };
        table[(int)DoomKey.RShift] = new[] { 0xA1, 0x10 };
        table[(int)DoomKey.LControl] = new[] { 0xA2, 0x11 };
        table[(int)DoomKey.RControl] = new[] { 0xA3, 0x11 };
        table[(int)DoomKey.LAlt] = new[] { 0xA4, 0x12 };
        table[(int)DoomKey.RAlt] = new[] { 0xA5, 0x12 };
        table[(int)DoomKey.LSystem] = new[] { 0x5B };
        table[(int)DoomKey.RSystem] = new[] { 0x5C };
        table[(int)DoomKey.Menu] = new[] { 0x5D };

        table[(int)DoomKey.Multiply] = new[] { 0x6A };
        table[(int)DoomKey.Add] = new[] { 0x6B };
        table[(int)DoomKey.Subtract] = new[] { 0x6D, VkOemMinus };
        table[(int)DoomKey.Divide] = new[] { 0x6F };
        table[(int)DoomKey.Equal] = new[] { VkOemPlus };

        table[(int)DoomKey.Semicolon] = new[] { VkOemSemicolon };
        table[(int)DoomKey.Comma] = new[] { VkOemComma };
        table[(int)DoomKey.Period] = new[] { VkOemPeriod };
        table[(int)DoomKey.Slash] = new[] { VkOemQuestion };
        table[(int)DoomKey.Tilde] = new[] { VkOemTilde };
        table[(int)DoomKey.LBracket] = new[] { VkOemOpenBracket };
        table[(int)DoomKey.Backslash] = new[] { VkOemBackslash };
        table[(int)DoomKey.RBracket] = new[] { VkOemCloseBracket };
        table[(int)DoomKey.Quote] = new[] { VkOemQuote };

        return table;
    }
}
