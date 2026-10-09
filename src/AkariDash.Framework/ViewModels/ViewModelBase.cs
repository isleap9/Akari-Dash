using CommunityToolkit.Mvvm.ComponentModel;

namespace AkariDash.Framework.ViewModels;

/// <summary>
/// Base class for all view models.
/// Provides <see cref="ObservableObject"/> infrastructure plus common helpers.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private string? _title;

    /// <summary>
    /// Display title used by navigation / page headers.
    /// </summary>
    public string? Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
}
