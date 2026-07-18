using System.Collections.Generic;
using System.Linq;
using SilverAssertions;
using Wolfenstein.Brix.GameEngine.Assets;
using Wolfenstein.Brix.GameEngine.Logic;
using Wolfenstein.Brix.GameEngine.Rendering;
using Xunit;

namespace Wolfenstein.Brix.GameEngine.Tests;

// Full-frame render checks: compose the E1L1 starting screen headless
// and assert the buffer holds a real textured scene with the HUD.
public class SceneRendererTests
{
    private static (WolfLogic Logic, FrameComposer Composer, RenderWorld World) StartGame()
    {
        var assets = WolfAssets.Load(TestWl1.AssetsFolderPath);
        var logic = new WolfLogic(assets);
        logic.StartNewGame(DifficultyLevel.BringEmOn);
        return (logic, new FrameComposer(assets), new RenderWorld(logic));
    }

    [Fact]
    public void E1L1_start_screen_composes_a_textured_scene_with_hud()
    {
        //Arrange
        var (logic, composer, world) = StartGame();
        logic.Tic(default(PlayerTicCommand));
        var frame = new uint[FrameComposer.ScreenWidth * FrameComposer.ScreenHeight];

        //Act
        composer.ComposeGameplay(frame, world);

        //Assert - many distinct colors (textures + HUD), all opaque
        var distinctColors = new HashSet<uint>(frame);
        (distinctColors.Count > 24).Should().BeTrue();
        frame.All(pixel => (pixel >> 24) == 0xFF).Should().BeTrue();
    }

    [Fact]
    public void Rendering_is_deterministic()
    {
        //Arrange
        var (logic, composer, world) = StartGame();
        logic.Tic(default(PlayerTicCommand));
        var first = new uint[FrameComposer.ScreenWidth * FrameComposer.ScreenHeight];
        var second = new uint[FrameComposer.ScreenWidth * FrameComposer.ScreenHeight];

        //Act
        composer.ComposeGameplay(first, world);
        composer.ComposeGameplay(second, world);

        //Assert
        first.SequenceEqual(second).Should().BeTrue();
    }

    [Fact]
    public void Turning_changes_the_rendered_scene()
    {
        //Arrange
        var (logic, composer, world) = StartGame();
        var before = new uint[FrameComposer.ScreenWidth * FrameComposer.ScreenHeight];
        var after = new uint[FrameComposer.ScreenWidth * FrameComposer.ScreenHeight];
        composer.ComposeGameplay(before, world);

        //Act - turn for half a second
        for (var i = 0; i < 35; i++)
        {
            logic.Tic(new PlayerTicCommand { AngleTurn = 3 * WolfMath.Ang1 });
        }

        composer.ComposeGameplay(after, world);

        //Assert
        before.SequenceEqual(after).Should().BeFalse();
    }
}
