namespace AkariDash.Core.Machine;

/// <summary>Whether a <see cref="ScheduledTaskLocation"/> is enabled.</summary>
public sealed record TaskEnabledValue(bool Enabled) : MachineValue
{
    public static TaskEnabledValue On { get; } = new(true);

    public static TaskEnabledValue Off { get; } = new(false);

    public override string ToString() => Enabled ? "Enabled" : "Disabled";
}
