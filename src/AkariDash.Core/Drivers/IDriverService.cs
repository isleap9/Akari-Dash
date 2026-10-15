using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>The graphics card Windows has a driver for, and that driver's version as its vendor writes it.</summary>
/// <param name="CardName">The adapter's name, e.g. <c>NVIDIA GeForce RTX 4070</c>.</param>
/// <param name="Version">The installed version, or <see langword="null"/> when it could not be read.</param>
public sealed record InstalledDriver(string CardName, string? Version);

/// <summary>
/// Finds, installs and configures a graphics card's driver. An Action backend (ADR-0004): every
/// write here runs once and cannot be undone, and in the Phase 1 build nothing is written,
/// downloaded or run at all - see <see cref="IsDryRun"/>. Reads are always live.
/// </summary>
public interface IDriverService
{
    bool IsDryRun { get; }

    /// <summary>The vendor's current driver, or <see langword="null"/> when its source could not be read.</summary>
    Task<AvailableDriver?> LatestAsync(GpuVendor vendor, CancellationToken cancellationToken = default);

    /// <summary>
    /// The vendor's card and its installed driver, or <see langword="null"/> when the machine could
    /// not be asked. A <see langword="null"/> means "could not ask", never "nothing installed".
    /// </summary>
    Task<InstalledDriver?> InstalledAsync(GpuVendor vendor, CancellationToken cancellationToken = default);

    /// <summary>What a real run of this row would do, in the words the Phase 1 build reports.</summary>
    string Describe(DriverRow row, string? installerPath = null);

    /// <summary>Installs the latest driver for the row's card, configuring it when the row says so.</summary>
    Task<DriverStepOutcome> InstallAsync(DriverRow row, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Strips a downloaded vendor installer of its extras, installs what is left and configures it.</summary>
    Task<DriverStepOutcome> DebloatAsync(DriverRow row, string installerPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages DDU and the replacement driver, then sets the next restart to safe mode, where the
    /// companion runs DDU on the row's card only. Nothing is removed until that restart.
    /// </summary>
    Task<DriverStepOutcome> CleanAsync(DriverRow row, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>The clean waiting for a restart, or the one that just ran; <see langword="null"/> when there is none.</summary>
    DriverResetState? ReadReset();

    /// <summary>Takes back a clean that has not run yet: no safe mode, no RunOnce, no note.</summary>
    Task CancelResetAsync(CancellationToken cancellationToken = default);

    /// <summary>Forgets a clean that has run, and tidies DDU away.</summary>
    void ForgetReset();

    /// <summary>Restarts the PC, into the clean that is waiting.</summary>
    void RestartNow();
}
