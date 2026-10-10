namespace AkariDash.Core.Machine;

/// <summary>
/// Whether the scheduled task at <paramref name="Path"/> (e.g. <c>\Microsoft\Windows\Defrag\ScheduledDefrag</c>)
/// is enabled.
/// </summary>
public sealed record ScheduledTaskLocation(string Path) : MachineLocation
{
    public override string ToString() => $"Scheduled task {Path}";
}
