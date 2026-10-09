using AkariDash.Framework.Services;
using Xunit;

namespace AkariDash.Tests;

public class FileSettingsStorageTests : IDisposable
{
    private readonly string _folderName;
    private readonly string _folderPath;

    public FileSettingsStorageTests()
    {
        _folderName = "FileSettingsStorageTests." + Guid.NewGuid().ToString("N");
        _folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _folderName);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folderPath, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task RoundTripsValues()
    {
        var storage = new FileSettingsStorage(_folderName);

        await storage.WriteAsync("Theme", "Dark");

        Assert.Equal("Dark", await storage.ReadAsync("Theme"));
    }

    [Fact]
    public async Task LeavesNoTempFileAfterSave()
    {
        var storage = new FileSettingsStorage(_folderName);

        await storage.WriteAsync("Theme", "Dark");

        var settingsPath = Path.Combine(_folderPath, "settings.json");
        Assert.True(File.Exists(settingsPath));
        Assert.Empty(Directory.GetFiles(_folderPath, "*.tmp"));
    }

    [Fact]
    public async Task ReturnsNullOnCorruptFile()
    {
        var storage = new FileSettingsStorage(_folderName);

        var settingsPath = Path.Combine(_folderPath, "settings.json");
        File.WriteAllText(settingsPath, "{bad json");

        Assert.Null(await storage.ReadAsync("Theme"));
    }

    [Fact]
    public async Task ReturnsNullOnMissingFile()
    {
        var storage = new FileSettingsStorage(_folderName);

        Assert.Null(await storage.ReadAsync("missing"));
    }
}
