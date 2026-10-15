namespace AkariDash.Core.Apps;

/// <summary>How an install ended. A failure is not one of these; <see cref="IAppInstallService.InstallAsync"/> throws instead.</summary>
public enum InstallOutcome
{
    /// <summary>Installed and ready.</summary>
    Installed,

    /// <summary>Installed, but Windows wants a restart before it is usable.</summary>
    InstalledNeedsRestart,

    /// <summary>It was already on the machine; nothing was downloaded.</summary>
    AlreadyInstalled,
}
