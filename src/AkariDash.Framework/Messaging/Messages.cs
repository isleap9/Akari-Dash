using Microsoft.UI.Xaml.Controls;
using AkariDash.Framework.Services;

namespace AkariDash.Framework.Messaging;

/// <summary>Published when the application theme changes.</summary>
public sealed record ThemeChangedMessage(AppTheme Theme);

/// <summary>Published when the UI language changes.</summary>
public sealed record CultureChangedMessage(string CultureName);

/// <summary>Published to show / update a global <see cref="InfoBar"/>.</summary>
public sealed record ShowInfoBarMessage(
    string Title,
    string Message,
    InfoBarSeverity Severity,
    bool IsOpen = true);

/// <summary>Published to request typed navigation from anywhere in the app.</summary>
public sealed record NavigationRequestedMessage(Type PageType, object? Parameter = null);

/// <summary>Generic application event for loosely coupled view models.</summary>
public sealed record UserActionMessage(string Action, object? Payload = null);
