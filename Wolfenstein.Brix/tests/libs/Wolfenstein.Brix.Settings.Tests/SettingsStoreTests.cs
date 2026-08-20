using System;
using System.IO;
using System.Linq;
using CodeBrix.Platform.AppSettings;
using CodeBrix.Sqlite;
using Wolfenstein.Brix.Settings;
using SilverAssertions;
using Xunit;

namespace Wolfenstein.Brix.Settings.Tests;

// These tests exercise the CodeBrix.Platform.AppSettings store that the
// Wolfenstein.Brix.Settings facade wraps. The add-in's store has no public test
// clock, so assertions about timestamped file names match on the naming
// pattern rather than exact names.
public class SettingsStoreTests : IDisposable
{
    readonly string root;
    readonly string directory;
    readonly string externalDirectory;

    public SettingsStoreTests()
    {
        root = Path.Combine(Path.GetTempPath(), "wolfenstein-brix-tests", Path.GetRandomFileName());
        directory = Path.Combine(root, "settings");
        externalDirectory = Path.Combine(root, "external");
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
    }

    AppSettingsStore CreateStore() => new AppSettingsStore(SettingsService.AppName, directory);

    string ExternalPath(string name)
    {
        Directory.CreateDirectory(externalDirectory);
        return Path.Combine(externalDirectory, name);
    }

    // Builds a valid settings.sqlite (in its own folder outside the store
    // under test) holding the given value, and returns its path.
    string CreateExternalSettingsFile(string key, string value)
    {
        var sourceDirectory = Path.Combine(externalDirectory, Path.GetRandomFileName());
        using (var source = new AppSettingsStore(SettingsService.AppName, sourceDirectory))
            source.Set(key, value);
        return Path.Combine(sourceDirectory, AppSettingsStore.SettingsFileName);
    }

    // The auto-backup files whose names carry a parseable timestamp,
    // alphabetical (= chronological, the naming scheme's guarantee).
    string[] AutoBackupFiles() =>
        Directory.EnumerateFiles(directory, $"{AppSettingsStore.AutoBackupFilePrefix}*.sqlite")
            .Select(Path.GetFileName)
            .Where(HasParseableTimestamp)
            .OrderBy(name => name)
            .ToArray();

    static bool HasParseableTimestamp(string fileName)
    {
        var stampText = fileName.Substring(AppSettingsStore.AutoBackupFilePrefix.Length,
            fileName.Length - AppSettingsStore.AutoBackupFilePrefix.Length - ".sqlite".Length);
        return DateTime.TryParseExact(stampText, AppSettingsStore.TimestampFormat,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _);
    }

    [Fact]
    public void Missing_file_is_silently_created_fresh()
    {
        //Act
        using var store = CreateStore();

        //Assert
        store.WasCreatedFresh.Should().BeTrue();
        File.Exists(Path.Combine(directory, AppSettingsStore.SettingsFileName)).Should().BeTrue();
    }

    [Fact]
    public void Get_returns_default_when_not_set()
    {
        //Arrange
        using var store = CreateStore();

        //Assert
        store.Get("Wolfenstein.Brix.Test.Missing", 42).Should().Be(42);
        store.Get<string>("Wolfenstein.Brix.Test.Missing").Should().BeNull();
        store.HasValue("Wolfenstein.Brix.Test.Missing").Should().BeFalse();
    }

    [Fact]
    public void Set_and_Get_round_trip_common_types()
    {
        //Arrange
        using var store = CreateStore();

        //Act
        store.Set("Wolfenstein.Brix.Test.String", "hello");
        store.Set("Wolfenstein.Brix.Test.Int", 7);
        store.Set("Wolfenstein.Brix.Test.Bool", true);
        store.Set("Wolfenstein.Brix.Test.Enum", DayOfWeek.Friday);

        //Assert
        store.Get<string>("Wolfenstein.Brix.Test.String").Should().Be("hello");
        store.Get<int>("Wolfenstein.Brix.Test.Int").Should().Be(7);
        store.Get<bool>("Wolfenstein.Brix.Test.Bool").Should().BeTrue();
        store.Get<DayOfWeek>("Wolfenstein.Brix.Test.Enum").Should().Be(DayOfWeek.Friday);
    }

    [Fact]
    public void Values_persist_across_reopen()
    {
        //Arrange
        using (var store = CreateStore())
            store.Set("Wolfenstein.Brix.Test.Persisted", "survives");

        //Act
        using var reopened = CreateStore();

        //Assert
        reopened.WasCreatedFresh.Should().BeFalse();
        reopened.Get<string>("Wolfenstein.Brix.Test.Persisted").Should().Be("survives");
    }

