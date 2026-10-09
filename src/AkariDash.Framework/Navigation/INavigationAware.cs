namespace AkariDash.Framework.Navigation;

/// <summary>
/// Implemented by view models that want lifecycle callbacks as navigation happens.
/// </summary>
public interface INavigationAware
{
    /// <summary>Called when the page (and its view model) becomes the active page.</summary>
    void OnNavigatedTo(object? parameter);

    /// <summary>Called when the page is no longer the active page.</summary>
    void OnNavigatedFrom();
}
