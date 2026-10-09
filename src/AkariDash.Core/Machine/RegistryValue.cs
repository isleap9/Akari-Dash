using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>
/// The data held by a registry value. A value that does not exist is represented by
/// <see langword="null"/> wherever a <see cref="RegistryValue"/> is expected.
/// </summary>
public sealed record RegistryValue(RegistryValueKind Kind, object Data)
{
    public static RegistryValue DWord(int data) => new(RegistryValueKind.DWord, data);

    public static RegistryValue String(string data) => new(RegistryValueKind.String, data);

    public override string ToString() => Data.ToString() ?? string.Empty;
}
