using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class ExplorerCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Explorer =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Explorer).ToList();

    private static DeclaredTweak Find(string id) => Explorer.Single(tweak => tweak.Id == id);

    private static RegistryLocation Location(DeclaredTweak tweak) =>
        (RegistryLocation)Assert.Single(tweak.Targets).Location;

    [Fact]
    public void Explorer_page_is_ten_curated_rows_not_a_port_of_folder_options()
    {
        // ADR-0005: a port of Winhance's 48 Explorer values would have been 34 rows. This page is
        // the six that moved off the Windows page and four that earned a place.
        Assert.Equal(10, Explorer.Count);
        Assert.All(Explorer, tweak =>
        {
            Assert.StartsWith("explorer.", tweak.Id);
            Assert.All(tweak.Targets, target => Assert.Equal(RegistryHive.CurrentUser, target.Location switch
            {
                RegistryLocation value => value.Hive,
                RegistryKeyLocation key => key.Hive,
                var other => throw new Xunit.Sdk.XunitException($"{other} is not a registry target"),
            }));
        });
        Assert.Equal(["Explorer's Home", "Working with files"], Explorer.Select(tweak => tweak.Group).Distinct());
    }

    [Fact]
    public void How_explorer_looks_is_a_matter_of_taste_so_none_has_a_recommended_option()
    {
        Assert.All(Explorer, tweak => Assert.Null(tweak.Recommended));
    }

    [Fact]
    public void A_missing_value_reads_as_an_option_rather_than_custom()
    {
        // The shortcut suffix and the classic menu have no AbsentMeans because absent is their own
        // Option: "Add it" and "Windows 11 menu" delete.
        foreach (var tweak in Explorer)
        {
            Assert.True(
                tweak.Options.Any(option => tweak.Targets.All(target =>
                    option.Values[target] is null || Equals(option.Values[target], target.AbsentMeans))),
                $"{tweak.Id} reads as Custom on a fresh Windows");
        }
    }

    [Theory]
    [InlineData("explorer.file-extensions", "windows.file-extensions")]
    [InlineData("explorer.hidden-files", "windows.hidden-files")]
    [InlineData("explorer.open-to", "windows.open-explorer-to")]
    [InlineData("explorer.full-path", "windows.full-path")]
    [InlineData("explorer.frequent-folders", "windows.frequent-folders")]
    [InlineData("explorer.recent-files", "windows.recent-files")]
    public void The_six_rows_that_moved_off_the_windows_page_kept_their_original_values(string id, string old)
    {
        Assert.Equal(id, RenamedTweaks.Ids[old]);
        Assert.Contains(Explorer, tweak => tweak.Id == id);
        Assert.DoesNotContain(TweakCatalog.All, tweak => tweak.Id == old);
    }

    [Fact]
    public void File_extensions_and_hidden_files_keep_their_odd_values()
    {
        // HideFileExt shows at 0; Hidden hides at 2, not 0. Getting either backwards flips the row.
        var extensions = Find("explorer.file-extensions");
        var ext = Assert.Single(extensions.Targets);
        Assert.Equal(RegistryValue.DWord(0), extensions.Options.Single(option => option.Id == "show").Values[ext]);
        Assert.Equal(RegistryValue.DWord(1), ext.AbsentMeans);

        var hidden = Find("explorer.hidden-files");
        var target = Assert.Single(hidden.Targets);
        Assert.Equal(RegistryValue.DWord(2), hidden.Options.Single(option => option.Id == "hide").Values[target]);
        Assert.Equal(RegistryValue.DWord(2), target.AbsentMeans);
    }

    [Fact]
    public void The_shortcut_suffix_writes_four_zero_bytes_and_undoes_by_deleting()
    {
        var tweak = Find("explorer.shortcut-suffix");
        var target = Assert.Single(tweak.Targets);

        Assert.Equal("link", Location(tweak).Name);
        Assert.Null(target.AbsentMeans);
        Assert.Null(tweak.Options.Single(option => option.Id == "keep").Values[target]);
        Assert.Equal(
            new RegistryValue(RegistryValueKind.Binary, new byte[] { 0, 0, 0, 0 }),
            tweak.Options.Single(option => option.Id == "drop").Values[target]);
    }

    [Theory]
    [InlineData("explorer.sync-notifications", "ShowSyncProviderNotifications")]
    [InlineData("explorer.office-files", "ShowCloudFilesInQuickAccess")]
    public void The_two_new_show_rows_are_on_when_missing(string id, string name)
    {
        var tweak = Find(id);
        Assert.Equal(name, Location(tweak).Name);
        Assert.Equal(RegistryValue.DWord(1), Assert.Single(tweak.Targets).AbsentMeans);
    }

    [Fact]
    public void The_classic_menu_creates_the_clsid_key_and_its_empty_handler()
    {
        var tweak = Find("explorer.classic-context-menu");
        var classic = tweak.Options.Single(option => option.Id == "classic");
        var windows11 = tweak.Options.Single(option => option.Id == "windows-11");

        var key = Assert.IsType<RegistryKeyLocation>(tweak.Targets[0].Location);
        Assert.Equal(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", key.Key);
        var handler = Assert.IsType<RegistryLocation>(tweak.Targets[1].Location);
        Assert.Equal($@"{key.Key}\InprocServer32", handler.Key);
        Assert.Equal("", handler.Name);

        Assert.Equal(KeyPresence.Present, classic.Values[tweak.Targets[0]]);
        Assert.Equal(RegistryValue.String(""), classic.Values[tweak.Targets[1]]);
        Assert.All(tweak.Targets, target => Assert.Null(windows11.Values[target]));
        Assert.Equal(Activation.AfterSignOut, tweak.Activation);
    }

    [Fact]
    public void Undoing_the_classic_menu_takes_the_whole_key_away_not_just_its_value()
    {
        // Winhance's revert deletes only the value, which leaves InprocServer32 behind and the classic
        // menu with it. AkariOS-Ultimate's deletes the key, and so does Undo.
        var machine = new InMemoryMachine();
        var engine = new TweakEngine(machine);
        var tweak = Find("explorer.classic-context-menu");
        var key = (RegistryKeyLocation)tweak.Targets[0].Location;

        Assert.Equal("windows-11", Assert.IsType<LiveState.InOption>(engine.ReadLiveState(tweak)).Option.Id);

        engine.Apply(tweak, tweak.Options.Single(option => option.Id == "classic"));
        Assert.Equal(KeyPresence.Present, machine.Read(key));
        Assert.Equal("classic", Assert.IsType<LiveState.InOption>(engine.ReadLiveState(tweak)).Option.Id);

        engine.Undo(tweak);
        Assert.Null(machine.Read(key));
        Assert.Null(machine.Read(new RegistryKeyLocation(key.Hive, $@"{key.Key}\InprocServer32")));
        Assert.Equal("windows-11", Assert.IsType<LiveState.InOption>(engine.ReadLiveState(tweak)).Option.Id);
    }

    [Fact]
    public void A_pc_that_already_had_the_classic_menu_keeps_it_after_undo()
    {
        // The key existed before Akari-Dash came along, so it is the machine's own and Undo puts it back.
        var machine = new InMemoryMachine();
        var tweak = Find("explorer.classic-context-menu");
        machine.Write(tweak.Targets[1].Location, RegistryValue.String(""));

        var engine = new TweakEngine(machine);
        engine.Apply(tweak, tweak.Options.Single(option => option.Id == "windows-11"));
        Assert.Null(machine.Read(tweak.Targets[0].Location));

        engine.Undo(tweak);
        Assert.Equal(KeyPresence.Present, machine.Read(tweak.Targets[0].Location));
        Assert.Equal(RegistryValue.String(""), machine.Read(tweak.Targets[1].Location));
    }

    [Theory]
    [InlineData("ShowTypeOverlay")]
    [InlineData("FolderContentsInfoTip")]
    [InlineData("ShowInfoTip")]
    [InlineData("ShowStatusBar")]
    [InlineData("ShowPreviewHandlers")]
    [InlineData("ShowEncryptCompressedColor")]
    [InlineData("HideMergeConflicts")]
    [InlineData("AutoCheckSelect")]
    [InlineData("SharingWizardOn")]
    [InlineData("ShowDriveLettersFirst")]
    [InlineData("HideDrivesWithNoMedia")]
    [InlineData("UseCompactMode")]
    [InlineData("TypeAhead")]
    [InlineData("SeparateProcess")]
    [InlineData("PersistBrowsers")]
    [InlineData("ShowSuperHidden")]
    [InlineData("NavPaneShowAllCloudStates")]
    [InlineData("NavPaneExpandToCurrentFolder")]
    [InlineData("NavPaneShowAllFolders")]
    [InlineData("System.IsPinnedToNameSpaceTree")]
    [InlineData("ShellState")]
    [InlineData("IconUnderline")]
    public void The_folder_options_checklist_is_left_out(string name)
    {
        // Declined under ADR-0005, each with its reason at the top of TweakCatalog.Explorer.cs.
        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets),
            target => target.Location switch
            {
                RegistryLocation location => location.Name == name,
                RegistryBitLocation bit => bit.Name == name,
                _ => false,
            });
    }

    [Fact]
    public void Nothing_else_in_the_catalog_writes_a_value_this_page_owns()
    {
        var mine = Explorer.SelectMany(tweak => tweak.Targets).Select(target => target.Location).ToHashSet();
        var others = TweakCatalog.All
            .Where(tweak => tweak.Category != Category.Explorer)
            .SelectMany(tweak => tweak.Targets.Select(target => target.Location));

        Assert.DoesNotContain(others, mine.Contains);
    }
}
