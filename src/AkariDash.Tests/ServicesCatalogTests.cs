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

    private static DeclaredTweak Find(string id) => Services.Single(tweak => tweak.Id == id);

    [Fact]
    public void Services_page_holds_only_service_tweaks()
    {
        Assert.All(Services, tweak =>
        {
            Assert.StartsWith("services.", tweak.Id);
            Assert.All(tweak.Targets, target => Assert.IsType<ServiceLocation>(target.Location));
        });
    }

    [Fact]
    public void Services_page_covers_every_listed_service()
    {
        var services = Services.SelectMany(tweak => tweak.Targets).Select(target => target.Location).OfType<ServiceLocation>().Select(location => location.ServiceName);

        Assert.Equal(42, services.Distinct().Count());
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
    [InlineData("services.sysmain")]
    public void Background_work_with_a_performance_case_recommends_off(string id)
    {
        Assert.Equal("off", Find(id).Recommended?.Id);
    }

    [Theory]
    [MemberData(nameof(ServiceTweakIds))]
    public void Every_service_tweak_offers_the_start_types_the_services_snap_in_does(string id)
    {
        var tweak = Find(id);

        // Off and On are always there; a start type every service already starts as is left out,
        // because On is that same machine state and two Options may not describe one state.
        Assert.Equal("off", tweak.Options[0].Id);
        Assert.Equal("on", tweak.Options[^1].Id);
        Assert.All(tweak.Options, option => Assert.All(tweak.Targets, target => Assert.IsType<ServiceStartValue>(option.Values[target])));

        var starts = new Dictionary<string, ServiceStartType>
        {
            ["manual"] = ServiceStartType.Manual,
            ["automatic"] = ServiceStartType.Automatic,
            ["delayed"] = ServiceStartType.AutomaticDelayed,
        };
        foreach (var (optionId, start) in starts)
        {
            var defaults = tweak.Targets.Select(target => ((ServiceStartValue)tweak.Options[^1].Values[target]!).StartType);
            var option = tweak.Options.SingleOrDefault(option => option.Id == optionId);
            if (defaults.All(windowsDefault => windowsDefault == start))
            {
                Assert.Null(option);
                continue;
            }

            Assert.All(tweak.Targets, target => Assert.Equal(new ServiceStartValue(start), option?.Values[target]));
        }
    }

    [Fact]
    public void Manual_is_offered_where_windows_starts_the_service_by_itself()
    {
        // SysMain starts Automatic, so Manual is a real third answer: there, but not at boot.
        var sysmain = Find("services.sysmain");

        Assert.Equal(["off", "manual", "delayed", "on"], sysmain.Options.Select(option => option.Id));
        Assert.Equal("Manual", sysmain.Options[1].Label);
        Assert.Equal("Automatic (delayed start)", sysmain.Options[2].Label);
    }

    [Fact]
    public void A_service_that_already_starts_manual_does_not_offer_manual_twice()
    {
        // Every Remote Desktop service starts Manual, which is what On puts back.
        Assert.Equal(["off", "automatic", "delayed", "on"], Find("services.remote-desktop").Options.Select(option => option.Id));
    }

    [Fact]
    public void A_tweak_whose_services_start_differently_offers_all_of_them()
    {
        // CDPSvc starts delayed and CDPUserSvc automatic, so no single start type is the default.
        Assert.Equal(["off", "manual", "automatic", "delayed", "on"], Find("services.connected-devices").Options.Select(option => option.Id));
    }

    [Theory]
    [InlineData("services.windows-search", "WSearch", ServiceStartType.AutomaticDelayed)]
    [InlineData("services.print-spooler", "Spooler", ServiceStartType.Automatic)]
    [InlineData("services.fax", "Fax", ServiceStartType.Manual)]
    [InlineData("services.sysmain", "SysMain", ServiceStartType.Automatic)]
    public void On_puts_back_the_windows_default_start_type(string id, string service, ServiceStartType windowsDefault)
    {
        var tweak = Find(id);
        var target = tweak.Targets.Single(target => target.Location == new ServiceLocation(service));

        Assert.Equal(new ServiceStartValue(windowsDefault), tweak.Options.Single(option => option.Id == "on").Values[target]);
    }
}
