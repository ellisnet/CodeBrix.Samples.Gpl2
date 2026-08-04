using SilverAssertions;
using Wolfenstein.Brix.GameEngine.Assets;
using Xunit;

namespace Wolfenstein.Brix.GameEngine.Tests;

// Parsing checks against the real shareware v1.4 .WL1 data set. The
// expected values were read directly from the canonical files (the
// md5-verified set the application's Assets Mode installs).
public class WolfAssetsTests
{
    private static WolfAssets Load() => WolfAssets.Load(TestWl1.AssetsFolderPath);

    [Fact]
    public void All_eight_data_files_parse()
    {
        var assets = Load();
        assets.Vswap.Should().NotBeNull();
        assets.Maps.Should().NotBeNull();
        assets.Graphics.Should().NotBeNull();
        assets.Audio.Should().NotBeNull();
    }

    [Fact]
    public void Vswap_has_the_shareware_page_layout()
    {
        var assets = Load();
        assets.Vswap.ChunkCount.Should().Be(663);
        assets.Vswap.SpriteStart.Should().Be(106);
        assets.Vswap.SoundStart.Should().Be(542);
        assets.Vswap.Walls.Length.Should().Be(106);
        assets.Vswap.Sprites.Length.Should().Be(436);
    }

    [Fact]
    public void Vswap_walls_decode_to_64x64_textures()
    {
        var assets = Load();
        assets.Vswap.Walls[0].IsEmpty.Should().BeFalse();
        assets.Vswap.Walls[0].Width.Should().Be(64);
        assets.Vswap.Walls[0].Height.Should().Be(64);

        // The shareware file carries walls 0-55 and 98-105; the
        // registered-only slots in between are sparse.
        assets.Vswap.Walls[55].IsEmpty.Should().BeFalse();
        assets.Vswap.Walls[56].IsEmpty.Should().BeTrue();
        assets.Vswap.Walls[97].IsEmpty.Should().BeTrue();
        assets.Vswap.Walls[98].IsEmpty.Should().BeFalse();
        assets.Vswap.Walls[105].IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Vswap_sprites_follow_the_shareware_sparse_ranges()
    {
        var assets = Load();

        // Present: 0-186 (scenery, guard, officer, SS, dog, mutant),
        // 296-306 (Hans) and 408-435 (weapons + BJ). Everything else
        // is registered-only and sparse.
        assets.Vswap.Sprites[0].IsEmpty.Should().BeFalse();
        assets.Vswap.Sprites[186].IsEmpty.Should().BeFalse();
        assets.Vswap.Sprites[187].IsEmpty.Should().BeTrue();
        assets.Vswap.Sprites[295].IsEmpty.Should().BeTrue();
        assets.Vswap.Sprites[296].IsEmpty.Should().BeFalse();
        assets.Vswap.Sprites[306].IsEmpty.Should().BeFalse();
        assets.Vswap.Sprites[307].IsEmpty.Should().BeTrue();
        assets.Vswap.Sprites[408].IsEmpty.Should().BeFalse();
        assets.Vswap.Sprites[435].IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Vswap_digitized_sounds_match_the_table_page()
    {
        var assets = Load();

        // The table page holds 46 four-byte (page, length) entries -
        // the full registered digitized-sound list; shareware-absent
        // slots decode to empty arrays.
        assets.Vswap.DigitizedSounds.Length.Should().Be(46);
        assets.Vswap.DigitizedSounds[0].Length.Should().Be(5996);
    }

    [Fact]
    public void Maps_decode_all_ten_shareware_levels()
    {
        var assets = Load();
        assets.Maps.RlewTag.Should().Be((ushort)0xABCD);
        assets.Maps.Maps.Length.Should().Be(10);
        foreach (var map in assets.Maps.Maps)
        {
            map.Width.Should().Be(64);
            map.Height.Should().Be(64);
            map.Plane0.Length.Should().Be(64 * 64);
            map.Plane1.Length.Should().Be(64 * 64);
        }

        assets.Maps.Maps[0].Name.Should().Be("Wolf1 Map1");
        assets.Maps.Maps[7].Name.Should().Be("Wolf1 Map8");
        assets.Maps.Maps[8].Name.Should().Be("Wolf1 Boss");
        assets.Maps.Maps[9].Name.Should().Be("Wolf1 Secret");
    }

    [Fact]
    public void Vgagraph_has_the_shareware_chunk_layout()
    {
        var assets = Load();
        assets.Graphics.ChunkCount.Should().Be(156);
        assets.Graphics.PicTable.Length.Should().Be(144);
        assets.Graphics.Fonts.Length.Should().Be(2);
        (assets.Graphics.Fonts[0].Length > 0).Should().BeTrue();
        (assets.Graphics.Fonts[1].Length > 0).Should().BeTrue();
    }

    [Fact]
    public void Vgagraph_pics_decode_with_plausible_dimensions()
    {
        var assets = Load();
        foreach (var pic in assets.Graphics.Pics)
        {
            if (!pic.IsEmpty)
            {
                (pic.Width > 0 && pic.Width <= 320).Should().BeTrue();
                (pic.Height > 0 && pic.Height <= 200).Should().BeTrue();
            }
        }
    }

    [Fact]
    public void Title_screen_and_status_bar_pics_are_full_width()
    {
        var assets = Load();

        // Chunk numbering (WL1): STATUSBARPIC = 98, TITLEPIC = 99,
        // pics start at chunk 3.
        var statusBar = assets.Graphics.Pics[98 - VgaGraphFile.StartPicsChunk];
        var title = assets.Graphics.Pics[99 - VgaGraphFile.StartPicsChunk];
        statusBar.Width.Should().Be(320);
        statusBar.Height.Should().Be(40);
        title.Width.Should().Be(320);
        title.Height.Should().Be(200);
    }

    [Fact]
    public void Audiot_slices_into_the_shareware_chunk_set()
    {
        var assets = Load();
        assets.Audio.Chunks.Length.Should().Be(288);
        assets.Audio.MusicCount.Should().Be(27);

        // Every AdLib effect chunk carries at least its instrument
        // header; music chunks are IMF data with a leading length word.
        (assets.Audio.GetAdlibSound(0).Length > 0).Should().BeTrue();
        (assets.Audio.GetMusic(0).Length > 2).Should().BeTrue();
    }
}
