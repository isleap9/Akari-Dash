using System.Text.Json;
using System.Text.Json.Serialization;
using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>Where a DDU clean is, across the restarts it needs.</summary>
public enum DriverResetStage
{
    /// <summary>Set up: the next restart goes to safe mode and runs the companion.</summary>
    Scheduled,

    /// <summary>The companion started DDU. Still this after a restart means it never finished.</summary>
    Cleaning,

    /// <summary>DDU ran to the end.</summary>
    Cleaned,

    /// <summary>The companion could not run DDU; <see cref="DriverResetState.Message"/> says why.</summary>
    Failed,
}

/// <summary>
/// The note the app and the safe-mode companion pass each other, kept in one small file under
/// ProgramData so it survives the restarts. The app writes it when it schedules the clean; the
/// companion updates it in safe mode; the app reads it back when Windows is normal again.
/// </summary>
public sealed record DriverResetState(
    GpuVendor Vendor,
    bool Manual,
    string DduFolder,
    DateTimeOffset Scheduled,
    DriverResetStage Stage,
    int? ExitCode = null,
    string? Message = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The file, under ProgramData\Akari-Dash.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Akari-Dash", "driver-reset.json");

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>The state in <paramref name="json"/>, or <see langword="null"/> for anything that is not one.</summary>
    public static DriverResetState? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<DriverResetState>(json, Options);
            return state is not null
                && Enum.IsDefined(state.Vendor)
                && Enum.IsDefined(state.Stage)
                && !string.IsNullOrWhiteSpace(state.DduFolder)
                ? state
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The line the page shows about this clean once Windows is back to normal.</summary>
    public string Summary => Stage switch
    {
        DriverResetStage.Scheduled => $"A DDU clean of the {Vendor.DisplayName()} driver is waiting for the next restart.",
        DriverResetStage.Cleaning => $"The DDU clean of the {Vendor.DisplayName()} driver started but did not finish. Run it again, or install the driver.",
        DriverResetStage.Cleaned when Manual => $"DDU was opened in safe mode for the {Vendor.DisplayName()} driver. Install the driver now.",
        DriverResetStage.Cleaned => $"DDU removed the {Vendor.DisplayName()} driver. Install the driver now.",
        _ => $"The DDU clean of the {Vendor.DisplayName()} driver did not run: {Message ?? "no reason was recorded"}",
    };
}