    [Fact]
    public void Set_null_removes_the_key()
    {
        //Arrange
        using var store = CreateStore();
        store.Set("Wolfenstein.Brix.Test.Removed", "value");

        //Act
        store.Set("Wolfenstein.Brix.Test.Removed", null);

        //Assert
        store.HasValue("Wolfenstein.Brix.Test.Removed").Should().BeFalse();
    }

    [Fact]
    public void Set_returns_true_only_when_the_value_changes()
    {
        //Arrange
        using var store = CreateStore();

        //Assert
        store.Set("Wolfenstein.Brix.Test.Changed", "a").Should().BeTrue();
        store.Set("Wolfenstein.Brix.Test.Changed", "a").Should().BeFalse();
        store.Set("Wolfenstein.Brix.Test.Changed", "b").Should().BeTrue();
    }

    [Fact]
    public void Set_raises_change_events()
    {
        //Arrange
        using var store = CreateStore();
        AppSettingChangedEventArgs broadcast = null;
        AppSettingChangedEventArgs keyed = null;
        store.SettingChanged += (_, args) => broadcast = args;
        store.AddSettingHandler("Wolfenstein.Brix.Test.Watched", (_, args) => keyed = args);

        //Act
        store.Set("Wolfenstein.Brix.Test.Watched", "new");

        //Assert
        broadcast.Should().NotBeNull();
        broadcast.Key.Should().Be("Wolfenstein.Brix.Test.Watched");
        keyed.Should().NotBeNull();
        keyed.NewValue.Should().Be("new");
    }

    [Fact]
    public void Startup_creates_an_autobackup_with_the_timestamp_naming_scheme()
    {
        //Act
        using var store = CreateStore();

        //Assert — exactly one backup, and its name parses under the scheme
        AutoBackupFiles().Should().HaveCount(1);
    }

    [Fact]
    public void Autobackup_is_a_complete_usable_database()
    {
        //Arrange — the value lands in the file the NEXT start backs up
        using (var store = CreateStore())
            store.Set("Wolfenstein.Brix.Test.InBackup", "captured");
        using (CreateStore()) { }

        //Act — pretend the main file was lost and only the newest backup remains.
        var newestBackup = AutoBackupFiles().Last();
        File.Delete(Path.Combine(directory, AppSettingsStore.SettingsFileName));
        File.Copy(Path.Combine(directory, newestBackup),
            Path.Combine(directory, AppSettingsStore.SettingsFileName));
        using var restored = CreateStore();

        //Assert
        restored.Get<string>("Wolfenstein.Brix.Test.InBackup").Should().Be("captured");
    }

    [Fact]
    public void Prune_keeps_only_the_newest_n_by_filename_timestamp()
    {
        //Arrange — retention 3, with stale backups whose file times are
        // deliberately misleading (all identical), so only the name matters.
        // The manufactured 2026-07 names sort before any real "now" stamp.
        using (var store = CreateStore())
            store.AutoBackupRetention = 3;
        var seed = Path.Combine(directory, AutoBackupFiles().Single());
        foreach (var stamp in new[] { "2026-07-02_00-00-00", "2026-07-03_00-00-00", "2026-07-04_00-00-00" })
            File.Copy(seed, Path.Combine(directory,
                $"{AppSettingsStore.AutoBackupFilePrefix}{stamp}.sqlite"), overwrite: true);

        //Act
        using (CreateStore()) { }

        //Assert — the newest three remain (the fresh backup counts toward n);
        // the oldest manufactured name is pruned away.
        var remaining = AutoBackupFiles();
        remaining.Should().HaveCount(3);
        remaining.Should().NotContain($"{AppSettingsStore.AutoBackupFilePrefix}2026-07-02_00-00-00.sqlite");
        remaining.Should().Contain($"{AppSettingsStore.AutoBackupFilePrefix}2026-07-04_00-00-00.sqlite");
    }

    [Fact]
    public void Retention_zero_creates_no_backup_and_prunes_nothing()
    {
        //Arrange
        using (var store = CreateStore())
            store.AutoBackupRetention = 0;
        var before = AutoBackupFiles();

        //Act
        using (CreateStore()) { }

        //Assert
        AutoBackupFiles().Should().Equal(before);
    }

