namespace AkariDash.Core.Machine;

/// <summary>
/// One setting of the Windows entry this PC booted from (bcdedit's <c>{current}</c>), by its bcdedit
/// name, such as <c>hypervisorlaunchtype</c>. Like a registry value, a missing setting is normal: it
/// means Windows' default, and deleting it puts that default back.
/// </summary>
public sealed record BootSettingLocation(string Name) : MachineLocation
{
    public override string ToString() => $"Boot setting {Name}";
}

/// <summary>What a <see cref="BootSettingLocation"/> holds, as bcdedit shows and takes it (e.g. <c>Auto</c>, <c>Yes</c>).</summary>
public sealed record BootSettingValue(string Text) : MachineValue
{
    public override string ToString() => Text;
}
