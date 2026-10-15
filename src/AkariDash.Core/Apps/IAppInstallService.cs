namespace AkariDash.Core.Apps;

/// <summary>
/// The Downloads page's backend: which of the catalogue is already installed, and installing one.
/// An Action backend (ADR-0004): an install runs once, cannot be undone, and is never a Tweak.
/// </summary>
public interface IAppInstallService
{
    /// <summary>
    /// True in the Phase 1 build, where nothing is installed and the page only says what it would
    /// have run. Phase 1 promises it is safe to launch anywhere; a real install would break that.
    /// </summary>
    bool IsDryRun { get; }

    /// <summary>
    /// The catalogue's ids that are installed, or <see langword="null"/> when winget could not be
    /// asked - in which case the page says so rather than claiming everything is missing.
    /// </summary>
    Task<IReadOnlySet<string>?> FindInstalledIdsAsync(
        IReadOnlyList<InstallableApp> apps,
        CancellationToken cancellationToken = default);

    /// <summary>Installs one app. Throws with a readable message when it fails.</summary>
    Task<InstallOutcome> InstallAsync(InstallableApp app, CancellationToken cancellationToken = default);
}
