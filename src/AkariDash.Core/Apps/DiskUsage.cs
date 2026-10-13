using System.Diagnostics;

namespace AkariDash.Core.Apps;

/// <summary>One folder or file in a drive scan, with everything under it added up.</summary>
public sealed record DiskEntry(string Path, string Name, long Bytes, long Files, bool IsFolder);

/// <summary>What scanning a drive found: its totals, its biggest top-level folders and its biggest files.</summary>
public sealed record DiskScan(
    string Root,
    long Bytes,
    long Files,
    TimeSpan Elapsed,
    IReadOnlyList<DiskEntry> TopFolders,
    IReadOnlyList<DiskEntry> LargestFiles);

/// <summary>
/// The Drives tab's scanner, after Vain Toolbox's: walks a drive and adds up what is on it. Read-only.
/// Folders it may not read are skipped, and links are not followed, so nothing is counted twice.
/// </summary>
public static class DiskUsage
{
    public const int LargestFileCount = 50;

    public static DiskScan Scan(string root, IProgress<long>? filesSeen = null, CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        var largest = new PriorityQueue<DiskEntry, long>();
        long files = 0;
        long bytes = 0;
        List<DiskEntry> folders = [];

        var top = new DirectoryInfo(root);
        foreach (var file in SafeFiles(top))
        {
            Count(file);
        }

        foreach (var folder in SafeFolders(top))
        {
            long folderBytes = 0;
            long folderFiles = 0;
            var stack = new Stack<DirectoryInfo>([folder]);
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = stack.Pop();
                foreach (var file in SafeFiles(current))
                {
                    Count(file);
                    folderBytes += file.Length;
                    folderFiles++;
                }

                foreach (var child in SafeFolders(current))
                {
                    stack.Push(child);
                }
            }

            folders.Add(new DiskEntry(folder.FullName, folder.Name, folderBytes, folderFiles, IsFolder: true));
        }

        var biggest = new List<DiskEntry>();
        while (largest.TryDequeue(out var entry, out _))
        {
            biggest.Add(entry);
        }

        biggest.Reverse();
        return new DiskScan(root, bytes, files, clock.Elapsed, [.. folders.OrderByDescending(folder => folder.Bytes)], biggest);

        void Count(FileInfo file)
        {
            files++;
            bytes += file.Length;
            if (files % 2000 == 0)
            {
                filesSeen?.Report(files);
            }

            // Keep the LargestFileCount biggest: a min-heap whose smallest is dropped once it is full.
            largest.Enqueue(new DiskEntry(file.FullName, file.Name, file.Length, 1, IsFolder: false), file.Length);
            if (largest.Count > LargestFileCount)
            {
                largest.Dequeue();
            }
        }
    }

    private static IEnumerable<FileInfo> SafeFiles(DirectoryInfo folder)
    {
        try
        {
            return folder.EnumerateFiles("*", Options).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return [];
        }
    }

    private static IEnumerable<DirectoryInfo> SafeFolders(DirectoryInfo folder)
    {
        try
        {
            // Junctions and symbolic links point at folders counted elsewhere (or at themselves).
            return folder.EnumerateDirectories("*", Options)
                .Where(child => !child.Attributes.HasFlag(FileAttributes.ReparsePoint))
                .ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return [];
        }
    }

    private static EnumerationOptions Options { get; } = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        RecurseSubdirectories = false,
    };

    /// <summary>Bytes as Vain Toolbox and Explorer show them: 5.6 MB, 138.2 GB.</summary>
    public static string Format(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.0} {units[unit]}";
    }
}
