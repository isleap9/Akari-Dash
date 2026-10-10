using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;

namespace AkariDash.Framework;

/// <summary>
/// Extension methods for registering the MVVM framework services in a DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the framework services: settings, theme, dialogs, windows,
    /// file pickers, info bar and the community toolkit messenger.
    /// <para>
    /// The app must additionally register:
    /// <list type="bullet">
    /// <item><c>Func&lt;XamlRoot?&gt;</c> (for <see cref="DialogService"/>)</item>
    /// <item><c>Func&lt;IntPtr&gt;</c> (for <see cref="FilePickerService"/>)</item>
    /// <item><see cref="INavigationService"/> with its page factory</item>
    /// </list>
    /// </para>
    /// </summary>
    public static IServiceCollection AddMvvmFramework(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        services.AddSingleton<ISettingsStorage, FileSettingsStorage>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IInfoBarService, InfoBarService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();

        return services;
    }
}
