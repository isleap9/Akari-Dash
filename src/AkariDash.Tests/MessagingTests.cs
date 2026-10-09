using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using AkariDash.Framework.Messaging;
using AkariDash.Framework.Services;
using Xunit;

namespace AkariDash.Tests;

public class MessagingTests
{
    private readonly IMessenger _messenger = WeakReferenceMessenger.Default;

    [Fact]
    public void ThemeChangedMessage_is_value_record()
    {
        Assert.Equal(new ThemeChangedMessage(AppTheme.Dark), new ThemeChangedMessage(AppTheme.Dark));
        Assert.NotEqual(new ThemeChangedMessage(AppTheme.Dark), new ThemeChangedMessage(AppTheme.Light));
    }

    [Fact]
    public void NavigationRequestedMessage_is_value_record()
    {
        var pageType = typeof(object);

        Assert.Equal(
            new NavigationRequestedMessage(pageType, "p"),
            new NavigationRequestedMessage(pageType, "p"));
        Assert.NotEqual(
            new NavigationRequestedMessage(pageType, "p"),
            new NavigationRequestedMessage(pageType, null));
    }

    [Fact]
    public void ShowInfoBarMessage_is_value_record()
    {
        var message = new ShowInfoBarMessage("Title", "Body", InfoBarSeverity.Warning, IsOpen: true);

        Assert.Equal("Title", message.Title);
        Assert.Equal("Body", message.Message);
        Assert.Equal(InfoBarSeverity.Warning, message.Severity);
        Assert.True(message.IsOpen);
    }

    [Fact]
    public void UserActionMessage_is_value_record()
    {
        var message = new UserActionMessage("navigate", "home");

        Assert.Equal("navigate", message.Action);
        Assert.Equal("home", message.Payload);
        Assert.Equal(new UserActionMessage("navigate", "home"), message);
    }

    [Fact]
    public void Messenger_sends_and_receives_user_action()
    {
        UserActionMessage? received = null;
        _messenger.Register<UserActionMessage>(this, (_, m) => received = m);
        try
        {
            _messenger.Send(new UserActionMessage("refresh"));

            Assert.NotNull(received);
            Assert.Equal("refresh", received.Action);
        }
        finally
        {
            _messenger.Unregister<UserActionMessage>(this);
        }
    }

    [Fact]
    public void Messenger_unregister_stops_delivery()
    {
        var delivered = false;
        _messenger.Register<UserActionMessage>(this, (_, _) => delivered = true);
        _messenger.Unregister<UserActionMessage>(this);

        _messenger.Send(new UserActionMessage("x"));

        Assert.False(delivered);
    }
}
