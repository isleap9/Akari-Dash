using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>What kind of work a driver row does.</summary>
public enum DriverStepKind
{
    Install,
    InstallAndConfigure,
    Debloat,
}

/// <summary>How much care a row needs before it is pressed.</summary>
public enum DriverRisk
{
    Safe,
    Caution,
    Advanced,
}

/// <summary>
/// One row on the Drivers page: which card it is for, what it does, and the one line that
/// tells a person what they are about to do.
/// </summary>
public sealed record DriverRow(
    string Id,
    GpuVendor Vendor,
    DriverStepKind Kind,
    string Title,
    string Description,
    DriverRisk Risk);
