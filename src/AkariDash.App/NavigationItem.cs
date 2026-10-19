namespace AkariDash.App;

/// <summary>
/// A single entry in the main NavigationView. An item with <paramref name="Children"/> is a parent
/// that expands into them and has no page of its own, so <paramref name="PageType"/> is null and
/// selecting it only opens the branch; every other item is a leaf with a page. <paramref name="Subtitle"/>
/// is the line under the page's title in the shell header.
/// </summary>
public sealed record NavigationItem(
    string Label,
    string Glyph,
    Type? PageType,
    IReadOnlyList<NavigationItem>? Children = null,
    string Subtitle = "")
{
    /// <summary>A parent item: no page, and the children it expands into.</summary>
    public static NavigationItem Parent(string label, string glyph, IReadOnlyList<NavigationItem> children) =>
        new(label, glyph, null, children);

    /// <summary>A section heading in the pane (Performance, Windows, Apps, Advanced): no page, no icon.</summary>
    public static NavigationItem Header(string label) => new(label, string.Empty, null) { IsHeader = true };

    /// <summary>
    /// What the page is opened with, for a page that shows one of several things - a GPU entry carries
    /// its card. Null for every other page.
    /// </summary>
    public object? Parameter { get; init; }

    /// <summary>Whether this is a section heading rather than an item.</summary>
    public bool IsHeader { get; private init; }

    /// <summary>Whether picking this item navigates. False for a parent, which only expands.</summary>
    public bool IsPage => PageType is not null;

    /// <summary>This item and, after it, every item below it, however deep.</summary>
    public IEnumerable<NavigationItem> SelfAndDescendants()
    {
        yield return this;

        foreach (var child in Children ?? [])
        {
            foreach (var descendant in child.SelfAndDescendants())
            {
                yield return descendant;
            }
        }
    }
}
