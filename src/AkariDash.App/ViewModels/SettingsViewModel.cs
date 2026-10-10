using CommunityToolkit.Mvvm.ComponentModel;
using AkariDash.Framework;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IThemeService _theme;

    private AppTheme _selectedTheme = AppTheme.System;

    public SettingsViewModel(IThemeService theme)
    {
        _theme = theme;
        Title = "Settings";
    }

    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    /// <summary>Theme picked in the UI; applies immediately through the theme service.</summary>
    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _ = _theme.SetThemeAsync(value);
            }
        }
    }

    /// <summary>Loads persisted settings; called once by the page when it becomes visible.</summary>
    public async Task InitializeAsync()
    {
        await _theme.InitializeAsync();
        _selectedTheme = _theme.CurrentTheme;
        OnPropertyChanged(nameof(SelectedTheme));
    }
}
