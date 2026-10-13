namespace AkariDash.Core.Apps;

/// <summary>
/// The Store apps and Restoration tabs' backend. An Action surface (ADR-0004): removing and restoring
/// run once, and neither is a Tweak.
/// </summary>
public interface IStoreAppService
{
    /// <summary>Every Store package this account has, apps, system packages and frameworks alike.</summary>
    Task<IReadOnlyList<StoreApp>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the package for every user, as AkariOS-Ultimate's Remove-AppxPackage -AllUsers does, then
    /// keeps it removed (see <see cref="AppxDeprovisioning"/>). Throws with a readable message on failure.
    /// </summary>
    Task RemoveAsync(StoreApp app, CancellationToken cancellationToken = default);

    /// <summary>
    /// Undoes what <see cref="RemoveAsync"/> did and puts the app back: from the files still on this PC
    /// when Windows kept them, otherwise from the Microsoft Store. Throws when neither works.
    /// </summary>
    Task RestoreAsync(RestorableApp app, CancellationToken cancellationToken = default);
}
