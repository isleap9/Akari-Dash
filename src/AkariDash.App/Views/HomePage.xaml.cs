using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariDash.App.Services;
using AkariDash.App.ViewModels;
using AkariDash.Framework.Services;

namespace AkariDash.App.Views;

public sealed partial class HomePage : Page
{
    private const string PaletteGlyph = "";
    private const string CloseGlyph = "";

    private readonly IThemeService _theme;

    // Each theme's button and where it sits when the fan is open: a quarter circle from straight
    // above the palette button round to its left.
    private readonly (Button Button, AppTheme Theme, Vector3 Open)[] _fan;

    private bool _fanOpen;

    /// <summary>Localized string accessor used by x:Bind function bindings.</summary>
    public LocalizedStrings Strings { get; }

    public HomeViewModel ViewModel { get; }

    public HomePage(HomeViewModel viewModel)
    {
        Strings = App.Services.GetRequiredService<LocalizedStrings>();
        _theme = App.Services.GetRequiredService<IThemeService>();
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;

        _fan =
        [
            (LightThemeButton, AppTheme.Light, new Vector3(0, -100, 0)),
            (AkariThemeButton, AppTheme.Akari, new Vector3(-50, -87, 0)),
            (SystemThemeButton, AppTheme.System, new Vector3(-87, -50, 0)),
            (DarkThemeButton, AppTheme.Dark, new Vector3(-100, 0, 0)),
        ];
    }

    private void OnThemeFanClick(object sender, RoutedEventArgs e) => SetFanOpen(!_fanOpen);

    private async void OnThemeButtonClick(object sender, RoutedEventArgs e)
    {
        var picked = _fan.First(entry => entry.Button == sender).Theme;
        SetFanOpen(false);
        await _theme.SetThemeAsync(picked);
    }

    private void SetFanOpen(bool open)
    {
        _fanOpen = open;
        ThemeFanGlyph.Glyph = open ? CloseGlyph : PaletteGlyph;

        foreach (var (button, theme, offset) in _fan)
        {
            button.Translation = open ? offset : Vector3.Zero;
            button.Opacity = open ? 1 : 0;
            button.IsHitTestVisible = open;

            // The theme in use is ringed in the accent colour.
            button.BorderThickness = new Thickness(theme == _theme.CurrentTheme ? 2 : 0);
        }
    }
}
