using System.Text.Json;

namespace AkariDash.Framework.Services;

/// <summary>
/// Typed JSON settings service. Values are serialized to JSON strings and
/// persisted through <see cref="ISettingsStorage"/>.
/// </summary>
public interface ISettingsService
{
    Task<T?> GetAsync<T>(string key, T? defaultValue = default);

    Task SetAsync<T>(string key, T value);

    Task RemoveAsync(string key);
}

/// <summary>Default <see cref="ISettingsService"/> using System.Text.Json serialization.</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsStorage _storage;

    public SettingsService(ISettingsStorage storage)
    {
        _storage = storage;
    }

    public async Task<T?> GetAsync<T>(string key, T? defaultValue = default)
    {
        var raw = await _storage.ReadAsync(key);
        if (raw is null)
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(raw, SerializerOptions);
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    public Task SetAsync<T>(string key, T value)
    {
        var raw = JsonSerializer.Serialize(value, SerializerOptions);
        return _storage.WriteAsync(key, raw);
    }

    public Task RemoveAsync(string key) => _storage.DeleteAsync(key);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
