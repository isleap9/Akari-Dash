using System.ComponentModel;
using System.Resources;
using CommunityToolkit.Mvvm.Messaging;

namespace AkariDash.App.Services;

/// <summary>
/// Localized string accessor. Exposes resources by key and raises
/// <see cref="INotifyPropertyChanged"/> when the culture changes so that
/// x:Bind function bindings (e.g. <c>{x:Bind Strings.Get("Key")}</c>) re-evaluate.
/// </summary>
public sealed class LocalizedStrings : INotifyPropertyChanged
{
    private readonly ResourceManager _resources = new(
        "AkariDash.App.Resources.Resources",
        typeof(LocalizedStrings).Assembly);

    /// <summary>Returns the localized string for <paramref name="key"/> (or the key itself when missing).</summary>
    public string Get(string key) => _resources.GetString(key) ?? key;

    /// <summary>Indexer form: <c>Strings["Key"]</c>.</summary>
    public string this[string key] => Get(key);

    /// <summary>Raised when resources should be re-read (culture change).</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Tells all bound elements to re-resolve their localized text.</summary>
    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
