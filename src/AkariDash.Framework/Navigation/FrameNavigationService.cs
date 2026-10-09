using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AkariDash.Framework.ViewModels;

namespace AkariDash.Framework.Navigation;

/// <summary>
/// Default <see cref="INavigationService"/> implementation.
/// <para>
/// Pages are created by an injected factory (typically resolving from the app's
/// dependency-injection container), so pages and view models may use constructor
/// injection. The service keeps its own back/forward stacks and raises
/// <see cref="INavigationAware"/> callbacks on the view models involved.
/// </para>
/// </summary>
public sealed class FrameNavigationService : INavigationService
{
    private readonly Func<Type, Page> _pageFactory;
    private readonly Stack<NavigationEntry> _backStack = new();
    private readonly Stack<NavigationEntry> _forwardStack = new();
    private Frame? _frame;

    public FrameNavigationService(Func<Type, Page> pageFactory)
    {
        _pageFactory = pageFactory ?? throw new ArgumentNullException(nameof(pageFactory));
    }

    public bool CanGoBack => _backStack.Count > 1;
    public bool CanGoForward => _forwardStack.Count > 0;
    public Type? CurrentPageType { get; private set; }
    public object? CurrentParameter { get; private set; }

    public event EventHandler<NavigationCompletedEventArgs>? Navigated;

    public void SetFrame(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    public void NavigateTo<T>(object? parameter = null, bool preserveStack = true)
        where T : Page => NavigateTo(typeof(T), parameter, preserveStack);

    public void NavigateTo(Type pageType, object? parameter = null, bool preserveStack = true)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (!typeof(Page).IsAssignableFrom(pageType))
        {
            throw new ArgumentException($"{pageType} is not a Page.", nameof(pageType));
        }

        var entry = new NavigationEntry(pageType, parameter);
        var page = CreatePage(pageType);

        GetDataContext(_frame?.Content as Page)?.OnNavigatedFrom();

        if (!preserveStack)
        {
            _backStack.Clear();
        }

        _backStack.Push(entry);
        _forwardStack.Clear();

        CurrentPageType = pageType;
        CurrentParameter = parameter;

        if (_frame is not null)
        {
            _frame.Content = page;
        }

        (GetDataContext(page))?.OnNavigatedTo(parameter);

        Navigated?.Invoke(this, new NavigationCompletedEventArgs(pageType, parameter, NavigationMode.New));
    }

    public void GoBack()
    {
        if (!CanGoBack || _backStack.Count == 0)
        {
            return;
        }

        var current = _backStack.Pop();
        _forwardStack.Push(current);

        var target = _backStack.Peek();
        Show(target, NavigationMode.Back);
    }

    public void GoForward()
    {
        if (!CanGoForward)
        {
            return;
        }

        var target = _forwardStack.Pop();
        _backStack.Push(target);

        Show(target, NavigationMode.Forward);
    }

    public void ClearHistory()
    {
        _backStack.Clear();
        _forwardStack.Clear();
    }

    public void RemoveBackEntry()
    {
        if (_backStack.Count > 1)
        {
            var current = _backStack.Pop();
            _backStack.Clear();
            _backStack.Push(current);
        }
    }

    private void Show(NavigationEntry entry, NavigationMode mode)
    {
        var page = CreatePage(entry.PageType);

        GetDataContext(_frame?.Content as Page)?.OnNavigatedFrom();

        CurrentPageType = entry.PageType;
        CurrentParameter = entry.Parameter;

        if (_frame is not null)
        {
            _frame.Content = page;
        }

        GetDataContext(page)?.OnNavigatedTo(entry.Parameter);

        Navigated?.Invoke(this, new NavigationCompletedEventArgs(entry.PageType, entry.Parameter, mode));
    }

    private Page CreatePage(Type pageType)
    {
        var page = _pageFactory(pageType);
        if (page is null)
        {
            throw new InvalidOperationException($"The page factory returned null for '{pageType}'.");
        }

        return page;
    }

    private static INavigationAware? GetDataContext(Page? page) => page?.DataContext as INavigationAware;
}
