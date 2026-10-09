using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// One named state a Tweak can be put in: the value of every target in that state.
/// A <see langword="null"/> value means the registry value does not exist.
/// </summary>
public sealed record TweakOption(string Id, string Label, IReadOnlyDictionary<RegistryTarget, RegistryValue?> Values);
