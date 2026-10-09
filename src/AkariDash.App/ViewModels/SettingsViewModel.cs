using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using AkariDash.Framework;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IThemeService _theme;
    private readonly ICultureService _culture;

    private AppTheme _selectedTheme = AppTheme.System;
    private CultureInfo _selectedCulture;

    public SettingsViewModel(IThemeService theme, ICultureService culture)
    {
        _theme = theme;
        _culture = culture;
        _selectedCulture = culture.SupportedCultures[0];
        Title = "Settings";
    }

    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    public IReadOnlyList<CultureInfo> Cultures => _culture.SupportedCultures;

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

    /// <summary>Culture picked in the UI; applies immediately through the culture service.</summary>
    public CultureInfo SelectedCulture
    {
        get => _selectedCulture;
        set
        {
            if (SetProperty(ref _selectedCulture, value))
            {
                _ = _culture.SetCultureAsync(value);
            }
        }
    }

    /// <summary>Loads persisted settings; called once by the page when it becomes visible.</summary>
    public async Task InitializeAsync()
    {
        await _theme.InitializeAsync();
        await _culture.InitializeAsync();
        _selectedTheme = _theme.CurrentTheme;
        _selectedCulture = _culture.CurrentCulture;
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedCulture));
    }
}
