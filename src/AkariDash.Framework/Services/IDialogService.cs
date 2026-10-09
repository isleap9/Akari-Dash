using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AkariDash.Framework.Services;

/// <summary>
/// Shows <see cref="ContentDialog"/>s from view models without hard-coding a XamlRoot.
/// Dialogs are serialized so overlapping requests queue instead of failing.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a simple text dialog with an optional primary / secondary / cancel button.</summary>
    Task<ContentDialogResult> ShowAsync(
        string title,
        string content,
        string primaryText = "OK",
        string? secondaryText = null,
        string? cancelText = null);

    /// <summary>Shows a confirm/cancel dialog; true when the primary button was pressed.</summary>
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        string? cancelText = "Cancel");

    /// <summary>Shows an informational dialog.</summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>Shows an error dialog.</summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>Shows a pre-built dialog.</summary>
    Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog);

    /// <summary>
    /// Shows a dialog whose content is a view model-bound element of type <typeparamref name="T"/>.
    /// The element's <see cref="FrameworkElement.DataContext"/> is set to <paramref name="viewModel"/>.
    /// </summary>
    Task<ContentDialogResult> ShowDialogAsync<T>(
        object viewModel,
        string title,
        string primaryText,
        string? secondaryText = null,
        string? cancelText = null)
        where T : UIElement, new();
}

public sealed class DialogService : IDialogService
{
    private readonly Func<XamlRoot?> _xamlRootProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DialogService(Func<XamlRoot?> xamlRootProvider)
    {
        _xamlRootProvider = xamlRootProvider ?? throw new ArgumentNullException(nameof(xamlRootProvider));
    }

    public Task<ContentDialogResult> ShowAsync(
        string title,
        string content,
        string primaryText = "OK",
        string? secondaryText = null,
        string? cancelText = null)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryText,
            CloseButtonText = cancelText,
        };

        if (secondaryText is not null)
        {
            dialog.SecondaryButtonText = secondaryText;
        }

        return ShowDialogAsync(dialog);
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        string? cancelText = "Cancel")
    {
        var result = await ShowAsync(title, message, confirmText, cancelText: cancelText);
        return result == ContentDialogResult.Primary;
    }

    public Task ShowInfoAsync(string title, string message)
        => ShowAsync(title, message);

    public Task ShowErrorAsync(string title, string message)
        => ShowAsync(title, message, primaryText: "OK");

    public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var xamlRoot = _xamlRootProvider();
        if (xamlRoot is null)
        {
            throw new InvalidOperationException("No XamlRoot is available. Ensure the main window has been activated.");
        }

        dialog.XamlRoot = xamlRoot;

        await _gate.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ContentDialogResult> ShowDialogAsync<T>(
        object viewModel,
        string title,
        string primaryText,
        string? secondaryText = null,
        string? cancelText = null)
        where T : UIElement, new()
    {
        var content = new T();
        if (content is FrameworkElement frameworkElement)
        {
            frameworkElement.DataContext = viewModel;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryText,
            CloseButtonText = cancelText,
        };

        if (secondaryText is not null)
        {
            dialog.SecondaryButtonText = secondaryText;
        }

        return await ShowDialogAsync(dialog);
    }
}
