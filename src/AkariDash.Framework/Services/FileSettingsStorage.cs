using System.Text.Json;

namespace AkariDash.Framework.Services;

/// <summary>
/// File-based <see cref="ISettingsStorage"/> that keeps a single JSON document
/// under %LOCALAPPDATA%\{AppName}\settings.json.
/// </summary>
public sealed class FileSettingsStorage : ISettingsStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly object _lock = new();

    public FileSettingsStorage(string? appName = null)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName ?? "AkariDash.Framework");

        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "settings.json");
    }

    public Task<string?> ReadAsync(string key)
    {
        lock (_lock)
        {
            var dictionary = Load();
            return Task.FromResult(dictionary.TryGetValue(key, out var value) ? value : null);
        }
    }

    public Task WriteAsync(string key, string? value)
    {
        lock (_lock)
        {
            var dictionary = Load();
            if (value is null)
            {
                dictionary.Remove(key);
            }
            else
            {
                dictionary[key] = value;
            }

            Save(dictionary);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        lock (_lock)
        {
            var dictionary = Load();
            dictionary.Remove(key);
            Save(dictionary);
        }

        return Task.CompletedTask;
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions);
            return dictionary ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // A missing, corrupt or unreadable file falls back to empty settings.
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save(Dictionary<string, string> dictionary)
    {
        var json = JsonSerializer.Serialize(dictionary, SerializerOptions);

        // Write to a temp file then atomically move it into place, so a crash mid-write
        // can never leave a corrupt settings.json behind.
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
