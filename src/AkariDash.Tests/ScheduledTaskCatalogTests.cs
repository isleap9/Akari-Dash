using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

public class ScheduledTaskCatalogTests
{
    // Defender's own tasks are on the Security page (SecurityCatalogTests); every other task is here.
    private static readonly IReadOnlyList<DeclaredTweak> Tasks =
        TweakCatalog.All.Where(tweak => tweak.Category != Category.Security && tweak.Targets.All(target => target.Location is ScheduledTaskLocation)).ToList();

    public static TheoryData<string> TaskTweakIds => new(Tasks.Select(tweak => tweak.Id));

    private static DeclaredTweak Find(string id) => Tasks.Single(tweak => tweak.Id == id);

    [Fact]
    public void Every_task_is_in_one_scheduled_tasks_section_on_the_gaming_page()
    {
        Assert.All(Tasks, tweak => Assert.Equal((Category.Gaming, "Scheduled tasks"), (tweak.Category, tweak.Group)));
        Assert.Equal(20, Tasks.SelectMany(tweak => tweak.Targets).Select(target => target.Location).Distinct().Count());
    }

    [Fact]
    public void Includes_every_task_from_winhance_including_pca_patch_db()
    {
        Assert.Contains(
            Tasks.SelectMany(tweak => tweak.Targets),
            target => target.Location == new ScheduledTaskLocation(@"\Microsoft\Windows\Application Experience\PcaPatchDbTask"));
    }

    [Theory]
    [MemberData(nameof(TaskTweakIds))]
    public void Task_tweaks_switch_every_task_off_and_on(string id)
    {
        var tweak = Find(id);

        Assert.All(tweak.Targets, target =>
        {
            Assert.Equal(TaskEnabledValue.Off, tweak.Options.Single(option => option.Id == "off").Values[target]);
            Assert.Equal(TaskEnabledValue.On, tweak.Options.Single(option => option.Id == "on").Values[target]);
        });
    }

    [Theory]
    [InlineData("services.compatibility-appraiser")]
    [InlineData("services.ceip-consolidator")]
    public void Background_telemetry_with_a_performance_case_recommends_off(string id)
    {
        Assert.Equal("off", Find(id).Recommended?.Id);
    }

    [Fact]
    public void Drive_optimization_has_no_recommended_option_because_it_trims_ssds()
    {
        Assert.Null(Find("services.scheduled-drive-optimization").Recommended);
    }
}
