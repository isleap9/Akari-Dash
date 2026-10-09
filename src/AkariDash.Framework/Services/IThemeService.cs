using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using AkariDash.Framework.Messaging;
using AkariDash.Framework.Services;

namespace AkariDash.Framework.Services;

/// <summary>Supported application themes.</summary>
public enum AppTheme
{
    [Description("Light")]
    Light = 0,

    [Description("Dark")]
    Dark = 1,

    [Description("System default")]
    System = 2,
}

/// <summary>
/// Central service for switching and persisting the application theme.
/// Raises <see cref="ThemeChangedMessage"/> so windows / root elements can react.
/// </summary>
public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    Task InitializeAsync();

    Task SetThemeAsync(AppTheme theme);

    void Toggle();

    Task SetSystemThemeAsync();
}

public sealed class ThemeService : IThemeService
{
    private const string SettingsKey = "Appearance.Theme";
    private readonly ISettingsService _settings;
    private readonly IMessenger _messenger;

    public ThemeService(ISettingsService settings, IMessenger messenger)
    {
        _settings = settings;
        _messenger = messenger;
    }

    public AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    public async Task InitializeAsync()
    {
        CurrentTheme = await _settings.GetAsync(SettingsKey, AppTheme.System);
    }

    public async Task SetThemeAsync(AppTheme theme)
    {
        if (CurrentTheme == theme)
        {
            return;
        }

        CurrentTheme = theme;
        await _settings.SetAsync(SettingsKey, theme);
        _messenger.Send(new ThemeChangedMessage(theme));
    }

    public void Toggle()
    {
        var next = CurrentTheme switch
        {
            AppTheme.Light => AppTheme.Dark,
            AppTheme.Dark => AppTheme.Light,
            _ => AppTheme.System,
        };

        _ = SetThemeAsync(next);
    }

    public async Task SetSystemThemeAsync() => await SetThemeAsync(AppTheme.System);
}
