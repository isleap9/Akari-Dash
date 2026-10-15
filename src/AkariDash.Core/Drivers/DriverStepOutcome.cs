namespace AkariDash.Core.Drivers;

/// <summary>How a driver row ended. A failure is not one of these; the service throws instead.</summary>
public enum DriverStepOutcome
{
    /// <summary>The driver was installed, and its settings were applied when the row asked for them.</summary>
    Completed,

    /// <summary>Installed, but Windows wants a restart before it is usable.</summary>
    CompletedNeedsRestart,

    /// <summary>Installed, but the settings the row promised were not applied. Says so; does not pretend.</summary>
    InstalledWithoutSettings,

    /// <summary>The vendor's own installer or page was opened because it cannot be run unattended.</summary>
    HandedOffToInstaller,
}
