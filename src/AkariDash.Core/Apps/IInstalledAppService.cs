namespace AkariDash.Core.Apps;

/// <summary>Lists the machine's Store and Win32 apps and removes them. An Action backend: uninstalls run once and cannot be undone.</summary>
public interface IInstalledAppService
{
    /// <summary>Every uninstallable app, Store packages first, then Win32, both by name.</summary>
    Task<IReadOnlyList<InstalledApp>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes one app. Throws with a readable message when the removal fails.</summary>
    Task UninstallAsync(InstalledApp app, CancellationToken cancellationToken = default);
}
