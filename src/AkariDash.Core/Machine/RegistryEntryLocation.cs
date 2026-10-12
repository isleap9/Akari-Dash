using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>
/// One <c>name=value;</c> entry (<paramref name="Entry"/>) of a registry string that holds several, such
/// as <c>DirectXUserGlobalSettings</c> (<c>SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;</c>). Reading
/// and writing it leave the other entries as they are, so Undo puts back only this one. A missing entry
/// is normal, like a missing registry value; deleting the last entry deletes the whole value.
/// </summary>
public sealed record RegistryEntryLocation(RegistryHive Hive, string Key, string Name, string Entry) : MachineLocation
{
    /// <summary>The whole registry value this entry is part of.</summary>
    public RegistryLocation Value => new(Hive, Key, Name);

    /// <summary>The entry in <paramref name="whole"/>, or <see langword="null"/> when it or the value is missing.</summary>
    public EntryValue? ReadFrom(MachineValue? whole) =>
        Parse(whole).FirstOrDefault(pair => pair.Name.Equals(Entry, StringComparison.OrdinalIgnoreCase)) is { Name: not null } found
            ? new EntryValue(found.Value)
            : null;

    /// <summary>
    /// <paramref name="whole"/> with this entry set to <paramref name="text"/> (or removed when it is
    /// <see langword="null"/>), every other entry unchanged and in order; <see langword="null"/> when no
    /// entry is left, so the value can be deleted.
    /// </summary>
    public RegistryValue? WriteInto(MachineValue? whole, string? text)
    {
        var pairs = Parse(whole).ToList();
        var index = pairs.FindIndex(pair => pair.Name.Equals(Entry, StringComparison.OrdinalIgnoreCase));

        if (text is null)
        {
            if (index >= 0)
            {
                pairs.RemoveAt(index);
            }
        }
        else if (index >= 0)
        {
            pairs[index] = (pairs[index].Name, text);
        }
        else
        {
            pairs.Add((Entry, text));
        }

        return pairs.Count == 0
            ? null
            : RegistryValue.String(string.Concat(pairs.Select(pair => $"{pair.Name}={pair.Value};")));
    }

    public override string ToString() => $"{Value} ({Entry})";

    private static IEnumerable<(string Name, string Value)> Parse(MachineValue? whole) =>
        whole is RegistryValue { Data: string text }
            ? text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Split('=', 2))
                .Select(parts => (parts[0].Trim(), parts.Length == 2 ? parts[1].Trim() : string.Empty))
            : [];
}

/// <summary>What a <see cref="RegistryEntryLocation"/> holds: the text after <c>name=</c>.</summary>
public sealed record EntryValue(string Text) : MachineValue
{
    public override string ToString() => Text;
}
