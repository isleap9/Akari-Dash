using System.Resources;

namespace AkariDash.App.Services;

/// <summary>
/// The app's user-facing text, kept in Resources.resx (English only). Pages read it through
/// x:Bind function bindings, e.g. <c>{x:Bind Strings.Get('Key')}</c>.
/// </summary>
public sealed class LocalizedStrings
{
    private readonly ResourceManager _resources = new(
        "AkariDash.App.Resources.Resources",
        typeof(LocalizedStrings).Assembly);

    /// <summary>Returns the string for <paramref name="key"/> (or the key itself when missing).</summary>
    public string Get(string key) => _resources.GetString(key) ?? key;

    /// <summary>Indexer form: <c>Strings["Key"]</c>.</summary>
    public string this[string key] => Get(key);
}
