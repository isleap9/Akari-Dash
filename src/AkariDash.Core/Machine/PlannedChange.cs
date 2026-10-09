namespace AkariDash.Core.Machine;

/// <summary>
/// One change a Dry Run would have made: <paramref name="Before"/> → <paramref name="After"/>,
/// where <see langword="null"/> means the value does not exist.
/// </summary>
public sealed record PlannedChange(RegistryLocation Location, RegistryValue? Before, RegistryValue? After)
{
    public bool IsCreate => Before is null;

    public bool IsDelete => After is null;

    /// <summary>e.g. <c>HKCU\Software\Key\Name: 1 → 0</c>, with creates and deletes called out.</summary>
    public override string ToString()
    {
        var suffix = IsCreate ? " (created)" : IsDelete ? " (deleted)" : string.Empty;
        return $"{Location}: {RegistryValue.Describe(Before)} → {RegistryValue.Describe(After)}{suffix}";
    }
}
