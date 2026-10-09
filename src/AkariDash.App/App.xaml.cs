using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using AkariDash.App.Services;
using AkariDash.Framework.Logging;
using AkariDash.App.ViewModels;
using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using AkariDash.Framework;
using AkariDash.Framework.Messaging;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;

namespace AkariDash.App;

public partial class App : Application
{
    private IHost? _host;

    /// <summary>Global service provider, usable from XAML bindings and non-DI code.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>The primary application window.</summary>
    public static MainWindow? MainWindow { get; private set; }

    public static string AppName => "Akari-Dash";

    public static string AppVersion =>
        typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>Folder and file used for persisted JSON settings.</summary>
    public static string SettingsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Akari-Dash");

    public static string SettingsFilePath => Path.Combine(SettingsFolder, "settings.json");

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        // Single-instance: only the first process becomes the primary instance.
        // Duplicate launches forward their activation to it and exit silently.
        var mainInstance = AppInstance.FindOrRegisterForKey("AkariDash");
        if (!mainInstance.IsCurrent)
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            mainInstance.RedirectActivationToAsync(activation).GetAwaiter().GetResult();
            Environment.Exit(0);
            return;
        }

        // When a duplicate launch is redirected here, bring the existing window to the front.
        mainInstance.Activated += (_, _) => MainWindow?.Activate();

        _host = BuildHost();
        Services = _host.Services;

        Services.GetRequiredService<ILogger<App>>().LogInformation(
            BuildInfo.IsDryRunOnly
                ? "{App} {Version} started (Phase 1: Dry Run only, Original Values kept in memory)"
                : "{App} {Version} started (real writes, Original Values in {Folder})",
            AppName, AppVersion, FileOriginalValuesStore.DefaultFolder);

        // Log crashes that happen off the UI thread (WinUI's UnhandledException only
        // covers the UI thread).
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var messenger = Services.GetRequiredService<IMessenger>();

        // Re-resolve localized strings whenever the culture changes.
        var localizer = Services.GetRequiredService<LocalizedStrings>();
        messenger.Register<CultureChangedMessage>(localizer, (r, _) => ((LocalizedStrings)r).Refresh());

        // Create and show the main window (it wires itself into navigation and theme).
        MainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow.Closed += (_, _) => Shutdown();
        MainWindow.Activate();

        DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
        {
            var cultureService = Services.GetRequiredService<ICultureService>();
            var themeService = Services.GetRequiredService<IThemeService>();

            await cultureService.InitializeAsync();
            await themeService.InitializeAsync();

            MainWindow?.ApplyTheme(themeService.CurrentTheme);
        });
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(SettingsFolder, "logs")));

        // Framework services (settings, theme, culture, dialogs, windows, pickers, info bar).
        builder.Services.AddMvvmFramework();

        // App services.
        builder.Services.AddSingleton<LocalizedStrings>();

        // Tweak engine: Live State is always read from the real machine.
        builder.Services.AddSingleton<IMachine, WindowsMachine>();
#if PHASE1
        // Phase 1: every apply and Undo is a Dry Run recorded for this session, and Original
        // Values live only as long as the session, so nothing is written to the machine or disk.
        builder.Services.AddSingleton(sp => new DryRunMachine(sp.GetRequiredService<IMachine>()));
        builder.Services.AddSingleton(sp => new TweakEngine(
            sp.GetRequiredService<IMachine>(),
            new InMemoryOriginalValuesStore(),
            applyTo: sp.GetRequiredService<DryRunMachine>()));
#else
        // Real build (VM only): applies write to the machine; Original Values persist under ProgramData.
        builder.Services.AddSingleton(sp => new TweakEngine(
            sp.GetRequiredService<IMachine>(),
            new FileOriginalValuesStore(FileOriginalValuesStore.DefaultFolder)));
#endif

        // Persist settings under the app's own folder.
        builder.Services.AddSingleton<ISettingsStorage>(new FileSettingsStorage("Akari-Dash"));

        // Main window.
        builder.Services.AddSingleton<MainWindow>();

        // View models.
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<CategoryViewModel>();

        // Navigation: pages are created through the DI container.
        builder.Services.AddSingleton<INavigationService>(sp =>
            new FrameNavigationService(pageType => (Page)ActivatorUtilities.CreateInstance(sp, pageType)));

        // Infrastructure providers consumed by framework services.
        builder.Services.AddSingleton(sp => new Func<XamlRoot?>(() => MainWindow?.Content?.XamlRoot));
        builder.Services.AddSingleton(sp => new Func<IntPtr>(() =>
            MainWindow is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(MainWindow)));

        return builder.Build();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Services?.GetService<ILogger<App>>()?.LogError(e.Exception, "Unhandled application exception");

        // If the window isn't shown yet there is nowhere to display a dialog,
        // so fall back to the normal OS crash handling.
        if (MainWindow?.Content?.XamlRoot is null)
        {
            return;
        }

        // Suppress termination so the dialog can be shown, then exit deliberately.
        e.Handled = true;

        DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
        {
            try
            {
                var dialogService = Services!.GetRequiredService<IDialogService>();
                await dialogService.ShowErrorAsync(
                    "Something went wrong",
                    $"The app ran into an unexpected error and needs to close.{Environment.NewLine}{Environment.NewLine}Details were logged to:{Environment.NewLine}{Path.Combine(SettingsFolder, "logs")}");
            }
            catch
            {
                // Never re-enter the crash handler from the dialog itself.
            }
            finally
            {
                Shutdown();
            }
        });
    }

    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Services?.GetService<ILogger<App>>()?.LogError(exception, "AppDomain unhandled exception");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Services?.GetService<ILogger<App>>()?.LogError(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    /// <summary>Disposes the DI host (flushing loggers) and terminates the app.</summary>
    private void Shutdown()
    {
        try
        {
            _host?.Dispose();
        }
        catch
        {
            // A failing service Dispose must never prevent the app from exiting.
        }

        _host = null;
        Exit();
    }
}
