using System.Linq;
using ManagedDoom;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.GameEngine.Tests;

// Proves the SaveGame byte-exposure seam: serialize a live world to raw vanilla
// save bytes, load them into a fresh game, and confirm the restored world-state
// hashes match — i.e. save -> bytes -> load restores identical state. Runs the
// whole headless engine on the shareware DOOM1.WAD (like DemoCompatibilityTests).
public class SaveGameRoundTripTests
{
    [Fact]
    public void Save_to_bytes_then_Load_restores_identical_world_state()
    {
        //Arrange: play DEMO1 for a while to evolve a non-trivial world.
        using var content = GameContent.CreateDummy(TestWad.Doom1SharewarePath);
        var demo = new Demo(content.Wad.ReadLump("DEMO1"));
        var cmds = Enumerable.Range(0, Player.MaxPlayerCount).Select(_ => new TicCmd()).ToArray();
        var game = new DoomGame(content, demo.Options);
        game.DeferedInitNew();

        var tics = 0;
        while (tics < 175 && demo.ReadCmd(cmds))
        {
            game.Update(cmds);
            tics++;
        }

        var expectedMobjHash = DoomDebug.GetMobjHash(game.World);
        var expectedSectorHash = DoomDebug.GetSectorHash(game.World);

        //Act: serialize to bytes, then load into a fresh game.
        var bytes = SaveAndLoad.Save(game, "ROUND TRIP");

        using var loadContent = GameContent.CreateDummy(TestWad.Doom1SharewarePath);
        var loadDemo = new Demo(loadContent.Wad.ReadLump("DEMO1"));
        var loaded = new DoomGame(loadContent, loadDemo.Options);
        loaded.DeferedInitNew();
        SaveAndLoad.Load(loaded, bytes);

        //Assert
        (bytes != null).Should().Be(true);
        (bytes.Length > 0).Should().Be(true);
        DoomDebug.GetMobjHash(loaded.World).Should().Be(expectedMobjHash);
        DoomDebug.GetSectorHash(loaded.World).Should().Be(expectedSectorHash);
    }
}
