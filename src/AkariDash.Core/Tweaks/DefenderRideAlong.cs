using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// The two things every Defender safe-mode Tweak writes besides its own targets: phishing protection's
/// key (WTDS\Components), which may be read only by SYSTEM and TrustedInstaller so the app cannot read
/// it back and the Tweak would always read Unavailable, and the Exploit Guard policy task, which needs
/// no read-back to put it back.
///
/// The values are the same whichever safe-mode Tweak is running — Defender optimize and Disable
/// Defender ride the same two — so they are written here once and asked for by each.
/// </summary>
public static class DefenderRideAlong
{
    /// <summary>The Exploit Guard policy refresh task, which AkariOS's Optimize disables.</summary>
    private const string ExploitGuardTask = @"\Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh";

    /// <summary>Phishing protection, which only SYSTEM and TrustedInstaller may even read.</summary>
    private const string PhishingKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components";

    private static readonly string[] PhishingValues =
        ["CaptureThreatWindow", "NotifyMalicious", "NotifyPasswordReuse", "NotifyUnsafeApp", "ServiceEnabled"];

    /// <summary>
    /// What the companion writes besides the Tweak's own targets, for <paramref name="disable"/> being
    /// the Optimize and <see langword="false"/> the Undo.
    /// </summary>
    public static IReadOnlyList<(MachineLocation Location, MachineValue? Value)> Defender(bool disable) =>
    [
        .. PhishingValues.Select(name => (
            (MachineLocation)new RegistryLocation(RegistryHive.LocalMachine, PhishingKey, name),
            (MachineValue?)RegistryValue.DWord(disable ? 0 : 1))),
        (new ScheduledTaskLocation(ExploitGuardTask),
            (MachineValue?)(disable ? TaskEnabledValue.Off : TaskEnabledValue.On)),
    ];
}
