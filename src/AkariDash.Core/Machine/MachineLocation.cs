namespace AkariDash.Core.Machine;

/// <summary>
/// Something on the machine a Tweak can read and write: a registry value
/// (<see cref="RegistryLocation"/>), one bit of a binary registry value (<see cref="RegistryBitLocation"/>),
/// one <c>name=value;</c> entry of a registry string (<see cref="RegistryEntryLocation"/>),
/// whether a registry key exists at all (<see cref="RegistryKeyLocation"/>),
/// a service's start type (<see cref="ServiceLocation"/>), a scheduled task's enabled state
/// (<see cref="ScheduledTaskLocation"/>) or a boot setting (<see cref="BootSettingLocation"/>).
/// </summary>
public abstract record MachineLocation;
