using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>
/// The Copilot &amp; AI Group. #34 listed what Winhance has here that we do not, from a Shield-era
/// checkout, which ADR-0003 allows only for learning that a setting exists — so each of these had to
/// find its value somewhere else, and these tests say where each one came from.
/// </summary>
public class PrivacyAITests
{
    private static readonly IReadOnlyList<DeclaredTweak> CopilotAndAI =
        TweakCatalog.All.Where(tweak => tweak.Group == "Copilot & AI").ToList();

    private static DeclaredTweak Find(string id) => CopilotAndAI.Single(tweak => tweak.Id == id);

    private static RegistryLocation Only(string id) => (RegistryLocation)Assert.Single(Find(id).Targets).Location;

    [Fact]
    public void The_group_is_on_the_privacy_page_and_writes_only_registry_values()
    {
        Assert.Equal(8, CopilotAndAI.Count);
        Assert.All(CopilotAndAI, tweak =>
        {
            Assert.Equal(Category.Privacy, tweak.Category);
            Assert.StartsWith("privacy.", tweak.Id);
            Assert.All(tweak.Targets, target => Assert.IsType<RegistryLocation>(target.Location));
        });
    }

    [Fact]
    public void Every_row_offers_to_delete_its_value_again_so_windows_decides()
    {
        // A policy Tweak always has a way back that is not "write the other number": deleting the
        // value hands the choice to Windows, which is what "not configured" means.
        Assert.All(CopilotAndAI, tweak =>
        {
            var handBack = tweak.Options[1];
            Assert.All(tweak.Targets, target => Assert.Null(handBack.Values[target]));
        });
    }

    [Theory]
    [InlineData("privacy.copilot-browsing", "BrowsingEnabled")]
    [InlineData("privacy.copilot-cowork-actions", "CopilotCoworkToolActionsEnabled")]
    public void The_copilot_apps_own_policies_are_enabled_policies_so_off_writes_zero(string id, string name)
    {
        // Microsoft's "Configure Microsoft Copilot policies": SOFTWARE\Policies\Microsoft\Copilot,
        // REG_DWORD, and they are named "...Enabled", so Off is 0 and not 1 like the Disable... ones.
        var location = Only(id);

        Assert.Equal(RegistryHive.LocalMachine, location.Hive);
        Assert.Equal(@"SOFTWARE\Policies\Microsoft\Copilot", location.Key);
        Assert.Equal(name, location.Name);

        var off = Find(id).Options[0];
        Assert.Equal(RegistryValue.DWord(0), off.Values[Find(id).Targets[0]]);
    }

    [Fact]
    public void The_copilot_app_key_is_not_the_copilot_pane_key()
    {
        // Two different things with similar names: privacy.copilot turns off the Copilot pane built
        // into Windows (WindowsCopilot), these two configure the separate Copilot app (Copilot).
        var pane = Find("privacy.copilot").Targets.Select(target => ((RegistryLocation)target.Location).Key);

        Assert.All(pane, key => Assert.EndsWith(@"Windows\WindowsCopilot", key));
        Assert.DoesNotContain(@"SOFTWARE\Policies\Microsoft\Copilot", pane);
    }

    [Fact]
    public void Bing_chat_eligibility_writes_the_value_akari_os_writes()
    {
        // Akari-OS reg.reg (MIT), which is a source ADR-0003 allows. Microsoft does not document it.
        var location = Only("privacy.bing-chat-eligibility");

        Assert.Equal(RegistryHive.LocalMachine, location.Hive);
        Assert.Equal(@"Software\Microsoft\Windows\Shell\Copilot\BingChat", location.Key);
        Assert.Equal("IsUserEligible", location.Name);
        Assert.Equal(RegistryValue.DWord(0), Find("privacy.bing-chat-eligibility").Options[0].Values[Find("privacy.bing-chat-eligibility").Targets[0]]);
    }

    [Fact]
    public void Paint_writes_the_three_policies_microsoft_documents_and_no_more()
    {
        // #34 asked for generative erase and remove background too. Microsoft's WindowsAI CSP
        // documents three Paint policies and no others, and the Shield-era tree may not be read for
        // the missing two, so AIInPaint stays at three.
        Assert.Equal(
            ["DisableCocreator", "DisableImageCreator", "DisableGenerativeFill"],
            Find("privacy.ai-in-paint").Targets.Select(target => ((RegistryLocation)target.Location).Name));
    }

