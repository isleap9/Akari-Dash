namespace AkariDash.Core.Machine;

/// <summary>The single seam through which all system access passes.</summary>
public interface IMachine
{
    /// <summary>Reads a registry value, or <see langword="null"/> when the key or value does not exist.</summary>
    RegistryValue? ReadRegistryValue(RegistryLocation location);
}
