using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// The Security page: the registry and boot-setting parts of Defender Optimize (issue #26), and the
/// settings Winhance keeps in the Security group of its Privacy &amp; Security page.
/// </summary>
public class SecurityCatalogTests
{
    private static readonly IReadOnlyList<DeclaredTweak> Security =
        TweakCatalog.All.Where(tweak => tweak.Category == Category.Security).ToList();

    private static DeclaredTweak Find(string id) => Security.Single(tweak => tweak.Id == id);

    [Fact]
    public void Security_page_has_its_groups_and_no_recommended_option()
    {
        Assert.Equal(
            ["Core isolation", "SmartScreen", "Exploit protection", "Microsoft Defender", "Notifications",
             "User Account Control", "Sign-in prompts", "Device encryption", "Wi-Fi", "Remote assistance"],
            Security.Select(tweak => tweak.Group).Distinct());
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

    // A real PC runs VBS and memory integrity with no hypervisor boot setting at all, so a missing
    // hypervisorlaunchtype has to read as the Auto it behaves as, not as Custom.
    [Fact]
    public void Vbs_on_a_machine_with_no_boot_settings_at_all_reads_as_letting_windows_decide()
    {
        var vbs = Find("security.vbs");

        var state = new TweakEngine(new InMemoryMachine()).ReadLiveState(vbs);

        Assert.Equal("windows-decides", Assert.IsType<LiveState.InOption>(state).Option.Id);
        Assert.Equal(new BootSettingValue("Auto"), vbs.Targets[1].AbsentMeans);
        Assert.All(vbs.Targets.Where((_, index) => index != 1), target => Assert.Null(target.AbsentMeans));
    }

    // Windows Security's "Reset all to default" writes its defaults out as an explicit all-enabled
    // mask instead of deleting the value, so both it and a missing value read as Windows' default.
    [Fact]
    public void Exploit_protection_reads_windows_default_whether_the_mask_is_absent_or_written_out()
    {
        var mitigations = Find("security.exploit-protection").Targets.Single();
        var written = new InMemoryMachine().With(mitigations.Location, mitigations.AbsentMeans!);

        var absentState = new TweakEngine(new InMemoryMachine()).ReadLiveState(Find("security.exploit-protection"));
        var writtenState = new TweakEngine(written).ReadLiveState(Find("security.exploit-protection"));

        Assert.Equal("windows-default", Assert.IsType<LiveState.InOption>(absentState).Option.Id);
        Assert.Equal("windows-default", Assert.IsType<LiveState.InOption>(writtenState).Option.Id);
        Assert.Equal(
            new RegistryValue(Microsoft.Win32.RegistryValueKind.Binary, Convert.FromHexString("111111000001000000000000000000000000000000000000")),
            mitigations.AbsentMeans);
    }

    // A mask the user built by hand is neither Option, and saying so is the point of Custom.
    [Fact]
    public void Exploit_protection_reads_custom_for_a_hand_built_mask()
    {
        var tweak = Find("security.exploit-protection");
        var machine = new InMemoryMachine().With(
            tweak.Targets.Single().Location,
            new RegistryValue(Microsoft.Win32.RegistryValueKind.Binary, Convert.FromHexString("121212000001000000000000000000000000000000000000")));

        var state = new TweakEngine(machine).ReadLiveState(tweak);

        Assert.IsType<LiveState.Custom>(state);
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
