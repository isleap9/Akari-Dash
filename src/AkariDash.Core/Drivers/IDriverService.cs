using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>The graphics card Windows has a driver for, and that driver's version as its vendor writes it.</summary>
/// <param name="CardName">The adapter's name, e.g. <c>NVIDIA GeForce RTX 4070</c>.</param>
/// <param name="Version">The installed version, or <see langword="null"/> when it could not be read.</param>
public sealed record InstalledDriver(string CardName, string? Version);

/// <summary>
/// Does what AkariOS-Ultimate's Graphics entries do: finds, installs and configures a graphics
/// driver, cleans it with DDU, applies and reverts the settings entries, installs DirectX and the
/// C++ runtimes. In the Phase 1 build nothing is written, downloaded or run - see <see cref="IsDryRun"/>.
/// Reads are always live.
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

    /// <summary>What a real run of this driver choice would do, in the words the Phase 1 build reports.</summary>
    string Describe(DriverRow row, string? installerPath = null);

    /// <summary>What a real run of these steps would do, one per line.</summary>
    string Describe(IReadOnlyList<DriverExtra> steps);

    /// <summary>The newest installer in the drop folder that is this vendor's own, or <see langword="null"/>.</summary>
    string? DroppedInstaller(GpuVendor vendor);

    /// <summary>Driver install (updated), or Driver install and settings (updated), for the row's card.</summary>
    Task<DriverStepOutcome> InstallAsync(DriverRow row, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Driver debloat install and settings: strips a downloaded vendor installer, installs what is left and configures it.</summary>
    Task<DriverStepOutcome> DebloatAsync(DriverRow row, string installerPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Driver clean (DDU): puts DDU (and, for the cards in <paramref name="cards"/>, the replacement
    /// driver) on disk, sets the next start to safe mode with the companion in RunOnce, and restarts,
    /// as AkariOS does.
    /// </summary>
    Task<DriverStepOutcome> CleanAsync(bool manual, IReadOnlySet<GpuVendor> cards, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Runs one entry's steps (Optimize, Default, or an Action); <see langword="false"/> when any did not apply.</summary>
    Task<bool> RunStepsAsync(IReadOnlyList<DriverExtra> steps, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>The clean that just ran, or is waiting; <see langword="null"/> when there is none.</summary>
    DriverResetState? ReadReset();

    /// <summary>Forgets a clean that has run, and tidies DDU away.</summary>
    void ForgetReset();
}
