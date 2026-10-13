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
/// <param name="BoardMaker">The motherboard's manufacturer, as the firmware names it.</param>
/// <param name="BoardModel">The motherboard's model, as the firmware names it.</param>
/// <param name="Disks">Every physical disk, with its size.</param>
/// <param name="Displays">What each graphics card is drawing now: resolution and refresh rate.</param>
public sealed record PcFacts(
    string? ProductName,
    string? DisplayVersion,
    int? Build,
    int? Revision,
    string? Cpu,
    IReadOnlyList<string> Gpus,
    ulong? InstalledRamKilobytes,
    string? RunningAs,
    string? SignedIn,
    string? BoardMaker = null,
    string? BoardModel = null,
    IReadOnlyList<DiskFact>? Disks = null,
    IReadOnlyList<DisplayFact>? Displays = null)
{
    /// <summary>A PC Windows could report nothing about.</summary>
    public static PcFacts Unknown { get; } = new(null, null, null, null, null, [], null, null, null);
}

/// <summary>A physical disk: its model name and its size in bytes.</summary>
public sealed record DiskFact(string Model, ulong? Bytes);

/// <summary>A display mode a graphics card is drawing: width and height in pixels, and refresh rate in Hz.</summary>
public sealed record DisplayFact(int Width, int Height, int? RefreshRate);
