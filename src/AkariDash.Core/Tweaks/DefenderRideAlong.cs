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
    /// the side of the pass that turns Defender and the two ride-alongs off: the values below are the
    /// off/0 side when it is <see langword="true"/> and the on/1 side when it is <see langword="false"/>.
    /// </summary>
    public static IReadOnlyList<(MachineLocation Location, MachineValue? Value)> Defender(bool disable) =>
    [
        .. PhishingValues.Select(name => (
            (MachineLocation)new RegistryLocation(RegistryHive.LocalMachine, PhishingKey, name),
            (MachineValue?)RegistryValue.DWord(disable ? 0 : 1))),
        (new ScheduledTaskLocation(ExploitGuardTask),
            (MachineValue?)(disable ? TaskEnabledValue.Off : TaskEnabledValue.On)),
    ];

    /// <summary>
    /// What the companion writes for <paramref name="pass"/> besides the Tweak's own targets. Which
    /// side of its Tweak a pass rides is the Tweak's own, because the Undo flag alone does not say it:
    /// Defender optimize's Optimize pass is its off side and its Undo the on side; Disable Defender's
    /// Off apply is its off side, and its On apply and every Undo are the on side.
    /// </summary>
    public static IReadOnlyList<(MachineLocation Location, MachineValue? Value)> For(SafeModePass pass) =>
        Defender(TurnsOff(pass));

    /// <summary>
    /// Whether <paramref name="pass"/> is the side of its Tweak that turns Defender off, and so rides
    /// the off/0 values <see cref="Defender"/> takes.
    ///
    /// Only an apply of Disable Defender's Off Option is that side. A pass's
    /// <see cref="SafeModePass.OptionId"/> is the Option that was applied last, but its Undo puts back
    /// the Original Values the <em>first</em> apply saved — Defender back on — so the Undo rides the
    /// enable side whichever Option was applied last.
    /// </summary>
    public static bool TurnsOff(SafeModePass pass)
    {
        if (pass.TweakId != DisableDefender.TweakId)
        {
            // Defender optimize, unchanged: the Optimize pass is the off side, its Undo the on side.
            return !pass.Undo;
        }

        return !pass.Undo && pass.OptionId == "off";
    }
}
