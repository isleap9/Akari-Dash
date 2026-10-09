using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using AkariDash.Framework.Messaging;
using AkariDash.Framework.Services;

namespace AkariDash.Framework.Services;

/// <summary>
/// Switches and persists the app's UI culture. Raises <see cref="CultureChangedMessage"/>
/// so localized views can re-resolve their resources.
/// </summary>
public interface ICultureService
{
    CultureInfo CurrentCulture { get; }

    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    Task InitializeAsync();

    Task SetCultureAsync(CultureInfo culture);
}

public sealed class CultureService : ICultureService
{
    private const string SettingsKey = "Appearance.Culture";

    private static readonly CultureInfo[] Supported =
    [
        new("en-US"),
        new("zh-CN"),
    ];

    private readonly ISettingsService _settings;
    private readonly IMessenger _messenger;

    public CultureService(ISettingsService settings, IMessenger messenger)
    {
        _settings = settings;
        _messenger = messenger;
    }

    public CultureInfo CurrentCulture { get; private set; } = Supported[0];

    public IReadOnlyList<CultureInfo> SupportedCultures => Supported;

    public async Task InitializeAsync()
    {
        var name = await _settings.GetAsync(SettingsKey, Supported[0].Name);
        CurrentCulture = Supported.FirstOrDefault(c => c.Name == name) ?? Supported[0];
        ApplyCulture(CurrentCulture);
    }

    public async Task SetCultureAsync(CultureInfo culture)
    {
        CurrentCulture = culture;
        await _settings.SetAsync(SettingsKey, culture.Name);
        ApplyCulture(culture);
        _messenger.Send(new CultureChangedMessage(culture.Name));
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
