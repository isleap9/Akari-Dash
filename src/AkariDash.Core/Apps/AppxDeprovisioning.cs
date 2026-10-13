namespace AkariDash.Core.Apps;

/// <summary>
/// Keeping a removed Store app removed. Removing a package for every user still leaves it provisioned,
/// so Windows installs it again for the next new account and on the next feature update. Microsoft's
/// way to stop that is to deprovision the package and leave a key named after its family under
/// AppxAllUserStore\Deprovisioned, which feature updates read before putting inbox apps back.
/// </summary>
public static class AppxDeprovisioning
{
    /// <summary>The HKLM key, relative to the hive, that marks a package family as deprovisioned.</summary>
    public static string DeprovisionedKey(string packageFamilyName)
    {
        if (string.IsNullOrWhiteSpace(packageFamilyName) || packageFamilyName.Contains('\\'))
        {
            throw new ArgumentException($"\"{packageFamilyName}\" is not a package family name.", nameof(packageFamilyName));
        }

        return $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned\{packageFamilyName}";
    }
}
