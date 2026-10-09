using AkariDash.Framework.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AkariDash.Tests;

public class LoggingTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "FileLoggerTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Reads a file that may still be open by a <see cref="FileLoggerProvider"/>.
    /// The read must use a share that includes Write (unlike <see cref="File.ReadAllText(string)"/>).
    /// </summary>
    private static string ReadAll(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Writes_log_lines_to_a_file_under_the_directory()
    {
        using var provider = new FileLoggerProvider(_directory);
        var logger = provider.CreateLogger("AkariDash.Test");

        logger.LogInformation("Hello {Name}", "World");

        var file = Directory.GetFiles(_directory).Single();
        var content = ReadAll(file);

        Assert.Contains("AkariDash.Test", content);
        Assert.Contains("Hello World", content);
    }

    [Fact]
    public void Exception_includes_type_message_and_stack_trace()
    {
        using var provider = new FileLoggerProvider(_directory);
        var logger = provider.CreateLogger("AkariDash.Test");

        logger.LogError(new InvalidOperationException("boom"), "Failed to do thing");

        var content = ReadAll(Directory.GetFiles(_directory).Single());

        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("boom", content);
        Assert.Contains("Failed to do thing", content);
    }

    [Fact]
    public void Multiple_categories_share_the_same_file()
    {
        using var provider = new FileLoggerProvider(_directory);
        provider.CreateLogger("Cat.A").LogInformation("one");
        provider.CreateLogger("Cat.B").LogInformation("two");

        var files = Directory.GetFiles(_directory);

        Assert.Single(files);
        var content = ReadAll(files[0]);
        Assert.Contains("Cat.A", content);
        Assert.Contains("Cat.B", content);
    }

    [Fact]
    public void Rotates_to_a_new_file_when_the_size_cap_is_exceeded()
    {
        using var provider = new FileLoggerProvider(_directory, maxFileSizeBytes: 256, maxRetainedFiles: 50);
        var logger = provider.CreateLogger("AkariDash.Test");

        for (var i = 0; i < 50; i++)
        {
            logger.LogInformation(new string('x', 200));
        }

        Assert.True(Directory.GetFiles(_directory).Length > 1);
    }

    [Fact]
    public void Deletes_oldest_files_beyond_the_retention_count()
    {
        using var provider = new FileLoggerProvider(_directory, maxFileSizeBytes: 128, maxRetainedFiles: 3);
        var logger = provider.CreateLogger("AkariDash.Test");

        for (var i = 0; i < 100; i++)
        {
            logger.LogInformation(new string('x', 100));
        }

        Assert.True(Directory.GetFiles(_directory).Length <= 3);
    }
}
