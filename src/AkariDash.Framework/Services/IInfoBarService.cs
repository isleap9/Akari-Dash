using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using AkariDash.Framework.Messaging;

namespace AkariDash.Framework.Services;

/// <summary>
/// Observable state object that drives a global <see cref="InfoBar"/> in the app shell.
/// Also publishes <see cref="ShowInfoBarMessage"/> so view models can trigger it without
/// a direct reference to the service.
/// </summary>
public interface IInfoBarService
{
    bool IsOpen { get; set; }
    string Title { get; set; }
    string Message { get; set; }
    InfoBarSeverity Severity { get; set; }
    bool IsClosable { get; set; }

    void Show(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational);
    void ShowInfo(string title, string message);
    void ShowSuccess(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    void Hide();
}

public partial class InfoBarService : ObservableObject, IInfoBarService
{
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity Severity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsClosable { get; set; } = true;

    public void Show(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        Title = title;
        Message = message;
        Severity = severity;
        IsOpen = true;
    }

    public void ShowInfo(string title, string message) => Show(title, message, InfoBarSeverity.Informational);

    public void ShowSuccess(string title, string message) => Show(title, message, InfoBarSeverity.Success);

    public void ShowWarning(string title, string message) => Show(title, message, InfoBarSeverity.Warning);

    public void ShowError(string title, string message) => Show(title, message, InfoBarSeverity.Error);

    public void Hide() => IsOpen = false;
}
