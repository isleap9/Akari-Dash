namespace AkariDash.Core.Apps;

/// <summary>
/// The winget command lines the Downloads page runs. Kept here beside the catalogue so the Phase 1
/// build can show exactly what a real install would have run without running it.
/// </summary>
public static class WingetCommand
{
    public static IReadOnlyList<string> Install(InstallableApp app) =>
    [
        "install", "--id", app.WingetId, "--source", "winget", "--exact", "--silent",
        "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity",
    ];

    public static IReadOnlyList<string> List() =>
    [
        "list", "--accept-source-agreements", "--disable-interactivity",
    ];

    public static string Describe(IReadOnlyList<string> arguments) => "winget " + string.Join(' ', arguments);

    public static string DescribeInstall(InstallableApp app) => Describe(Install(app));
}
