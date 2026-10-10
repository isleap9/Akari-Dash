namespace AkariDash.Core.Machine;

/// <summary>
/// What a <see cref="MachineLocation"/> holds: a <see cref="RegistryValue"/>, a
/// <see cref="ServiceStartValue"/> or a <see cref="TaskEnabledValue"/>. A location that does not
/// exist is represented by <see langword="null"/> wherever a <see cref="MachineValue"/> is expected.
/// </summary>
public abstract record MachineValue
{
    /// <summary>Text for a value that may not exist.</summary>
    public static string Describe(MachineValue? value) => value?.ToString() ?? "(not set)";
}
