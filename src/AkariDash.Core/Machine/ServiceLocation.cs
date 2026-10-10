namespace AkariDash.Core.Machine;

/// <summary>The start type of the Windows service named <paramref name="ServiceName"/>.</summary>
public sealed record ServiceLocation(string ServiceName) : MachineLocation
{
    public override string ToString() => $"Service {ServiceName} start type";
}
