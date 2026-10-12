using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>
/// One bit (<paramref name="Mask"/> of byte <paramref name="ByteIndex"/>) of a binary registry value,
/// such as an effect in <c>HKCU\Control Panel\Desktop\UserPreferencesMask</c>. Reading and writing it
/// leave every other bit of the value as it is, so Undo puts back only this bit. The value itself
/// must already exist: a missing or too-short value makes the Tweak Unavailable, like a missing service.
/// </summary>
public sealed record RegistryBitLocation(RegistryHive Hive, string Key, string Name, int ByteIndex, byte Mask) : MachineLocation
{
    /// <summary>The whole registry value this bit is part of.</summary>
    public RegistryLocation Value => new(Hive, Key, Name);

    /// <summary>The bit in <paramref name="whole"/>, or <see langword="null"/> when the value is missing, not binary or too short.</summary>
    public BitValue? ReadFrom(MachineValue? whole) =>
        whole is RegistryValue { Kind: RegistryValueKind.Binary, Data: byte[] bytes } && ByteIndex < bytes.Length
            ? new BitValue((bytes[ByteIndex] & Mask) != 0)
            : null;

    /// <summary><paramref name="whole"/> with this bit set or cleared and every other bit unchanged.</summary>
    /// <exception cref="InvalidOperationException">The value is missing, not binary or too short.</exception>
    public RegistryValue WriteInto(MachineValue? whole, bool set)
    {
        if (whole is not RegistryValue { Kind: RegistryValueKind.Binary, Data: byte[] bytes } || ByteIndex >= bytes.Length)
        {
            throw new InvalidOperationException($"{Value} does not exist or is too short to hold byte {ByteIndex}.");
        }

        var copy = (byte[])bytes.Clone();
        copy[ByteIndex] = (byte)(set ? copy[ByteIndex] | Mask : copy[ByteIndex] & ~Mask);
        return new RegistryValue(RegistryValueKind.Binary, copy);
    }

    public override string ToString() => $"{Value} (byte {ByteIndex}, bit 0x{Mask:X2})";
}
