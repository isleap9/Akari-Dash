namespace AkariDash.Core.Machine;

/// <summary>The single seam through which all system access passes.</summary>
public interface IMachine
{
    /// <summary>
    /// Reads what <paramref name="location"/> holds, or <see langword="null"/> when it does not exist
    /// (no such registry key or value, service or scheduled task).
    /// </summary>
    MachineValue? Read(MachineLocation location);

    /// <summary>
    /// Sets <paramref name="location"/> to <paramref name="value"/>, which must be the kind of value
    /// that location holds. A registry value is created, with its key, if needed; a service or
    /// scheduled task must already exist.
    /// </summary>
    void Write(MachineLocation location, MachineValue value);

    /// <summary>
    /// Deletes a registry value; deleting one that does not exist does nothing. Services and
    /// scheduled tasks cannot be deleted, so for those it does nothing only when they do not exist.
    /// </summary>
    void Delete(MachineLocation location);
}
