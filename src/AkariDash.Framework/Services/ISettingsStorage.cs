namespace AkariDash.Framework.Services;

/// <summary>
/// Key/value persistence abstraction. Implementations may back onto the
/// file system, an in-memory dictionary (tests), registry, etc.
/// </summary>
public interface ISettingsStorage
{
    Task<string?> ReadAsync(string key);

    Task WriteAsync(string key, string? value);

    Task DeleteAsync(string key);
}

/// <summary>
/// In-memory implementation of <see cref="ISettingsStorage"/>,
/// primarily useful for unit tests.
/// </summary>
public sealed class MemorySettingsStorage : ISettingsStorage
{
    private readonly Dictionary<string, string> _values = new();

    public Task<string?> ReadAsync(string key)
    {
        _values.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task WriteAsync(string key, string? value)
    {
        if (value is null)
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }
}
