using Microsoft.Extensions.Logging;

namespace AkariDash.Framework.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that writes formatted log lines to a rolling file.
/// <para>
/// A file per day is created (<c>app-yyyyMMdd.log</c>). When a file exceeds the size cap
/// a new file is started (<c>app-yyyyMMdd.log.1</c>, <c>.2</c>, …). Only the newest files
/// up to the retention count are kept. All writes are serialized through a single lock so
/// multiple logger categories cannot interleave.
/// </para>
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly string _logDirectory;
    private readonly long _maxFileSizeBytes;
    private readonly int _maxRetainedFiles;
    private StreamWriter? _writer;
    private DateTime _activeDate;

    /// <summary>Creates a provider that writes logs under <paramref name="logDirectory"/>.</summary>
    public FileLoggerProvider(
        string logDirectory,
        long maxFileSizeBytes = 1_048_576,
        int maxRetainedFiles = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        if (maxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes));
        }

        if (maxRetainedFiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetainedFiles));
        }

        _logDirectory = logDirectory;
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxRetainedFiles = maxRetainedFiles;
    }

    /// <summary>Creates a logger for the given category.</summary>
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    internal void Write(string categoryName, string formattedMessage)
    {
        lock (_gate)
        {
            try
            {
                EnsureWriter()?.WriteLine(formattedMessage);
            }
            catch
            {
                // Logging must never crash the app (e.g. when a disk is full or a
                // log path is unwritable). Reset the writer so the next write retries.
                CloseWriter();
            }
        }
    }

    private StreamWriter? EnsureWriter()
    {
        var now = DateTime.Now;

        var sizeExceeded = _writer is not null &&
                           _activeDate == now.Date &&
                           _writer.BaseStream.Length > _maxFileSizeBytes;
        var dateChanged = _writer is not null && _activeDate != now.Date;

        if (_writer is not null && !sizeExceeded && !dateChanged)
        {
            return _writer;
        }

        CloseWriter();
        Directory.CreateDirectory(_logDirectory);

        var basePath = Path.Combine(_logDirectory, $"app-{now:yyyyMMdd}.log");

        if (sizeExceeded)
        {
            var index = 1;
            while (File.Exists($"{basePath}.{index}"))
            {
                index++;
            }

            _writer = CreateWriter($"{basePath}.{index}");
        }
        else
        {
            _writer = CreateWriter(basePath);
        }

        _activeDate = now.Date;
        Prune();
        return _writer;
    }

    /// <summary>
    /// Opens a writer in append mode. The file is shared for reading and deletion so
    /// logs can be inspected while the app is running.
    /// </summary>
    private static StreamWriter CreateWriter(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);

        return new StreamWriter(stream) { AutoFlush = true };
    }

    private void CloseWriter()
    {
        _writer?.Dispose();
        _writer = null;
    }

    /// <summary>Deletes the oldest files so at most the retention count remain.</summary>
    private void Prune()
    {
        try
        {
            var oldest = Directory
                .EnumerateFiles(_logDirectory)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Skip(_maxRetainedFiles)
                .ToList();

            foreach (var file in oldest)
            {
                File.Delete(file);
            }
        }
        catch
        {
            // Never let log-housekeeping take down the app.
        }
    }

    /// <summary>Closes the active log file.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            CloseWriter();
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _categoryName;

        public FileLogger(FileLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);

            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = logLevel.ToString().ToUpperInvariant();

            if (exception is null)
            {
                _provider.Write(_categoryName, $"{timestamp} [{level}] {_categoryName}: {message}");
            }
            else
            {
                _provider.Write(_categoryName, $"{timestamp} [{level}] {_categoryName}: {message}");
                _provider.Write(_categoryName, $"{timestamp} [{level}] {exception}");
            }
        }
    }
}
