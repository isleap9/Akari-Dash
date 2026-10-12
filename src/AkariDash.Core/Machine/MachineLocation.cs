namespace AkariDash.Core.Machine;

/// <summary>
/// Something on the machine a Tweak can read and write: a registry value
/// (<see cref="RegistryLocation"/>), one bit of a binary registry value (<see cref="RegistryBitLocation"/>),
/// a service's start type (<see cref="ServiceLocation"/>) or a scheduled task's enabled state
/// (<see cref="ScheduledTaskLocation"/>).
/// </summary>
public abstract record MachineLocation;
