namespace AkariDash.App;

/// <summary>A single entry in the main NavigationView.</summary>
public sealed record NavigationItem(string Label, string Glyph, Type PageType);
