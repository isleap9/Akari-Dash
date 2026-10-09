namespace AkariDash.Core.Machine;

/// <summary>The single seam through which all system access passes.</summary>
public interface IMachine
{
    /// <summary>Reads a registry value, or <see langword="null"/> when the key or value does not exist.</summary>
    RegistryValue? ReadRegistryValue(RegistryLocation location);

    /// <summary>Creates or overwrites a registry value, creating its key if needed.</summary>
    void WriteRegistryValue(RegistryLocation location, RegistryValue value);

    /// <summary>Deletes a registry value; deleting one that does not exist does nothing.</summary>
    void DeleteRegistryValue(RegistryLocation location);
}
