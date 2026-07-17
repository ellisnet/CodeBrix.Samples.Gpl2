using System.Linq;
using ManagedDoom;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.GameEngine.Tests;

// The three demo-playback compatibility tests from upstream's IwadDemo
// suite that run on the shareware DOOM1.WAD. Each one plays a demo lump
// embedded in the IWAD through the ENTIRE headless engine — game logic,
// physics, monsters, sectors — and hash-checks the resulting world state
// every tic, proving the port's game logic end-to-end with no UI or
// audio hardware. The expected hashes are upstream's, so a pass also
// proves vanilla Doom v1.9 demo compatibility.
public class DemoCompatibilityTests
{
    [Fact]
    public void Demo1_playback_matches_vanilla_world_state() =>
        RunEmbeddedDemo("DEMO1", 0xa497cb7fu, 0x5a1776fdu, 0x55d373a2u, 0xcaafd23bu);

    [Fact]
    public void Demo2_playback_matches_vanilla_world_state() =>
        RunEmbeddedDemo("DEMO2", 0xf7f5daddu, 0xb576525au, 0xf2e936b0u, 0xe62009fau);

    [Fact]
    public void Demo3_playback_matches_vanilla_world_state() =>
        RunEmbeddedDemo("DEMO3", 0x893f32d2u, 0x22b21b86u, 0xfef34aafu, 0xa881ce6fu);

    private static void RunEmbeddedDemo(
        string lumpName,
        uint expectedLastMobjHash,
        uint expectedAggMobjHash,
        uint expectedLastSectorHash,
        uint expectedAggSectorHash)
    {
        using var content = GameContent.CreateDummy(TestWad.Doom1SharewarePath);
        var demo = new Demo(content.Wad.ReadLump(lumpName));
        var cmds = Enumerable.Range(0, Player.MaxPlayerCount).Select(i => new TicCmd()).ToArray();
        var game = new DoomGame(content, demo.Options);
        game.DeferedInitNew();

        var lastMobjHash = 0;
        var aggMobjHash = 0;
        var lastSectorHash = 0;
        var aggSectorHash = 0;

        while (demo.ReadCmd(cmds))
        {
            game.Update(cmds);
            lastMobjHash = DoomDebug.GetMobjHash(game.World);
            aggMobjHash = DoomDebug.CombineHash(aggMobjHash, lastMobjHash);
            lastSectorHash = DoomDebug.GetSectorHash(game.World);
            aggSectorHash = DoomDebug.CombineHash(aggSectorHash, lastSectorHash);
        }

        ((uint)lastMobjHash).Should().Be(expectedLastMobjHash);
        ((uint)aggMobjHash).Should().Be(expectedAggMobjHash);
        ((uint)lastSectorHash).Should().Be(expectedLastSectorHash);
        ((uint)aggSectorHash).Should().Be(expectedAggSectorHash);
    }
}
