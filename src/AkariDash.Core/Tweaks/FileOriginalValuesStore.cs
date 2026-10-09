using System.Text.Json;
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
        applied.OriginalValues
            .Select(original => new ValueRecord(
                original.Location.Hive,
                original.Location.Key,
                original.Location.Name,
                original.Value?.Kind,
                original.Value is null ? null : JsonSerializer.SerializeToElement(original.Value.Data, original.Value.Data.GetType())))
            .ToList());

    private static AppliedTweak FromRecord(AppliedRecord record) => new(
        record.LastAppliedOptionId,
        record.OriginalValues
            .Select(value => new OriginalValue(
                new RegistryLocation(value.Hive, value.Key, value.Name),
                value.Kind is { } kind && value.Data is { } data ? new RegistryValue(kind, ReadData(kind, data)) : null))
            .ToList());

    private static object ReadData(RegistryValueKind kind, JsonElement data) => kind switch
    {
        RegistryValueKind.DWord => data.GetInt32(),
        RegistryValueKind.QWord => data.GetInt64(),
        RegistryValueKind.String or RegistryValueKind.ExpandString => data.GetString()!,
        RegistryValueKind.MultiString => data.Deserialize<string[]>()!,
        // Binary, None and Unknown values all come back from the registry as raw bytes.
        _ => data.GetBytesFromBase64(),
    };

    // Kind and Data are null when the value did not exist.
    private sealed record ValueRecord(RegistryHive Hive, string Key, string Name, RegistryValueKind? Kind, JsonElement? Data);

    private sealed record AppliedRecord(string LastAppliedOptionId, List<ValueRecord> OriginalValues);
}
