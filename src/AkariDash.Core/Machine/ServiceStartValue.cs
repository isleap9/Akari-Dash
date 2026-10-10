namespace AkariDash.Core.Machine;

/// <summary>When Windows starts a service.</summary>
public enum ServiceStartType
{
    Boot,
    System,
    Automatic,
    AutomaticDelayed,
    Manual,
    Disabled,
}

/// <summary>The start type a <see cref="ServiceLocation"/> holds.</summary>
public sealed record ServiceStartValue(ServiceStartType StartType) : MachineValue
{
    public override string ToString() => StartType switch
    {
        ServiceStartType.AutomaticDelayed => "Automatic (delayed start)",
        _ => StartType.ToString(),
    };
}
