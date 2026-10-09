using AkariDash.Core.Machine;

namespace AkariDash.Tests;

/// <summary>
/// Wraps a machine so that writing <c>failWrite</c> fails and deleting <c>failDelete</c> fails:
/// enough to make a rollback (which deletes values that did not exist) fail as well.
/// </summary>
public sealed class RollbackFailingMachine(IMachine inner, RegistryLocation failWrite, RegistryLocation failDelete) : IMachine
{
    public RegistryValue? ReadRegistryValue(RegistryLocation location) => inner.ReadRegistryValue(location);

    public void WriteRegistryValue(RegistryLocation location, RegistryValue value)
    {
        if (location == failWrite)
        {
            throw new UnauthorizedAccessException("Access is denied.");
        }

        inner.WriteRegistryValue(location, value);
    }

    public void DeleteRegistryValue(RegistryLocation location)
    {
        if (location == failDelete)
        {
            throw new UnauthorizedAccessException("Access is denied.");
        }

        inner.DeleteRegistryValue(location);
    }
}
