using System;
using System.IO;
using Doom.Brix.Settings;
using ManagedDoom;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Game.Tests;

// Storage-level round-trips for the settings.sqlite-backed IDoomStorage. The
// app's SettingsService is a process-global singleton, so it is initialized
// once (to a throwaway folder) for the whole test process; each test then
// clears the keys it uses so method ordering does not matter.
public class SqliteDoomStorageTests
{
    static SqliteDoomStorageTests()
    {
        if (!SettingsService.IsInitialized)
        {
            var dir = Path.Combine(
                Path.GetTempPath(), "DoomBrixSqliteStorageTests_" + Guid.NewGuid().ToString("N"));
            SettingsService.Initialize(dir);
        }
    }

    private readonly SqliteDoomStorage storage = new SqliteDoomStorage();

    public SqliteDoomStorageTests()
    {
        // Start each test from a clean slate.
        SettingsService.Set(SqliteDoomStorage.ConfigKey, null);
        for (var i = 0; i < SaveSlots.SlotCount; i++)
        {
            SettingsService.Set(SqliteDoomStorage.SlotDataKey(i), null);
            SettingsService.Set(SqliteDoomStorage.SlotDescriptionKey(i), null);
        }
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
    public void Config_text_round_trips_through_settings_sqlite()
    {
        //Act + Assert
        storage.LoadConfigText().Should().Be(null); // nothing stored yet
        storage.SaveConfigText("hello = world\nfoo = bar");
        storage.LoadConfigText().Should().Be("hello = world\nfoo = bar");
    }

    [Fact]
    public void Save_slot_data_and_description_round_trip()
    {
        //Arrange
        var data = MakeSlotBytes("MY SAVE");

        //Act
        storage.SaveSaveSlot(3, "MY SAVE", data);

        //Assert
        data.AsSpan().SequenceEqual(storage.LoadSaveSlot(3)).Should().Be(true);
        var descriptions = storage.ReadSlotDescriptions();
        descriptions.Length.Should().Be(SaveSlots.SlotCount);
        descriptions[3].Should().Be("MY SAVE");
        // The slot is persisted under the documented key.
        SettingsService.HasValue(SqliteDoomStorage.SlotDataKey(3)).Should().Be(true);
    }

    [Fact]
    public void Slot_description_is_normalized_like_vanilla()
    {
        //Arrange: a mixed-case name, embedded in the payload as SaveGame would.
        var data = MakeSlotBytes("My Save");

        //Act
        storage.SaveSaveSlot(1, "My Save", data);

        //Assert: DoomInterop.ToString uppercases, matching file-based names.
        storage.ReadSlotDescriptions()[1].Should().Be("MY SAVE");
    }

    [Fact]
    public void Empty_slots_read_as_null()
    {
        //Assert
        (storage.LoadSaveSlot(5) == null).Should().Be(true);
        var descriptions = storage.ReadSlotDescriptions();
        descriptions.Length.Should().Be(SaveSlots.SlotCount);
        for (var i = 0; i < descriptions.Length; i++)
        {
            (descriptions[i] == null).Should().Be(true);
        }
    }
}
