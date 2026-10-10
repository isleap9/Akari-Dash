using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class FileOriginalValuesStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "FileOriginalValuesStoreTests." + Guid.NewGuid().ToString("N"));

    private static readonly AppliedTweak Applied = new(
        "off",
        [
            new OriginalValue(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Akari\Test", "DWord"), RegistryValue.DWord(10)),
            new OriginalValue(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Akari\Test", "String"), RegistryValue.String("text")),
            new OriginalValue(new RegistryLocation(RegistryHive.LocalMachine, @"Software\Akari\Test", "Missing"), null),
        ]);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void Unknown_tweak_has_no_record()
    {
        Assert.Null(new FileOriginalValuesStore(_folder).Get("never-applied"));
    }

    [Fact]
    public void Round_trips_a_record_including_values_that_did_not_exist()
    {
        var store = new FileOriginalValuesStore(_folder);

        store.Save("test", Applied);

        AssertSame(Applied, store.Get("test"));
    }

    [Fact]
    public void Record_survives_a_new_store_instance()
    {
        new FileOriginalValuesStore(_folder).Save("test", Applied);

        AssertSame(Applied, new FileOriginalValuesStore(_folder).Get("test"));
    }

    [Fact]
    public void Round_trips_qword_expand_string_multi_string_and_binary_values()
    {
        var location = new RegistryLocation(RegistryHive.CurrentUser, @"Software\Akari\Test", "Value");
        RegistryValue[] values =
        [
            new(RegistryValueKind.QWord, 1L << 40),
            new(RegistryValueKind.ExpandString, "%SystemRoot%"),
            new(RegistryValueKind.MultiString, new[] { "a", "b" }),
            new(RegistryValueKind.Binary, new byte[] { 1, 2, 255 }),
            new(RegistryValueKind.None, new byte[] { 7 }),
        ];

        foreach (var value in values)
        {
            new FileOriginalValuesStore(_folder).Save("test", new AppliedTweak("on", [new OriginalValue(location, value)]));

            var read = Assert.IsType<RegistryValue>(Assert.Single(new FileOriginalValuesStore(_folder).Get("test")!.OriginalValues).Value);
            Assert.Equal(value.Kind, read.Kind);
            Assert.Equal(value.Data, read.Data);
        }
    }

    [Fact]
    public void Round_trips_service_start_types_and_task_enabled_states_including_ones_that_did_not_exist()
    {
        var applied = new AppliedTweak(
            "off",
            [
                new OriginalValue(new ServiceLocation("SysMain"), new ServiceStartValue(ServiceStartType.AutomaticDelayed)),
                new OriginalValue(new ServiceLocation("Missing"), null),
                new OriginalValue(new ScheduledTaskLocation(@"\Microsoft\Windows\Defrag\ScheduledDefrag"), TaskEnabledValue.On),
                new OriginalValue(new ScheduledTaskLocation(@"\Missing"), null),
                new OriginalValue(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Akari\Test", "DWord"), RegistryValue.DWord(10)),
            ]);

        new FileOriginalValuesStore(_folder).Save("test", applied);

        AssertSame(applied, new FileOriginalValuesStore(_folder).Get("test"));
    }

    [Fact]
    public void A_value_of_the_wrong_kind_is_refused_rather_than_saved_as_did_not_exist()
    {
        var applied = new AppliedTweak("off", [new OriginalValue(new ServiceLocation("SysMain"), RegistryValue.DWord(4))]);

        Assert.Throws<ArgumentException>(() => new FileOriginalValuesStore(_folder).Save("test", applied));
    }

    [Fact]
    public void Reads_files_written_before_service_and_task_targets()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "original-values.json"), """
            {
              "test": {
                "LastAppliedOptionId": "off",
                "OriginalValues": [
                  { "Hive": -2147483647, "Key": "Software\\Akari\\Test", "Name": "DWord", "Kind": 4, "Data": 10 },
                  { "Hive": -2147483646, "Key": "Software\\Akari\\Test", "Name": "Missing", "Kind": null, "Data": null }
                ]
              }
            }
            """);

        AssertSame(
            new AppliedTweak(
                "off",
                [
                    new OriginalValue(new RegistryLocation(RegistryHive.CurrentUser, @"Software\Akari\Test", "DWord"), RegistryValue.DWord(10)),
                    new OriginalValue(new RegistryLocation(RegistryHive.LocalMachine, @"Software\Akari\Test", "Missing"), null),
                ]),
            new FileOriginalValuesStore(_folder).Get("test"));
    }

    [Fact]
    public void Clear_removes_only_that_tweak_and_survives_a_new_store_instance()
    {
        var store = new FileOriginalValuesStore(_folder);
        store.Save("cleared", Applied);
        store.Save("kept", Applied);

        store.Clear("cleared");

        var reopened = new FileOriginalValuesStore(_folder);
        Assert.Null(reopened.Get("cleared"));
        AssertSame(Applied, reopened.Get("kept"));
    }

    [Fact]
    public void Applying_a_tweak_creates_the_folder_and_file_and_undo_empties_it()
    {
        var location = new RegistryLocation(RegistryHive.CurrentUser, @"Software\Akari\Test", "Value");
        var target = new TweakTarget(location);
        var off = new TweakOption("off", "Off", new Dictionary<TweakTarget, MachineValue?> { [target] = RegistryValue.DWord(0) });
        var on = new TweakOption("on", "On", new Dictionary<TweakTarget, MachineValue?> { [target] = RegistryValue.DWord(1) });
        var tweak = new DeclaredTweak("test", "Test", "A test Tweak.", Category.Gaming, "Test Group", [target], [off, on]);
        var engine = new TweakEngine(
            new InMemoryMachine().With(location, RegistryValue.DWord(1)),
            new FileOriginalValuesStore(_folder));

        engine.Apply(tweak, off);

        var file = Path.Combine(_folder, "original-values.json");
        Assert.True(File.Exists(file));
        Assert.Contains("\"test\"", File.ReadAllText(file));

        engine.Undo(tweak);

        Assert.DoesNotContain("\"test\"", File.ReadAllText(file));
    }

    [Fact]
    public void Leaves_no_temp_file_after_save()
    {
        new FileOriginalValuesStore(_folder).Save("test", Applied);

        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    private static void AssertSame(AppliedTweak expected, AppliedTweak? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.LastAppliedOptionId, actual.LastAppliedOptionId);
        Assert.Equal(expected.OriginalValues, actual.OriginalValues);
    }
}
