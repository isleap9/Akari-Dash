using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AkariDash.Framework.Navigation;

/// <summary>
/// Raised after a navigation completes.
/// </summary>
public sealed record NavigationCompletedEventArgs(
    Type PageType,
    object? Parameter,
    NavigationMode Mode);

/// <summary>
/// Typed navigation over a WinUI <see cref="Frame"/> with dependency-injected pages,
/// view-model lifecycle callbacks and an explicit back/forward stack.
/// </summary>
public interface INavigationService
{
    bool CanGoBack { get; }
    bool CanGoForward { get; }

    /// <summary>The page type currently displayed.</summary>
    Type? CurrentPageType { get; }

    /// <summary>The parameter passed to the current page.</summary>
    object? CurrentParameter { get; }

    /// <summary>Occurs after navigation completes.</summary>
    event EventHandler<NavigationCompletedEventArgs>? Navigated;

    /// <summary>Binds the service to the frame that will host the pages.</summary>
    void SetFrame(Frame frame);

    /// <summary>Navigates to the page, optionally passing a parameter.</summary>
    void NavigateTo<T>(object? parameter = null, bool preserveStack = true) where T : Page;

    /// <summary>Navigates to the page, optionally passing a parameter.</summary>
    void NavigateTo(Type pageType, object? parameter = null, bool preserveStack = true);

    /// <summary>Navigates back, if possible.</summary>
    void GoBack();

    /// <summary>Navigates forward, if possible.</summary>
    void GoForward();

    /// <summary>Clears the back and forward stacks.</summary>
    void ClearHistory();

    /// <summary>Removes the most recent back entry.</summary>
    void RemoveBackEntry();
}
