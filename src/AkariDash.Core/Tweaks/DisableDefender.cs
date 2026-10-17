namespace AkariDash.Core.Tweaks;

/// <summary>
/// Akari-Dash's side of AkariOS-Ultimate's Advanced 'Defender' Tweak (Tweaks/Advanced.ps1:3-435, MIT):
/// the whole of it, turning Windows Defender off and back on. It rides the same safe-mode pass as
/// <see cref="DefenderOptimize"/> — Windows lets Defender's own keys, the kernel and DeviceGuard keys
/// and Defender's services be written only in safe boot, as TrustedInstaller — but it goes further:
/// the antivirus itself, its eight services and its three drivers are switched off, not only its
/// protections.
///
/// The row itself lives in <c>TweakCatalog.Security.cs</c> beside <c>DefenderOptimizeTweak()</c>; this
/// file is the Tweak's identity, which the companion uses to tell its passes apart.
/// </summary>
public static class DisableDefender
{
    /// <summary>The row's id, <c>security.disable-defender</c>.</summary>
    public const string TweakId = "security.disable-defender";
}
