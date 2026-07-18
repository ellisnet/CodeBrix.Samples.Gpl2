using System;
using System.Text;

namespace Doom.Brix.Synth.Tests;

// Decodes a fixed-width 20-byte SF2 name field (as exposed by CodeBrix.Audio's
// record structs) into a trimmed string for comparison.
internal static class Sf2Name
{
    public static string Of(byte[] nameBytes)
    {
        var terminator = Array.IndexOf(nameBytes, (byte)0);
        return Encoding.ASCII.GetString(nameBytes, 0, terminator >= 0 ? terminator : nameBytes.Length);
    }
}
