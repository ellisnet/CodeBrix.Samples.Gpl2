using ManagedDoom;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.GameEngine.Tests;

// Round-trip tests for Config's text serialization seam (SaveToText / FromText),
// which the settings.sqlite-backed storage uses in place of a .cfg file.
public class ConfigStorageTests
{
    [Fact]
    public void SaveToText_then_FromText_restores_all_values()
    {
        //Arrange
        var original = new Config
        {
            mouse_sensitivity = 5,
            mouse_disableyaxis = true,
            game_alwaysrun = false,
            video_screenwidth = 1280,
            video_screenheight = 800,
            video_fullscreen = true,
            video_highresolution = false,
            video_displaymessage = false,
            video_gamescreensize = 9,
            video_gammacorrection = 4,
            video_fpsscale = 3,
            audio_soundvolume = 3,
            audio_musicvolume = 11,
            audio_randompitch = false,
            audio_soundfont = "custom.sf2",
            audio_musiceffect = false,
        };

        //Act
        var restored = Config.FromText(original.SaveToText());

        //Assert
        restored.IsRestoredFromFile.Should().Be(true);
        restored.mouse_sensitivity.Should().Be(5);
        restored.mouse_disableyaxis.Should().Be(true);
        restored.game_alwaysrun.Should().Be(false);
        restored.video_screenwidth.Should().Be(1280);
        restored.video_screenheight.Should().Be(800);
        restored.video_fullscreen.Should().Be(true);
        restored.video_highresolution.Should().Be(false);
        restored.video_displaymessage.Should().Be(false);
        restored.video_gamescreensize.Should().Be(9);
        restored.video_gammacorrection.Should().Be(4);
        restored.video_fpsscale.Should().Be(3);
        restored.audio_soundvolume.Should().Be(3);
        restored.audio_musicvolume.Should().Be(11);
        restored.audio_randompitch.Should().Be(false);
        restored.audio_soundfont.Should().Be("custom.sf2");
        restored.audio_musiceffect.Should().Be(false);
        // Key bindings serialize/parse through the same text path.
        restored.key_fire.ToString().Should().Be(original.key_fire.ToString());
    }

    [Fact]
    public void FromText_null_keeps_defaults()
    {
        //Act
        var config = Config.FromText(null);

        //Assert
        config.IsRestoredFromFile.Should().Be(false);
        config.mouse_sensitivity.Should().Be(8);
    }

    [Fact]
    public void FromText_empty_keeps_defaults()
    {
        //Act
        var config = Config.FromText("");

        //Assert
        config.IsRestoredFromFile.Should().Be(false);
        config.mouse_sensitivity.Should().Be(8);
    }
}