    [Fact]
    public void Files_not_matching_the_autobackup_scheme_are_never_deleted()
    {
        //Arrange
        using (var store = CreateStore())
            store.AutoBackupRetention = 1;
        var manualCopy = Path.Combine(directory, "settings_bak_bob_before_changes.sqlite");
        File.Copy(Path.Combine(directory, AppSettingsStore.SettingsFileName), manualCopy);
        // Matches the prefix and extension but has no parseable timestamp.
        var oddName = Path.Combine(directory,
            $"{AppSettingsStore.AutoBackupFilePrefix}not-a-timestamp.sqlite");
        File.WriteAllText(oddName, "not a database");

        //Act
        using (CreateStore()) { }

        //Assert — the manual copy and the unparseable name survive; retention 1
        // leaves exactly one real timestamped backup.
        File.Exists(manualCopy).Should().BeTrue();
        File.Exists(oddName).Should().BeTrue();
        AutoBackupFiles().Should().HaveCount(1);
    }

    [Fact]
    public void Corrupt_file_is_quarantined_and_restored_from_newest_backup()
    {
        //Arrange — a healthy run that leaves a backup containing the value.
        using (var store = CreateStore())
            store.Set("Wolfenstein.Brix.Test.Value", "from-backup");
        using (CreateStore()) { }
        File.WriteAllText(Path.Combine(directory, AppSettingsStore.SettingsFileName),
            "this is not a sqlite database");

        //Act
        using var store2 = CreateStore();

        //Assert
        store2.WasRestoredFromBackup.Should().BeTrue();
        store2.WasCreatedFresh.Should().BeFalse();
        store2.Get<string>("Wolfenstein.Brix.Test.Value").Should().Be("from-backup");
        Directory.EnumerateFiles(directory, $"{AppSettingsStore.CorruptFilePrefix}*.sqlite")
            .Should().NotBeEmpty();
    }

