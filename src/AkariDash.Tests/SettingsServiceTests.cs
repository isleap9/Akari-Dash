using AkariDash.Framework.Services;
using Xunit;

namespace AkariDash.Tests;

public class SettingsServiceTests
{
    [Fact]
    public async Task MemorySettingsStorage_round_trips_values()
    {
        var storage = new MemorySettingsStorage();

        await storage.WriteAsync("key", "value");

        Assert.Equal("value", await storage.ReadAsync("key"));
    }

    [Fact]
    public async Task MemorySettingsStorage_write_null_removes_key()
    {
        var storage = new MemorySettingsStorage();
        await storage.WriteAsync("key", "value");

        await storage.WriteAsync("key", null);

        Assert.Null(await storage.ReadAsync("key"));
    }

    [Fact]
    public async Task MemorySettingsStorage_delete_removes_key()
    {
        var storage = new MemorySettingsStorage();
        await storage.WriteAsync("key", "value");

        await storage.DeleteAsync("key");

        Assert.Null(await storage.ReadAsync("key"));
    }

    [Fact]
    public async Task SettingsService_returns_default_when_unset()
    {
        var service = new SettingsService(new MemorySettingsStorage());

        var value = await service.GetAsync<int>("missing", 42);

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task SettingsService_round_trips_typed_values()
    {
        var service = new SettingsService(new MemorySettingsStorage());
        var profile = new UserProfile { Name = "Ada", Age = 36 };

        await service.SetAsync("User.Profile", profile);
        var loaded = await service.GetAsync<UserProfile>("User.Profile");

        Assert.NotNull(loaded);
        Assert.Equal("Ada", loaded.Name);
        Assert.Equal(36, loaded.Age);
    }

    [Fact]
    public async Task SettingsService_remove_deletes_key()
    {
        var service = new SettingsService(new MemorySettingsStorage());
        await service.SetAsync("key", 5);

        await service.RemoveAsync("key");

        Assert.Equal(0, await service.GetAsync<int>("key"));
    }

    [Fact]
    public async Task SettingsService_returns_default_on_corrupt_json()
    {
        var storage = new MemorySettingsStorage();
        await storage.WriteAsync("key", "not-json{{");
        var service = new SettingsService(storage);

        Assert.Equal(-1, await service.GetAsync<int>("key", -1));
    }

    private sealed class UserProfile
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }
}
