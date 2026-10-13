using Microsoft.Win32;

namespace AkariDash.Core.Machine;

/// <summary>
/// Whether a registry key exists at all, for the settings Windows reads from a key being there rather
/// than from a value it holds — the Windows 10 right-click menu is a CLSID key whose presence alone
/// overrides the Windows 11 one. Writing <see cref="KeyPresence.Present"/> creates the key; deleting
/// removes it and everything under it, so an Original Value of "did not exist" is undone by taking
/// the whole key away again, not just the value inside it.
/// </summary>
public sealed record RegistryKeyLocation : MachineLocation
{
    public RegistryKeyLocation(RegistryHive hive, string key)
    {
        // Deleting a key deletes its whole tree, so a catalog typo must not be able to name a key near
        // the root. Every key this is for sits at least three levels down (Software\Classes\CLSID\{...}).
        if (key.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length < 3)
        {
            throw new ArgumentException($"{key} is too near the root of the registry for Akari-Dash to delete.", nameof(key));
        }

        Hive = hive;
        Key = key;
    }

    public RegistryHive Hive { get; }

    public string Key { get; }

    public override string ToString() => $@"{(Hive == RegistryHive.CurrentUser ? "HKCU" : Hive == RegistryHive.LocalMachine ? "HKLM" : Hive.ToString())}\{Key} (key)";
}

/// <summary>What a <see cref="RegistryKeyLocation"/> holds when the key exists. A missing key is <see langword="null"/>, like a missing value.</summary>
public sealed record KeyPresence : MachineValue
{
    public static KeyPresence Present { get; } = new();

    public override string ToString() => "exists";
}
