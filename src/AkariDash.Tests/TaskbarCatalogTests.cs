using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

public class TaskbarCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Taskbar =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Taskbar).ToList();

    private static DeclaredTweak Find(string id) => Taskbar.Single(tweak => tweak.Id == id);

    private static RegistryLocation Location(DeclaredTweak tweak) =>
        (RegistryLocation)Assert.Single(tweak.Targets).Location;

    [Fact]
    public void Taskbar_page_holds_only_registry_tweaks_in_its_two_groups()
    {
        Assert.Equal(8, Taskbar.Count);
        Assert.All(Taskbar, tweak =>
        {
            Assert.StartsWith("taskbar.", tweak.Id);
            Assert.All(tweak.Targets, target => Assert.IsType<RegistryLocation>(target.Location));
        });
        Assert.Equal(
            ["What the taskbar shows", "Taskbar behaviour"],
            Taskbar.Select(tweak => tweak.Group).Distinct());
    }

    [Fact]
    public void How_the_taskbar_looks_is_a_matter_of_taste_so_none_has_a_recommended_option()
    {
        Assert.All(Taskbar, tweak => Assert.Null(tweak.Recommended));
    }

    [Fact]
    public void A_missing_value_reads_as_an_option_rather_than_custom_except_for_the_widgets_policy()
    {
        // Widgets is a policy: absent is not a state it can claim, so one Option deletes it instead.
        Assert.All(Taskbar.Where(tweak => tweak.Id != "taskbar.widgets"), tweak =>
            Assert.Contains(tweak.Options, option =>
                tweak.Targets.All(target => target.AbsentMeans is { } absent && Equals(option.Values[target], absent))));

        var widgets = Find("taskbar.widgets");
        Assert.Null(Assert.Single(widgets.Targets).AbsentMeans);
        Assert.Contains(widgets.Options, option => option.Values.Values.Single() is null);
    }

    [Fact]
    public void Search_has_the_four_modes_microsoft_documents_with_the_search_box_as_windows_default()
    {
        // ConfigureSearchOnTaskbarMode, and Winhance's MIT snapshot: 0 hidden, 1 the icon, 2 the icon
        // with a label, 3 the search box. The Windows page had 2 and 3 the wrong way round, so a
        // stock machine read the box from an absent value that stands for the box under a different
        // number, and picking "Search box" wrote the icon and label.
        var tweak = Find("taskbar.search");
        var target = Assert.Single(tweak.Targets);

        Assert.Equal(["hidden", "icon", "icon-label", "box"], tweak.Options.Select(option => option.Id));
        Assert.Equal(
            [RegistryValue.DWord(0), RegistryValue.DWord(1), RegistryValue.DWord(2), RegistryValue.DWord(3)],
            tweak.Options.Select(option => option.Values[target]));
        Assert.Equal(RegistryValue.DWord(3), target.AbsentMeans);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Search", ((RegistryLocation)target.Location).Key);
    }

    [Theory]
    [InlineData("taskbar.task-view", "ShowTaskViewButton")]
    [InlineData("taskbar.chat", "TaskbarMn")]
    [InlineData("taskbar.alignment", "TaskbarAl")]
    [InlineData("taskbar.show-desktop-corner", "TaskbarSd")]
    [InlineData("taskbar.share-window", "TaskbarSn")]
    public void The_taskbars_own_values_live_in_explorer_advanced(string id, string name)
    {
        var location = Location(Find(id));

        Assert.Equal(RegistryHive.CurrentUser, location.Hive);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", location.Key);
        Assert.Equal(name, location.Name);
    }

    [Fact]
    public void End_task_lives_under_the_developer_settings_subkey_and_is_off_when_missing()
    {
        var tweak = Find("taskbar.end-task");
        var target = Assert.Single(tweak.Targets);
        var location = (RegistryLocation)target.Location;

        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", location.Key);
        Assert.Equal("TaskbarEndTask", location.Name);
        Assert.Equal(RegistryValue.DWord(0), target.AbsentMeans);
    }

    [Fact]
    public void Widgets_writes_the_machine_policy_microsoft_documents_and_not_a_user_copy()
    {
        // Winhance writes an HKCU copy of AllowNewsAndInterests as well; the documented policy is the
        // machine one, and a second value nothing reads would only make the row read Custom.
        var location = Location(Find("taskbar.widgets"));

        Assert.Equal(RegistryHive.LocalMachine, location.Hive);
        Assert.Equal(@"SOFTWARE\Policies\Microsoft\Dsh", location.Key);
        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets),
            target => target.Location is RegistryLocation { Hive: RegistryHive.CurrentUser, Name: "AllowNewsAndInterests" });
    }

    [Theory]
    [InlineData("taskbar.search")]
    [InlineData("taskbar.alignment")]
    [InlineData("taskbar.task-view")]
    [InlineData("taskbar.widgets")]
    [InlineData("taskbar.end-task")]
    public void The_five_rows_that_moved_off_the_windows_page_kept_their_original_values(string id)
    {
        var old = RenamedTweaks.Ids.Single(pair => pair.Value == id).Key;

        Assert.StartsWith("windows.", old);
        Assert.DoesNotContain(TweakCatalog.All, tweak => tweak.Id == old);
        Assert.DoesNotContain(TweakCatalog.All.Where(tweak => tweak.Category == Category.Windows), tweak => tweak.Group == "Taskbar");
    }

    [Fact]
    public void The_copilot_taskbar_button_is_left_out_because_current_windows_has_none()
    {
        // Winhance scopes ShowCopilotButton to builds 22621-26099, and the Privacy page's Copilot
        // comment left the decision to this ticket. The decision is no.
        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets),
            target => target.Location is RegistryLocation { Name: "ShowCopilotButton" });
    }

    [Theory]
    [InlineData("HideSCAMeetNow")]
    [InlineData("EnableFeeds")]
    [InlineData("TaskbarSmallIcons")]
    [InlineData("TaskbarAcrylicOpacity")]
    [InlineData("EnableAutoTray")]
    [InlineData("IconSizePreference")]
    public void The_windows_10_taskbar_values_and_the_undocumented_ones_stay_out(string name)
    {
        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets),
            target => target.Location is RegistryLocation location && location.Name == name);
    }

    [Fact]
    public void Nothing_else_in_the_catalog_writes_a_value_this_page_owns()
    {
        var mine = Taskbar.SelectMany(tweak => tweak.Targets).Select(target => target.Location).ToHashSet();
        var others = TweakCatalog.All
            .Where(tweak => tweak.Category != Category.Taskbar)
            .SelectMany(tweak => tweak.Targets.Select(target => (tweak.Id, target.Location)));

        Assert.DoesNotContain(others, other => mine.Contains(other.Location));
    }
}