    [Fact]
    public void Corrupt_file_without_backups_starts_fresh()
    {
        //Arrange
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, AppSettingsStore.SettingsFileName), "garbage");

        //Act
        using var store = CreateStore();

        //Assert
        store.WasCreatedFresh.Should().BeTrue();
        store.WasRestoredFromBackup.Should().BeFalse();
        Directory.EnumerateFiles(directory, $"{AppSettingsStore.CorruptFilePrefix}*.sqlite")
            .Should().NotBeEmpty();
    }

    [Fact]
    public void Retention_read_is_clamped_to_the_legal_range()
    {
        //Arrange — write the raw key past the maximum, bypassing the property's
        // own write-side clamp.
        using var store = CreateStore();
        store.Set(AppSettingsStore.AutoBackupRetentionKey, 99);

        //Assert
        store.AutoBackupRetention.Should().Be(AppSettingsStore.MaxAutoBackupRetention);
    }

    [Fact]
    public void Mismatched_type_read_returns_the_default()
    {
        //Arrange
        using var store = CreateStore();
        store.Set("Wolfenstein.Brix.Test.Typed", "not a number");

        //Assert
        store.Get("Wolfenstein.Brix.Test.Typed", 5).Should().Be(5);
    }

    [Fact]
    public void Export_writes_a_complete_self_contained_copy()
    {
        //Arrange
        using var store = CreateStore();
        store.Set("Wolfenstein.Brix.Test.Exported", "travels");
        var exportPath = ExternalPath("my-settings.sqlite");

        //Act
        store.ExportToFile(exportPath);

        //Assert — the single exported file, used as settings.sqlite of a
        // brand-new installation, carries the value with no companion files.
        File.Exists(exportPath).Should().BeTrue();
        File.Exists(exportPath + "-wal").Should().BeFalse();
        File.Exists(exportPath + "-shm").Should().BeFalse();
        var otherInstallation = Path.Combine(root, "other-installation");
        Directory.CreateDirectory(otherInstallation);
        File.Copy(exportPath, Path.Combine(otherInstallation, AppSettingsStore.SettingsFileName));
        using var reopened = new AppSettingsStore(SettingsService.AppName, otherInstallation);
        reopened.Get<string>("Wolfenstein.Brix.Test.Exported").Should().Be("travels");
    }

    [Fact]
    public void Export_into_the_settings_folder_is_rejected()
    {
        //Arrange
        using var store = CreateStore();

        //Act
        Action direct = () => store.ExportToFile(Path.Combine(directory, "copy.sqlite"));
        Action nested = () => store.ExportToFile(Path.Combine(directory, "sub", "copy.sqlite"));

        //Assert
        direct.Should().Throw<InvalidOperationException>();
        nested.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Import_stages_a_validated_incoming_file()
    {
        //Arrange
        using var store = CreateStore();
        var sourcePath = CreateExternalSettingsFile("Wolfenstein.Brix.Test.Imported", "incoming");

        //Act
        store.StageIncomingFile(sourcePath);

        //Assert
        File.Exists(Path.Combine(directory, AppSettingsStore.IncomingFileName)).Should().BeTrue();
    }

    [Fact]
    public void Import_rejects_a_non_database_file()
    {
        //Arrange
        using var store = CreateStore();
        var sourcePath = ExternalPath("not-a-database.sqlite");
        File.WriteAllText(sourcePath, "this is not a sqlite database");

        //Act
        Action act = () => store.StageIncomingFile(sourcePath);

        //Assert
        act.Should().Throw<InvalidDataException>();
        File.Exists(Path.Combine(directory, AppSettingsStore.IncomingFileName)).Should().BeFalse();
    }

    [Fact]
    public void Import_rejects_a_database_without_the_setting_table()
    {
        //Arrange — a healthy SQLite database that is not a settings file.
        using var store = CreateStore();
        var sourcePath = ExternalPath("other-database.sqlite");
        using (var other = new SqliteDatabase(sourcePath, null, new SqliteDatabaseOptions()))
        {
            other.SafeOpen();
            other.ExecuteNonQuery("CREATE TABLE NotSettings (Id INTEGER PRIMARY KEY)");
        }

        //Act
        Action act = () => store.StageIncomingFile(sourcePath);

        //Assert
        act.Should().Throw<InvalidDataException>();
        File.Exists(Path.Combine(directory, AppSettingsStore.IncomingFileName)).Should().BeFalse();
    }

    [Fact]
    public void Incoming_file_is_adopted_on_startup()
    {
        //Arrange — a store holding the old value, with an import staged.
        var sourcePath = CreateExternalSettingsFile("Wolfenstein.Brix.Test.Key", "new");
        using (var store = CreateStore())
        {
            store.Set("Wolfenstein.Brix.Test.Key", "old");
            store.StageIncomingFile(sourcePath);
        }

        //Act
        using var reopened = CreateStore();

        //Assert — the import took over; the previous file was kept.
        reopened.WasReplacedByImport.Should().BeTrue();
        reopened.WasCreatedFresh.Should().BeFalse();
        reopened.Get<string>("Wolfenstein.Brix.Test.Key").Should().Be("new");
        File.Exists(Path.Combine(directory, AppSettingsStore.IncomingFileName)).Should().BeFalse();
        Directory.EnumerateFiles(directory, $"{AppSettingsStore.OldFilePrefix}*.sqlite")
            .Should().NotBeEmpty();
    }

    [Fact]
    public void Adoption_without_an_existing_settings_file_uses_the_incoming_file_directly()
    {
        //Arrange — a staged import in a folder with no settings.sqlite yet.
        var sourcePath = CreateExternalSettingsFile("Wolfenstein.Brix.Test.Key", "fresh-import");
        Directory.CreateDirectory(directory);
        using (var source = new AppSettingsStore(SettingsService.AppName, Path.GetDirectoryName(sourcePath)))
            source.ExportToFile(Path.Combine(directory, AppSettingsStore.IncomingFileName));

        //Act
        using var store = CreateStore();

        //Assert
        store.WasReplacedByImport.Should().BeTrue();
        store.Get<string>("Wolfenstein.Brix.Test.Key").Should().Be("fresh-import");
        Directory.EnumerateFiles(directory, $"{AppSettingsStore.OldFilePrefix}*.sqlite").Should().BeEmpty();
    }

    [Fact]
    public void Facade_initializes_reads_writes_and_shuts_down()
    {
        //Arrange — the facade is process-global state, so this test (the only
        // facade user in the assembly) restores it to uninitialized when done.
        SettingsService.IsInitialized.Should().BeFalse();
        try
        {
            //Act
            SettingsService.Initialize(directory);

            //Assert
            SettingsService.IsInitialized.Should().BeTrue();
            SettingsService.Store.AppName.Should().Be(SettingsService.AppName);
            SettingsService.Set("Wolfenstein.Brix.Test.Facade", "works");
            SettingsService.HasValue("Wolfenstein.Brix.Test.Facade").Should().BeTrue();
            SettingsService.Get<string>("Wolfenstein.Brix.Test.Facade").Should().Be("works");
            SettingsService.Get("Wolfenstein.Brix.Test.FacadeMissing", 3).Should().Be(3);
        }
        finally
        {
            SettingsService.Shutdown();
        }
        SettingsService.IsInitialized.Should().BeFalse();
    }
}
