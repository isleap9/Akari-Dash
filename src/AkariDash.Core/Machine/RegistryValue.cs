using System.Collections;
using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>
/// The data held by a registry value. A value that does not exist is represented by
/// <see langword="null"/> wherever a <see cref="RegistryValue"/> is expected.
/// Binary and multi-string data compare by content.
/// </summary>
public sealed record RegistryValue(RegistryValueKind Kind, object Data)
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

    /// <summary>Text for a value that may not exist.</summary>
    public static string Describe(RegistryValue? value) => value?.ToString() ?? "(not set)";
}
