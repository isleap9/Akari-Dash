using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AkariDash.Framework.Services;

/// <summary>
/// File picking for unpackaged WinUI 3 apps (pickers are initialized with the
/// owning window handle instead of requiring an MSIX package).
/// </summary>
public interface IFilePickerService
{
    /// <summary>Opens a multi-select file picker. Returns null when cancelled.</summary>
    Task<IReadOnlyList<StorageFile>?> PickOpenFilesAsync(
        IReadOnlyList<string> fileTypeFilters,
        string? suggestedStartLocation = null);

    /// <summary>Opens a single-select file picker. Returns null when cancelled.</summary>
    Task<StorageFile?> PickOpenFileAsync(
        IReadOnlyList<string> fileTypeFilters,
        string? suggestedStartLocation = null);

    /// <summary>Opens a save-as picker. Returns null when cancelled.</summary>
    Task<StorageFile?> PickSaveFileAsync(
        string suggestedFileName,
        IReadOnlyList<string> fileTypeFilters,
        string? suggestedStartLocation = null);
}

public sealed class FilePickerService : IFilePickerService
{
    private readonly Func<IntPtr> _hwndProvider;

    public FilePickerService(Func<IntPtr> hwndProvider)
    {
        _hwndProvider = hwndProvider ?? throw new ArgumentNullException(nameof(hwndProvider));
    }

    public async Task<IReadOnlyList<StorageFile>?> PickOpenFilesAsync(
        IReadOnlyList<string> fileTypeFilters,
        string? suggestedStartLocation = null)
    {
        var picker = CreateFileOpenPicker(fileTypeFilters, suggestedStartLocation);
        var files = await picker.PickMultipleFilesAsync();
        return files?.ToList();
    }

    public async Task<StorageFile?> PickOpenFileAsync(
        IReadOnlyList<string> fileTypeFilters,
        string? suggestedStartLocation = null)
    {
        var picker = CreateFileOpenPicker(fileTypeFilters, suggestedStartLocation);
        return await picker.PickSingleFileAsync();
    }

    public async Task<StorageFile?> PickSaveFileAsync(
        string suggestedFileName,
        IReadOnlyList<string> fileTypeFilters,
        string? suggestedStartLocation = null)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
        };

        if (TryGetStartLocation(suggestedStartLocation, out var startLocation))
        {
            picker.SuggestedStartLocation = startLocation;
        }

        foreach (var filter in fileTypeFilters)
        {
            picker.FileTypeChoices.Add(Path.GetExtension(filter), [filter]);
        }

        InitializeWithWindow.Initialize(picker, _hwndProvider());
        return await picker.PickSaveFileAsync();
    }

    private FileOpenPicker CreateFileOpenPicker(IReadOnlyList<string> fileTypeFilters, string? suggestedStartLocation)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
        };

        if (TryGetStartLocation(suggestedStartLocation, out var startLocation))
        {
            picker.SuggestedStartLocation = startLocation;
        }

        foreach (var filter in fileTypeFilters)
        {
            picker.FileTypeFilter.Add(filter);
        }

        InitializeWithWindow.Initialize(picker, _hwndProvider());
        return picker;
    }

    private static bool TryGetStartLocation(string? suggestedStartLocation, out PickerLocationId location)
    {
        switch (suggestedStartLocation?.ToLowerInvariant())
        {
            case "documents":
                location = PickerLocationId.DocumentsLibrary;
                return true;
            case "pictures":
                location = PickerLocationId.PicturesLibrary;
                return true;
            case "downloads":
                location = PickerLocationId.Downloads;
                return true;
            default:
                location = PickerLocationId.ComputerFolder;
                return false;
        }
    }
}
