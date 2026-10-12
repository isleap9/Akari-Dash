using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

/// <summary>The Security page: the registry and boot-setting parts of Defender Optimize (issue #26).</summary>
public class SecurityCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Security =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Security).ToList();

    private static DeclaredTweak Find(string id) => Security.Single(tweak => tweak.Id == id);

    [Fact]
    public void Security_page_has_its_groups_and_no_recommended_option()
    {
        Assert.Equal(["Core isolation", "SmartScreen", "Exploit protection", "Microsoft Defender", "Notifications"], Security.Select(tweak => tweak.Group).Distinct());
        Assert.All(Security, tweak =>
        {
            Assert.StartsWith("security.", tweak.Id);
            Assert.Null(tweak.Recommended);
        });
    }

    [Fact]
    public void Vbs_off_sets_the_policy_to_zero_and_deletes_the_hypervisor_boot_settings()
    {
        var vbs = Find("security.vbs");
        var off = vbs.Options.Single(option => option.Id == "off");

        Assert.Equal(RegistryValue.DWord(0), off.Values[vbs.Targets[0]]);
        Assert.Equal(
            ["hypervisorlaunchtype", "isolatedcontext", "allowedinmemorysettings"],
            vbs.Targets.Skip(1).Select(target => ((BootSettingLocation)target.Location).Name));
        Assert.All(vbs.Targets.Skip(1), target => Assert.Null(off.Values[target]));
    }

    [Fact]
    public void Memory_integrity_off_and_on_match_defender_optimize_and_its_default()
    {
        var tweak = Find("security.memory-integrity");
        var off = tweak.Options.Single(option => option.Id == "off");
        var on = tweak.Options.Single(option => option.Id == "on");

        Assert.Equal([RegistryValue.DWord(0), null, null], tweak.Targets.Select(target => off.Values[target]));
        Assert.Equal([RegistryValue.DWord(1), RegistryValue.DWord(2), null], tweak.Targets.Select(target => on.Values[target]));
    }

    [Fact]
    public void Defender_itself_and_smart_app_control_are_not_on_the_security_page()
    {
        var keys = Security.SelectMany(tweak => tweak.Targets).Select(target => target.Location).OfType<RegistryLocation>().Select(location => location.Key);

        Assert.DoesNotContain(keys, key => key.Contains(@"Windows Defender\Real-Time Protection") || key.Contains("SMARTLOCKER"));
    }

    [Fact]
    public void Defender_tasks_are_the_four_defender_optimize_disables()
    {
        var tasks = Find("security.defender-tasks").Targets.Select(target => ((ScheduledTaskLocation)target.Location).Path);

        Assert.Equal(
            [
                @"\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance",
                @"\Microsoft\Windows\Windows Defender\Windows Defender Cleanup",
                @"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan",
                @"\Microsoft\Windows\Windows Defender\Windows Defender Verification",
            ],
            tasks);
    }
}
