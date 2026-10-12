namespace AkariDash.Core.Machine;

/// <summary>Whether a <see cref="RegistryBitLocation"/> is set.</summary>
public sealed record BitValue(bool IsSet) : MachineValue
{
    public static BitValue On { get; } = new(true);

    public static BitValue Off { get; } = new(false);

    public override string ToString() => IsSet ? "On" : "Off";
}
