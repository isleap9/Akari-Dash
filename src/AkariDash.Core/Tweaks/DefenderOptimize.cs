using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// Akari-Dash's side of AkariOS-Ultimate's "Defender optimize" (Tweaks/Windows.ps1, the 'defender'
/// Tweak). It is the only Tweak written in safe mode: Windows lets Defender's own keys be written
/// only there, as TrustedInstaller, so <see cref="TweakEngine"/> hands the work to the companion exe
/// beside the app and restarts the PC into safe mode to do it.
///
/// Two parts of what AkariOS's Tweak does cannot be Tweak targets. Phishing protection's key
/// (WTDS\Components) may be read only by SYSTEM and TrustedInstaller, so the app cannot read it back
/// and the Tweak would always read Unavailable. The Exploit Guard policy task needs no read-back to
/// put it back. The companion writes both along with the Tweak's own targets.
///
/// Everything else AkariOS's Tweak does already has its own row on the Security page — memory
/// integrity, VBS, LSA protection, the vulnerable driver blocklist, SmartScreen, exploit protection,
/// the four Defender tasks and the Security Center and firewall notifications — and those parts of
/// it are written in a normal boot, so they stay there rather than being done twice.
/// </summary>
public static class DefenderOptimize
{
    /// <summary>The companion the app stages, published beside the app's own exe.</summary>
    public const string CompanionExecutable = "AkariDash.DefenderReset.exe";

    /// <summary>The RunOnce value the companion is registered under; the leading * runs it once.</summary>
    public const string RunOnceValueName = "*defenderoptimize";

    /// <summary>The Exploit Guard policy refresh task, which AkariOS's Optimize disables.</summary>
    private const string ExploitGuardTask = @"\Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh";

    /// <summary>Phishing protection, which only SYSTEM and TrustedInstaller may even read.</summary>
    private const string PhishingKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components";

    private static readonly string[] PhishingValues =
        ["CaptureThreatWindow", "NotifyMalicious", "NotifyPasswordReuse", "NotifyUnsafeApp", "ServiceEnabled"];

    /// <summary>The command RunOnce runs: the companion, told which pass to do.</summary>
    public static string RunOnceCommand(string companion, string passPath) =>
        $"\"{companion}\" --pass \"{passPath}\"";

    /// <summary>
    /// The targets the safe-mode pass writes, which is every one except those Akari-Dash writes in the
    /// session that asked: those are in that session's own profile, and safe mode may sign in as someone
    /// else, which would put them on the wrong profile.
    /// </summary>
    public static IReadOnlyList<TweakTarget> PassTargets(DeclaredTweak tweak) =>
        [.. tweak.Targets.Where(target => !target.WrittenInOwnSession)];

    /// <summary>
    /// What the companion writes besides the Tweak's own targets, for <paramref name="optimize"/>
    /// being the Optimize Option and <see langword="false"/> the Undo.
    /// </summary>
    public static IReadOnlyList<(MachineLocation Location, MachineValue? Value)> Extra(bool optimize) =>
    [
        .. PhishingValues.Select(name => (
            (MachineLocation)new RegistryLocation(RegistryHive.LocalMachine, PhishingKey, name),
            (MachineValue?)RegistryValue.DWord(optimize ? 0 : 1))),
        (new ScheduledTaskLocation(ExploitGuardTask),
            (MachineValue?)(optimize ? TaskEnabledValue.Off : TaskEnabledValue.On)),
    ];
}
