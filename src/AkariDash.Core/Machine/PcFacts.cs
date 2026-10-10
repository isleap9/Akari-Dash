namespace AkariDash.Core.Machine;

/// <summary>
/// What Windows reports about this PC, as raw as it reports it; anything it cannot report is
/// <see langword="null"/> (or, for the graphics cards, empty).
/// </summary>
/// <param name="ProductName">Windows' own product name, e.g. <c>Windows 10 Pro</c> (Windows 11 reports this too).</param>
/// <param name="DisplayVersion">The feature update, e.g. <c>24H2</c>.</param>
/// <param name="Build">The OS build number, e.g. <c>26100</c>.</param>
/// <param name="Revision">The update revision of the build, e.g. <c>2033</c>.</param>
/// <param name="Cpu">The processor's name.</param>
/// <param name="Gpus">The name of every display adapter Windows has a driver for.</param>
/// <param name="InstalledRamKilobytes">The physically installed memory.</param>
/// <param name="RunningAs">The account Akari-Dash is running as (<c>DOMAIN\user</c>).</param>
/// <param name="SignedIn">The user signed in to the session Akari-Dash runs in (<c>DOMAIN\user</c>).</param>
public sealed record PcFacts(
    string? ProductName,
    string? DisplayVersion,
    int? Build,
    int? Revision,
    string? Cpu,
    IReadOnlyList<string> Gpus,
    ulong? InstalledRamKilobytes,
    string? RunningAs,
    string? SignedIn)
{
    /// <summary>A PC Windows could report nothing about.</summary>
    public static PcFacts Unknown { get; } = new(null, null, null, null, null, [], null, null, null);
}
