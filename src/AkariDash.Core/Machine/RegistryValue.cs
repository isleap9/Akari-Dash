using System.Collections;
using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>
/// The data held by a registry value.
/// Binary and multi-string data compare by content.
/// </summary>
public sealed record RegistryValue(RegistryValueKind Kind, object Data) : MachineValue
{
    public static RegistryValue DWord(int data) => new(RegistryValueKind.DWord, data);

    public static RegistryValue String(string data) => new(RegistryValueKind.String, data);

    public bool Equals(RegistryValue? other) =>
        other is not null &&
        Kind == other.Kind &&
        StructuralComparisons.StructuralEqualityComparer.Equals(Data, other.Data);

    public override int GetHashCode() =>
        HashCode.Combine(Kind, StructuralComparisons.StructuralEqualityComparer.GetHashCode(Data));

    public override string ToString() => Data switch
    {
        byte[] bytes => string.Join(" ", bytes.Select(b => b.ToString("X2"))),
        string[] strings => string.Join("; ", strings),
        _ => Data.ToString() ?? string.Empty,
    };
}
