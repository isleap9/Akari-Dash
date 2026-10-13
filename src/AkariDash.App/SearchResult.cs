using AkariDash.Core.Tweaks;

namespace AkariDash.App;

/// <summary>
/// One suggestion in the shell's search box: a Tweak, where it lives, and the page to go to.
/// </summary>
/// <param name="Title">The Tweak's own title.</param>
/// <param name="Detail">Its Category and Group, plus the value's name when that is what matched.</param>
/// <param name="TweakId">Passed to the page so it can scroll the Tweak into view and point it out.</param>
public sealed record SearchResult(string Title, string Detail, string TweakId, Type PageType)
{
    public static SearchResult From(TweakMatch match, Type pageType) =>
        new(match.Tweak.Title, match.Detail, match.Tweak.Id, pageType);

    /// <summary>What the box shows once a suggestion is picked.</summary>
    public override string ToString() => Title;
}
