using AkariDash.Core.Machine;

namespace AkariDash.Tests;

/// <summary>
/// Wraps a machine so that writing <c>failWrite</c> fails and deleting <c>failDelete</c> fails:
/// enough to make a rollback (which deletes values that did not exist) fail as well.
/// </summary>
public sealed class RollbackFailingMachine(IMachine inner, MachineLocation failWrite, MachineLocation failDelete) : IMachine
{
    public MachineValue? Read(MachineLocation location) => inner.Read(location);

    public void Write(MachineLocation location, MachineValue value)
    {
        if (location == failWrite)
        {
            throw new UnauthorizedAccessException("Access is denied.");
        }

        inner.Write(location, value);
    }

    public void Delete(MachineLocation location)
    {
        if (location == failDelete)
        {
            throw new UnauthorizedAccessException("Access is denied.");
        }

        inner.Delete(location);
    }

    public IReadOnlySet<GpuVendor> GpuVendors() => inner.GpuVendors();
}
