using System.Text.Json;
using System.Text.Json.Serialization;
using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// Keeps Original Values in a JSON file under %ProgramData%\Akari-Dash\ so they survive the
/// app being deleted and re-downloaded.
/// </summary>
public sealed class FileOriginalValuesStore(string folder) : IOriginalValuesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private const string ServiceType = "service";
    private const string TaskType = "task";

    private readonly string _filePath = Path.Combine(folder, "original-values.json");
    private readonly object _lock = new();

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Akari-Dash");

    public AppliedTweak? Get(string tweakId)
    {
        lock (_lock)
        {
            return Load().TryGetValue(tweakId, out var record) ? FromRecord(record) : null;
        }
    }

    public void Save(string tweakId, AppliedTweak applied)
    {
        lock (_lock)
        {
            var records = Load();
            records[tweakId] = ToRecord(applied);
            Persist(records);
        }
    }

    public void Clear(string tweakId)
    {
        lock (_lock)
        {
            var records = Load();
            if (records.Remove(tweakId))
            {
                Persist(records);
            }
        }
    }

    // Unlike FileSettingsStorage, an unreadable file is an error, not an empty store: treating it
    // as empty would let the next Save erase every Original Value.
    private Dictionary<string, AppliedRecord> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Dictionary<string, AppliedRecord>>(File.ReadAllText(_filePath)) ?? [];
    }

    // Write to a temp file flushed to disk, then swap it in, so a crash or power loss mid-write
    // never loses the records.
    private void Persist(Dictionary<string, AppliedRecord> records)
    {
        Directory.CreateDirectory(folder);
        var tempPath = _filePath + ".tmp";
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, records, SerializerOptions);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static AppliedRecord ToRecord(AppliedTweak applied) => new(
        applied.LastAppliedOptionId,
        applied.OriginalValues.Select(original => ToRecord(original.Location, original.Value)).ToList());

    // A value of the wrong kind throws rather than being saved as "did not exist", which Undo
    // would act on by deleting.
    private static ValueRecord ToRecord(MachineLocation location, MachineValue? value) => (location, value) switch
    {
        (RegistryLocation registry, null) => new ValueRecord(null, registry.Hive, registry.Key, registry.Name, null, null),
        (RegistryLocation registry, RegistryValue data) => new ValueRecord(
            null,
            registry.Hive,
            registry.Key,
            registry.Name,
            data.Kind,
            JsonSerializer.SerializeToElement(data.Data, data.Data.GetType())),
        (ServiceLocation service, null) => new ValueRecord(ServiceType, null, null, service.ServiceName, null, null),
        (ServiceLocation service, ServiceStartValue start) => new ValueRecord(
            ServiceType, null, null, service.ServiceName, null, JsonSerializer.SerializeToElement(start.StartType.ToString())),
        (ScheduledTaskLocation task, null) => new ValueRecord(TaskType, null, null, task.Path, null, null),
        (ScheduledTaskLocation task, TaskEnabledValue enabled) => new ValueRecord(
            TaskType, null, null, task.Path, null, JsonSerializer.SerializeToElement(enabled.Enabled)),
        _ => throw new ArgumentException($"{location} cannot hold {value}.", nameof(value)),
    };

    private static AppliedTweak FromRecord(AppliedRecord record) => new(
        record.LastAppliedOptionId,
        record.OriginalValues.Select(FromRecord).ToList());

    private static OriginalValue FromRecord(ValueRecord value) => value.Type switch
    {
        null => new OriginalValue(
            new RegistryLocation(value.Hive!.Value, value.Key!, value.Name),
            value.Kind is { } kind && value.Data is { } data ? new RegistryValue(kind, ReadData(kind, data)) : null),
        ServiceType => new OriginalValue(
            new ServiceLocation(value.Name),
            value.Data is { } data ? new ServiceStartValue(Enum.Parse<ServiceStartType>(data.GetString()!)) : null),
        TaskType => new OriginalValue(
            new ScheduledTaskLocation(value.Name),
            value.Data is { } data ? new TaskEnabledValue(data.GetBoolean()) : null),
        _ => throw new JsonException($"Unknown kind of Original Value: {value.Type}"),
    };

    private static object ReadData(RegistryValueKind kind, JsonElement data) => kind switch
    {
        RegistryValueKind.DWord => data.GetInt32(),
        RegistryValueKind.QWord => data.GetInt64(),
        RegistryValueKind.String or RegistryValueKind.ExpandString => data.GetString()!,
        RegistryValueKind.MultiString => data.Deserialize<string[]>()!,
        // Binary, None and Unknown values all come back from the registry as raw bytes.
        _ => data.GetBytesFromBase64(),
    };

    // Type is null for a registry value (files written before service and task targets have no
    // Type), "service" for a service's start type (Name is the service) and "task" for a
    // scheduled task's enabled state (Name is the task path). Data is null when it did not exist.
    private sealed record ValueRecord(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Type,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RegistryHive? Hive,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Key,
        string Name,
        RegistryValueKind? Kind,
        JsonElement? Data);

    private sealed record AppliedRecord(string LastAppliedOptionId, List<ValueRecord> OriginalValues);
}
