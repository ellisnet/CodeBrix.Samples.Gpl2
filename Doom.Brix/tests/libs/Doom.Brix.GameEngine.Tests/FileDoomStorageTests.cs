using System;
using System.IO;
using ManagedDoom;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.GameEngine.Tests;

// Storage-level round-trips for the default (file-based) IDoomStorage, over a
// throwaway DataDirectory. Test methods within a class run sequentially, and no
// other test reads ConfigUtilities.DataDirectory, so mutating that global here
// is safe.
public class FileDoomStorageTests
{
    private static FileDoomStorage NewStorageInTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DoomBrixFileStorageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ConfigUtilities.DataDirectory = dir;
        return new FileDoomStorage();
    }

    // Builds a minimal .dsg-shaped payload with the description embedded in the
    // first 24 bytes, exactly as SaveGame writes it.
    private static byte[] MakeSlotBytes(string description)
    {
        var data = new byte[64];
        for (var i = 0; i < description.Length; i++)
        {
            data[i] = (byte)description[i];
        }

        return data;
    }

    [Fact]
    public void Config_text_round_trips_through_a_file()
    {
        //Arrange
        var storage = NewStorageInTempDirectory();

        //Act + Assert
        storage.LoadConfigText().Should().Be(null); // nothing stored yet
        storage.SaveConfigText("hello = world");
        storage.LoadConfigText().Should().Be("hello = world");
    }

    [Fact]
    public void Save_slot_data_and_description_round_trip()
    {
        //Arrange
        var storage = NewStorageInTempDirectory();
        var data = MakeSlotBytes("MY SAVE");

        //Act
        storage.SaveSaveSlot(2, "MY SAVE", data);

        //Assert
        data.AsSpan().SequenceEqual(storage.LoadSaveSlot(2)).Should().Be(true);
        var descriptions = storage.ReadSlotDescriptions();
        descriptions.Length.Should().Be(SaveSlots.SlotCount);
        descriptions[2].Should().Be("MY SAVE");
    }

    [Fact]
    public void Empty_slots_read_as_null()
    {
        //Arrange
        var storage = NewStorageInTempDirectory();

        //Assert
        (storage.LoadSaveSlot(0) == null).Should().Be(true);
        var descriptions = storage.ReadSlotDescriptions();
        descriptions.Length.Should().Be(SaveSlots.SlotCount);
        for (var i = 0; i < descriptions.Length; i++)
        {
            (descriptions[i] == null).Should().Be(true);
        }
    }
}
