using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using AkariDash.Framework;
using AkariDash.Framework.Messaging;
using AkariDash.Framework.Services;
using Xunit;

namespace AkariDash.Tests;

public class ServicesTests
{
    private readonly IMessenger _messenger = WeakReferenceMessenger.Default;
    private readonly MemorySettingsStorage _storage = new();
    private readonly ISettingsService _settings;

    public ServicesTests()
    {
        _settings = new SettingsService(_storage);
    }

    [Fact]
    public async Task ThemeService_initialize_uses_default()
    {
        var service = new ThemeService(_settings, _messenger);

        await service.InitializeAsync();

        Assert.Equal(AppTheme.System, service.CurrentTheme);
    }

    [Fact]
    public async Task ThemeService_initialize_loads_persisted_theme()
    {
        await _settings.SetAsync("Appearance.Theme", AppTheme.Dark);
        var service = new ThemeService(_settings, _messenger);

        await service.InitializeAsync();

        Assert.Equal(AppTheme.Dark, service.CurrentTheme);
    }

    [Fact]
    public async Task ThemeService_set_theme_persists_and_publishes_message()
    {
        var service = new ThemeService(_settings, _messenger);
        ThemeChangedMessage? received = null;
        _messenger.Register<ThemeChangedMessage>(this, (_, m) => received = m);
        try
        {
            await service.SetThemeAsync(AppTheme.Dark);

            Assert.Equal(AppTheme.Dark, service.CurrentTheme);
            Assert.Equal(AppTheme.Dark, await _settings.GetAsync<AppTheme>("Appearance.Theme"));
            Assert.NotNull(received);
            Assert.Equal(AppTheme.Dark, received.Theme);
        }
        finally
        {
            _messenger.Unregister<ThemeChangedMessage>(this);
        }
    }

    [Fact]
    public async Task ThemeService_set_same_theme_is_noop()
    {
        var service = new ThemeService(_settings, _messenger);
        await service.SetThemeAsync(AppTheme.Light);
        var published = false;
        _messenger.Register<ThemeChangedMessage>(this, (_, _) => published = true);
        try
        {
            await service.SetThemeAsync(AppTheme.Light);

            Assert.False(published);
            Assert.Equal(AppTheme.Light, await _settings.GetAsync<AppTheme>("Appearance.Theme"));
        }
        finally
        {
            _messenger.Unregister<ThemeChangedMessage>(this);
        }
    }

    [Fact]
    public async Task ThemeService_toggle_cycles_light_dark()
    {
        var service = new ThemeService(_settings, _messenger);
        await service.SetThemeAsync(AppTheme.Light);

        service.Toggle();

        Assert.Equal(AppTheme.Dark, service.CurrentTheme);
    }

    [Fact]
    public async Task ThemeService_set_system_theme()
    {
        var service = new ThemeService(_settings, _messenger);
        await service.SetThemeAsync(AppTheme.Dark);

        await service.SetSystemThemeAsync();

        Assert.Equal(AppTheme.System, service.CurrentTheme);
        Assert.Equal(AppTheme.System, await _settings.GetAsync<AppTheme>("Appearance.Theme"));
    }

    [Fact]
    public void InfoBarService_show_sets_state()
    {
        var service = new InfoBarService();

        service.Show("Title", "Message", InfoBarSeverity.Error);

        Assert.True(service.IsOpen);
        Assert.Equal("Title", service.Title);
        Assert.Equal("Message", service.Message);
        Assert.Equal(InfoBarSeverity.Error, service.Severity);
    }

    [Theory]
    [InlineData(InfoBarSeverity.Informational)]
    [InlineData(InfoBarSeverity.Success)]
    [InlineData(InfoBarSeverity.Warning)]
    [InlineData(InfoBarSeverity.Error)]
    public void InfoBarService_show_variants_set_severity(InfoBarSeverity severity)
    {
        var service = new InfoBarService();

        switch (severity)
        {
            case InfoBarSeverity.Informational: service.ShowInfo("t", "m"); break;
            case InfoBarSeverity.Success: service.ShowSuccess("t", "m"); break;
            case InfoBarSeverity.Warning: service.ShowWarning("t", "m"); break;
            default: service.ShowError("t", "m"); break;
        }

        Assert.Equal(severity, service.Severity);
        Assert.True(service.IsOpen);
    }

    [Fact]
    public void InfoBarService_hide_closes()
    {
        var service = new InfoBarService();
        service.Show("t", "m");

        service.Hide();

        Assert.False(service.IsOpen);
    }

    [Fact]
    public void InfoBarService_raises_property_changed()
    {
        var service = new InfoBarService();
        var changed = new List<string?>();
        service.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        service.Show("t", "m", InfoBarSeverity.Success);

        Assert.Contains(nameof(service.Title), changed);
        Assert.Contains(nameof(service.Message), changed);
        Assert.Contains(nameof(service.Severity), changed);
        Assert.Contains(nameof(service.IsOpen), changed);
    }

    [Fact]
    public void AddMvvmFramework_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => ((ServiceCollection)null!).AddMvvmFramework());
    }

    [Fact]
    public void AddMvvmFramework_registers_services()
    {
        var services = new ServiceCollection();

        services.AddMvvmFramework();
        var provider = services.BuildServiceProvider();

        Assert.Same(WeakReferenceMessenger.Default, provider.GetRequiredService<IMessenger>());
        Assert.NotNull(provider.GetRequiredService<ISettingsStorage>());
        Assert.NotNull(provider.GetRequiredService<ISettingsService>());
        Assert.NotNull(provider.GetRequiredService<IThemeService>());
        Assert.NotNull(provider.GetRequiredService<IInfoBarService>());
        Assert.NotNull(provider.GetRequiredService<IWindowService>());
    }
}
