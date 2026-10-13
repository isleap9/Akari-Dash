namespace AkariDash.Core.Apps;

/// <summary>Reads what dism.exe prints with /English /Format:Table, and what its exit codes mean.</summary>
public static class DismOutput
{
    /// <summary>DISM's "the operation completed, restart to finish" (ERROR_SUCCESS_REBOOT_REQUIRED).</summary>
    public const int RestartRequired = 3010;

    /// <summary>
    /// The rows of a /Get-Capabilities table, "Name | State" under a dashed rule. A capability is on when
    /// it is Installed; Not Present, Staged and the rest are off. The display name is the identity
    /// without its "~~~~version" tail, which is how Settings shortens it too.
    /// </summary>
    public static IReadOnlyList<WindowsFeature> Capabilities(string output)
    {
        List<WindowsFeature> capabilities = [];
        var inTable = false;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("---", StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }

            if (!inTable || line.Length == 0 || !line.Contains('|'))
            {
                continue;
            }

            var cells = line.Split('|', 2, StringSplitOptions.TrimEntries);
            var name = cells[0];

            // The column header sits between two dashed rules.
            if (name.Equals("Capability Identity", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tilde = name.IndexOf('~');
            capabilities.Add(new WindowsFeature(
                name,
                tilde > 0 ? name[..tilde] : name,
                WindowsFeatureKind.Capability,
                string.Equals(cells[1], "Installed", StringComparison.OrdinalIgnoreCase)));
        }

        return capabilities;
    }
}
