using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AkariDash.Framework.Navigation;
using Xunit;

namespace AkariDash.Tests;

public abstract class AbstractTestPage : Page
{
}

public class NavigationTests
{
    [Fact]
    public void Constructor_rejects_null_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new FrameNavigationService(null!));
    }

    [Fact]
    public void SetFrame_rejects_null()
    {
        var service = new FrameNavigationService(_ => throw new InvalidOperationException());
        Assert.Throws<ArgumentNullException>(() => service.SetFrame(null!));
    }

    [Fact]
    public void NavigateTo_rejects_null_page_type()
    {
        var service = new FrameNavigationService(_ => throw new InvalidOperationException());
        Assert.Throws<ArgumentNullException>(() => service.NavigateTo(null!));
    }

    [Fact]
    public void NavigateTo_rejects_non_page_type()
    {
        var service = new FrameNavigationService(_ => throw new InvalidOperationException());
        var ex = Assert.Throws<ArgumentException>(() => service.NavigateTo(typeof(string)));
        Assert.Equal("pageType", ex.ParamName);
    }

    [Fact]
    public void NavigateTo_throws_when_factory_returns_null()
    {
        var service = new FrameNavigationService(_ => null!);

        var ex = Assert.Throws<InvalidOperationException>(() => service.NavigateTo(typeof(AbstractTestPage)));
        Assert.Contains(nameof(AbstractTestPage), ex.Message);

        Assert.Null(service.CurrentPageType);
        Assert.Null(service.CurrentParameter);
        Assert.False(service.CanGoBack);
    }

    [Fact]
    public void Initial_state_is_empty()
    {
        var service = new FrameNavigationService(_ => throw new InvalidOperationException());

        Assert.False(service.CanGoBack);
        Assert.False(service.CanGoForward);
        Assert.Null(service.CurrentPageType);
        Assert.Null(service.CurrentParameter);
    }

    [Fact]
    public void Stack_operations_on_empty_history_are_noops()
    {
        var service = new FrameNavigationService(_ => throw new InvalidOperationException());

        service.GoBack();
        service.GoForward();
        service.ClearHistory();
        service.RemoveBackEntry();

        Assert.False(service.CanGoBack);
        Assert.False(service.CanGoForward);
    }

    [Fact]
    public void NavigationEntry_is_a_value_record()
    {
        var entry = new NavigationEntry(typeof(AbstractTestPage), "payload");

        Assert.Equal(typeof(AbstractTestPage), entry.PageType);
        Assert.Equal("payload", entry.Parameter);
        Assert.Equal(new NavigationEntry(typeof(AbstractTestPage), "payload"), entry);
        Assert.NotEqual(new NavigationEntry(typeof(AbstractTestPage), "other"), entry);
    }

    [Fact]
    public void NavigationCompletedEventArgs_is_a_value_record()
    {
        var args = new NavigationCompletedEventArgs(typeof(AbstractTestPage), "payload", NavigationMode.New);

        Assert.Equal(new NavigationCompletedEventArgs(typeof(AbstractTestPage), "payload", NavigationMode.New), args);
        Assert.NotEqual(
            new NavigationCompletedEventArgs(typeof(AbstractTestPage), "payload", NavigationMode.Back),
            args);
    }
}