    [Theory]
    [InlineData("DisableSettingsAgent")]
    [InlineData("ConfigureAgentConnectors")]
    [InlineData("AgentConsentDuration")]
    [InlineData("DisableRecallDataProviders")]
    [InlineData("AllowRecallExport")]
    [InlineData("RemoveMicrosoftCopilotApp")]
    [InlineData("AllowRecallEnablement")]
    [InlineData("SetDenyAppListForRecall")]
    [InlineData("SetDenyUriListForRecall")]
    [InlineData("SetCopilotHardwareKey")]
    [InlineData("ShowCopilotButton")]
    public void What_the_file_says_it_leaves_out_really_is_left_out(string name)
    {
        // Each of these has its reason in TweakCatalog.Privacy.cs above Copilot(): Insider-only,
        // not Pro, not undoable, Enterprise-only, or another page's job.
        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets).Select(target => target.Location).OfType<RegistryLocation>(),
            location => location.Name == name);
    }

    private static readonly IReadOnlyList<DeclaredTweak> EdgeAI =
        TweakCatalog.All.Where(tweak => tweak.Group == "Microsoft Edge AI").ToList();

    [Fact]
    public void Every_edge_row_is_one_documented_policy_under_edges_machine_policy_key()
    {
        // Microsoft publishes the same registry path for all of them, so a row writing anywhere else
        // would be a value somebody made up.
        Assert.Equal(5, EdgeAI.Count);
        Assert.All(EdgeAI, tweak =>
        {
            Assert.Equal(Category.Privacy, tweak.Category);
            var location = (RegistryLocation)Assert.Single(tweak.Targets).Location;
            Assert.Equal(RegistryHive.LocalMachine, location.Hive);
            Assert.Equal(@"SOFTWARE\Policies\Microsoft\Edge", location.Key);
        });
    }

    [Theory]
    [InlineData("privacy.edge-copilot-page-context", "CopilotPageContext", 0)]
    [InlineData("privacy.edge-history-ai-search", "EdgeHistoryAISearchEnabled", 0)]
    [InlineData("privacy.edge-builtin-ai-apis", "BuiltInAIAPIsEnabled", 0)]
    [InlineData("privacy.edge-ai-themes", "AIGenThemesEnabled", 0)]
    // The one enum in the group: Microsoft documents 1 as Disallowed, not as "enabled = true".
    [InlineData("privacy.edge-local-ai-model", "GenAILocalFoundationalModelSettings", 1)]
    public void An_edge_row_writes_the_value_microsoft_documents_for_it(string id, string name, int off)
    {
        var tweak = EdgeAI.Single(candidate => candidate.Id == id);
        var target = Assert.Single(tweak.Targets);

        Assert.Equal(name, ((RegistryLocation)target.Location).Name);
        Assert.Equal(RegistryValue.DWord(off), tweak.Options[0].Values[target]);
        Assert.Null(tweak.Options[1].Values[target]);
    }

    [Fact]
    public void No_edge_row_is_unavailable_without_edge()
    {
        // #35: the policy is writable whether or not Edge is installed, and takes effect when Edge
        // arrives. Hiding the rows on a PC without Edge would be worse than saying so in the words.
        Assert.All(EdgeAI, tweak => Assert.Null(tweak.RequiresGpu));
        Assert.All(EdgeAI, tweak => Assert.Contains("Edge", tweak.Description));
    }

    [Theory]
    [InlineData("EdgeCopilotEnabled")]
    [InlineData("CopilotCDPPageContext")]
    [InlineData("EdgeEntraCopilotPageContext")]
    [InlineData("Microsoft365CopilotChatIconEnabled")]
    [InlineData("ComposeInlineEnabled")]
    public void The_edge_policies_that_do_nothing_here_are_left_out(string name)
    {
        // Reasons above EdgeAITweaks(): not supported on Windows, obsolete, or scoped to Entra work
        // profiles and Edge for Business.
        Assert.DoesNotContain(
            TweakCatalog.All.SelectMany(tweak => tweak.Targets).Select(target => target.Location).OfType<RegistryLocation>(),
            location => location.Name == name);
    }
}
