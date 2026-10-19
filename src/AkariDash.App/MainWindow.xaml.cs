using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using AkariDash.App.Services;
using AkariDash.App.Views;
using AkariDash.Core.Tweaks;
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
        IStatusService status,
        LocalizedStrings strings)
    {
        Strings = strings;
        Status = status;
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

        RootElement.ActualThemeChanged += (_, _) => UpdatePageLogo();

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

    /// <summary>The status bar's left side: Ready, or what is running with a progress bar.</summary>
    public IStatusService Status { get; }

    /// <summary>The status bar's right-hand stamp.</summary>
    public string BuildStamp => $"WinUI 3 · .NET 10 · {App.AppName} {App.AppVersion}";

    /// <summary>App icon shown in the custom title bar.</summary>
    public ImageSource AppIconSource { get; } = LoadAppIcon();

    private static ImageSource LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png");
        return File.Exists(path) ? new BitmapImage(new Uri(path)) : null!;
    }

    /// <summary>Navigation items for the shell NavigationView (top of the pane).</summary>
    public IReadOnlyList<NavigationItem> NavItems => ShellPages.Main;

    /// <summary>The page each Category's Tweaks live on, so a search result knows where to go.</summary>
    private static readonly Dictionary<Category, Type> PagesByCategory = new()
    {
        [Category.Gaming] = typeof(GamingPage),
        [Category.Privacy] = typeof(PrivacyPage),
        [Category.Services] = typeof(ServicesPage),
        [Category.Notifications] = typeof(NotificationsPage),
        [Category.StartMenu] = typeof(CustomizePage),
        [Category.Taskbar] = typeof(CustomizePage),
        [Category.Explorer] = typeof(CustomizePage),
        [Category.Windows] = typeof(WindowsPage),
        [Category.Update] = typeof(UpdatePage),
        [Category.Security] = typeof(SecurityPage),
    };

    // Ctrl+F puts the caret in the search box, wherever the user is.
    private void OnFindInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox.Focus(FocusState.Programmatic);
        args.Handled = true;
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Only when the user typed: the control also raises this when it fills the box in itself.
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        sender.ItemsSource = TweakSearch.Find(sender.Text)
            .Where(match => PagesByCategory.ContainsKey(match.Tweak.Category))
            .Select(match => SearchResult.From(match, PagesByCategory[match.Tweak.Category]))
            .ToList();
    }

    private void OnSearchSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchResult result)
        {
            GoTo(result);
        }
    }

    // Enter with nothing picked goes to the best match, so searching never needs the mouse.
    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchResult chosen)
        {
            GoTo(chosen);
            return;
        }

        if (sender.ItemsSource is IReadOnlyList<SearchResult> { Count: > 0 } results)
        {
            GoTo(results[0]);
        }
    }

    /// <summary>Goes to the Tweak's page, passing its id so the page can scroll to it.</summary>
    private void GoTo(SearchResult result)
    {
        _navigation.NavigateTo(result.PageType, result.TweakId);
        RefreshShellState();

        // The query stays in the box, so the next search starts from a clean one.
        SearchBox.Text = string.Empty;
        SearchBox.ItemsSource = null;
    }

    /// <summary>Navigation items pinned to the bottom of the pane (footer).</summary>
    public IReadOnlyList<NavigationItem> FooterNavItems => ShellPages.Footer;

    /// <summary>Applies an application theme to this window's content and title bar.</summary>
    public void ApplyTheme(AppTheme theme)
    {
        // The Akari theme is Dark with Themes/AkariTheme.xaml merged on top; every other theme goes
        // without it, so Dark stays Windows' own dark.
        var merged = Application.Current.Resources.MergedDictionaries;
        var akari = merged.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.EndsWith("/Themes/AkariTheme.xaml", StringComparison.OrdinalIgnoreCase) == true);
        if (theme == AppTheme.Akari && akari is null)
        {
            merged.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/AkariTheme.xaml") });
        }
        else if (theme != AppTheme.Akari && akari is not null)
        {
            merged.Remove(akari);
        }

        var target = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark or AppTheme.Akari => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        // {ThemeResource} brushes re-resolve only when the theme changes, so going between Dark and
        // Akari (both Dark underneath) passes through Light to make them pick up the dictionary.
        RootElement.RequestedTheme = target == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
        RootElement.RequestedTheme = target;

        ApplyTitleBarColors(theme);
        UpdatePageLogo();
    }

    // The logo is white, so it shows only on a dark page; System can be either.
    private void UpdatePageLogo() =>
        PageLogo.Visibility = RootElement.ActualTheme == ElementTheme.Light ? Visibility.Collapsed : Visibility.Visible;

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Type? pageType = null;
        object? parameter = null;

        switch (args.SelectedItem)
        {
            case NavigationItem item:
                pageType = item.PageType;
                parameter = item.Parameter;
                break;
            case NavigationViewItem navItem when navItem.Tag is Type type:
                pageType = type;
                break;
        }

        // A GPU entry shares its page with the other cards, so the card it carries decides too.
        if (pageType is not null &&
            (pageType != _navigation.CurrentPageType || (parameter is not null && !Equals(parameter, _navigation.CurrentParameter))))
        {
            _navigation.NavigateTo(pageType, parameter);
        }
    }

    private void RefreshShellState()
    {
        var current = _navigation.CurrentPageType;

        // Every level, so a page under Customize is selected in the pane like any other.
        var item = NavItems.Concat(FooterNavItems)
            .SelectMany(top => top.SelfAndDescendants())
            .FirstOrDefault(i => i.PageType == current && (i.Parameter is null || Equals(i.Parameter, _navigation.CurrentParameter)));

        if (item is not null)
        {
            HeaderIcon.Glyph = item.Glyph;
            HeaderTitle.Text = item.Label;
            HeaderSubtitle.Text = item.Subtitle;
        }

        if (item is null || ReferenceEquals(NavView.SelectedItem, item))
        {
            return;
        }

        // Branches start closed, and a closed branch cannot show its selected child. So when the user
        // lands on a page under one another way - a search result, mostly - open the branch first.
        if (NavItems.FirstOrDefault(parent => parent.Children?.Contains(item) == true) is { } branch &&
            NavView.ContainerFromMenuItem(branch) is NavigationViewItem { IsExpanded: false } container)
        {
            container.IsExpanded = true;
        }

        NavView.SelectedItem = item;
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
            // About two thirds of the screen, so a page's tabs and tables have room without the window
            // taking over a 1920 x 1080 screen (about 1300 x 850 there): 68% of the width and 82% of
            // the height of the work area, but never under 1100 x 720 where the screen has room.
            var width = Math.Min(Math.Max(1100, workArea.Width * 68 / 100), workArea.Width - 40);
            var height = Math.Min(Math.Max(720, workArea.Height * 82 / 100), workArea.Height - 40);
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
            AppTheme.Dark or AppTheme.Akari => true,
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
