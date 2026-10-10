using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using AkariDash.App.Services;
using AkariDash.App.Views;
using AkariDash.Framework;
using AkariDash.Framework.Messaging;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using Windows.Graphics;
using Windows.UI;

namespace AkariDash.App;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private readonly IMessenger _messenger;

    public MainWindow(
        INavigationService navigation,
        IInfoBarService infoBar,
        IThemeService theme,
        IMessenger messenger,
        LocalizedStrings strings)
    {
        Strings = strings;
        InitializeComponent();

        _navigation = navigation;
        InfoBar = infoBar;
        _theme = theme;
        _messenger = messenger;

        Title = App.AppName;

        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureWindow();

        _navigation.SetFrame(ContentFrame);
        _navigation.NavigateTo<HomePage>();
        _navigation.Navigated += (_, _) => RefreshShellState();
        RefreshShellState();

        _messenger.Register<ThemeChangedMessage>(this, (r, m) => ((MainWindow)r).ApplyTheme(m.Theme));
        _messenger.Register<NavigationRequestedMessage>(this, (r, m) =>
            ((MainWindow)r)._navigation.NavigateTo(m.PageType, m.Parameter));
    }

    /// <summary>Localized string accessor used by x:Bind function bindings.</summary>
    public LocalizedStrings Strings { get; }

    /// <summary>Whether the persistent "Dry Run only" banner is shown (Phase 1 builds).</summary>
    public bool IsDryRunOnly => BuildInfo.IsDryRunOnly;

    /// <summary>Global info-bar state bound by the shell.</summary>
    public IInfoBarService InfoBar { get; }

    /// <summary>App name shown in the custom title bar.</summary>
    public string AppTitle => App.AppName;

    /// <summary>App icon shown in the custom title bar.</summary>
    public ImageSource AppIconSource { get; } = LoadAppIcon();

    private static ImageSource LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png");
        return File.Exists(path) ? new BitmapImage(new Uri(path)) : null!;
    }

    /// <summary>Navigation items for the shell NavigationView (top of the pane).</summary>
    public IReadOnlyList<NavigationItem> NavItems { get; } =
    [
        new("Home", "\uE80F", typeof(HomePage)),
        new("Gaming", "\uE7FC", typeof(GamingPage)),
        new("Privacy", "\uE72E", typeof(PrivacyPage)),
        new("Services", "\uE9F5", typeof(ServicesPage)),
    ];

    /// <summary>Navigation items pinned to the bottom of the pane (footer).</summary>
    public IReadOnlyList<NavigationItem> FooterNavItems { get; } =
    [
        new("Settings", "\uE713", typeof(SettingsPage)),
    ];

    /// <summary>Applies an application theme to this window's content and title bar.</summary>
    public void ApplyTheme(AppTheme theme)
    {
        RootElement.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        ApplyTitleBarColors(theme);
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Type? pageType = null;

        switch (args.SelectedItem)
        {
            case NavigationItem item:
                pageType = item.PageType;
                break;
            case NavigationViewItem navItem when navItem.Tag is Type type:
                pageType = type;
                break;
        }

        if (pageType is not null && pageType != _navigation.CurrentPageType)
        {
            _navigation.NavigateTo(pageType);
        }
    }

    private void RefreshShellState()
    {
        var current = _navigation.CurrentPageType;
        var item = NavItems.Concat(FooterNavItems).FirstOrDefault(i => i.PageType == current);

        if (item is not null && !ReferenceEquals(NavView.SelectedItem, item))
        {
            NavView.SelectedItem = item;
        }
    }

    private void ConfigureWindow()
    {
        var appWindow = GetAppWindow();
        if (appWindow is null)
        {
            return;
        }

        try
        {
            var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
            var width = Math.Min(1150, workArea.Width - 60);
            var height = Math.Min(800, workArea.Height - 80);
            appWindow.Resize(new SizeInt32((int)width, (int)height));

            appWindow.Move(new PointInt32(
                workArea.X + (workArea.Width - (int)width) / 2,
                workArea.Y + (workArea.Height - (int)height) / 2));

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Window configuration failed: {ex.Message}");
        }

        ApplyTitleBarColors(_theme.CurrentTheme);
    }

    private AppWindow? GetAppWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void ApplyTitleBarColors(AppTheme theme)
    {
        var appWindow = GetAppWindow();
        if (appWindow?.TitleBar is null)
        {
            return;
        }

        var isDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => RootElement.ActualTheme == ElementTheme.Dark,
        };

        var foreground = isDark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        var hoverBackground = isDark ? Microsoft.UI.Colors.Gray : Microsoft.UI.Colors.Transparent;

        appWindow.TitleBar.ForegroundColor = foreground;
        appWindow.TitleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
        appWindow.TitleBar.ButtonForegroundColor = foreground;
        appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        appWindow.TitleBar.ButtonHoverForegroundColor = foreground;
        appWindow.TitleBar.ButtonHoverBackgroundColor = hoverBackground;
        appWindow.TitleBar.ButtonPressedBackgroundColor = hoverBackground;
    }
}
