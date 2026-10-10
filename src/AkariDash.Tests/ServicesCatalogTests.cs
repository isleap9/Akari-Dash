using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

public class ServicesCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Services =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Services).ToList();

    public static TheoryData<string> ServiceTweakIds =>
        new(Services.Where(tweak => tweak.Targets.All(target => target.Location is ServiceLocation)).Select(tweak => tweak.Id));

    public static TheoryData<string> TaskTweakIds =>
        new(Services.Where(tweak => tweak.Targets.All(target => target.Location is ScheduledTaskLocation)).Select(tweak => tweak.Id));

    private static DeclaredTweak Find(string id) => Services.Single(tweak => tweak.Id == id);

    [Fact]
    public void Services_page_holds_only_service_and_task_tweaks()
    {
        Assert.All(Services, tweak =>
        {
            Assert.StartsWith("services.", tweak.Id);
            Assert.All(tweak.Targets, target => Assert.True(target.Location is ServiceLocation or ScheduledTaskLocation, $"{tweak.Id} targets {target.Location}"));
        });
    }

    [Fact]
    public void Services_page_covers_every_listed_service_and_task()
    {
        var services = Services.SelectMany(tweak => tweak.Targets).Select(target => target.Location).OfType<ServiceLocation>().Select(location => location.ServiceName);
        var tasks = Services.SelectMany(tweak => tweak.Targets).Select(target => target.Location).OfType<ScheduledTaskLocation>();

        Assert.Equal(40, services.Distinct().Count());
        Assert.Equal(18, tasks.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(ServiceTweakIds))]
    public void Service_tweaks_disable_every_service_when_off_and_take_effect_after_restart(string id)
    {
        var tweak = Find(id);
        var off = tweak.Options.Single(option => option.Id == "off");

        Assert.All(tweak.Targets, target => Assert.Equal(new ServiceStartValue(ServiceStartType.Disabled), off.Values[target]));
        Assert.Equal(Activation.AfterRestart, tweak.Activation);
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
    [InlineData("services.print-spooler")]
    [InlineData("services.xbox-sign-in")]
    [InlineData("services.xbox-game-saves")]
    [InlineData("services.xbox-networking")]
    [InlineData("services.windows-search")]
    [InlineData("services.remote-desktop")]
    [InlineData("services.smart-cards")]
    [InlineData("services.biometrics")]
    [InlineData("services.touch-keyboard")]
    [InlineData("services.vpn")]
    public void Services_people_rely_on_have_no_recommended_option(string id)
    {
        Assert.Null(Find(id).Recommended);
    }

    [Theory]
    [InlineData("services.diagnostics-tracking")]
    [InlineData("services.compatibility-appraiser")]
    [InlineData("services.ceip-consolidator")]
    public void Background_telemetry_recommends_off(string id)
    {
        Assert.Equal("off", Find(id).Recommended?.Id);
    }

    [Theory]
    [InlineData("services.windows-search", "WSearch", ServiceStartType.AutomaticDelayed)]
    [InlineData("services.print-spooler", "Spooler", ServiceStartType.Automatic)]
    [InlineData("services.fax", "Fax", ServiceStartType.Manual)]
    public void On_puts_back_the_windows_default_start_type(string id, string service, ServiceStartType windowsDefault)
    {
        var tweak = Find(id);
        var target = tweak.Targets.Single(target => target.Location == new ServiceLocation(service));

        Assert.Equal(new ServiceStartValue(windowsDefault), tweak.Options.Single(option => option.Id == "on").Values[target]);
    }
}
