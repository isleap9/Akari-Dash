namespace AkariDash.Core.Apps;

/// <summary>Turns what Windows stores about an app into what the Uninstaller shows.</summary>
public static class InstalledAppText
{
    /// <summary>
    /// The name to show for a Store package. Its display name, unless Windows could not resolve it —
    /// empty, or still an <c>ms-resource:</c> reference — in which case the package name without its
    /// publisher prefix: <c>Microsoft.WindowsCalculator</c> reads as <c>WindowsCalculator</c>.
    /// </summary>
    public static string PackageName(string? displayName, string packageName)
    {
        if (!string.IsNullOrWhiteSpace(displayName) && !displayName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
        {
            return displayName.Trim();
        }

        var dot = packageName.IndexOf('.');
        return dot > 0 && dot < packageName.Length - 1 ? packageName[(dot + 1)..] : packageName;
    }

    /// <summary>
    /// The file a Win32 program's <c>DisplayIcon</c> points at, or <see langword="null"/> when it names
    /// none. The value is a path, optionally quoted, optionally followed by <c>,index</c> for which icon
    /// inside the file: <c>"C:\Program Files\App\app.exe",0</c> is <c>C:\Program Files\App\app.exe</c>.
    /// </summary>
    public static string? IconPath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var path = displayIcon.Trim();

        // ",0" or ",-101" after the path picks an icon inside the file; the comma cannot be part of a
        // file name that ends in an extension, so cut from the last comma when what follows is a number.
        var comma = path.LastIndexOf(',');
        if (comma > 0 && int.TryParse(path[(comma + 1)..].Trim(), out _))
        {
            path = path[..comma].Trim();
        }

        path = path.Trim('"').Trim();
        return path.Length == 0 ? null : Environment.ExpandEnvironmentVariables(path);
    }
}
