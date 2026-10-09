namespace AkariDash.Framework.Navigation;

/// <summary>
/// A single back/forward stack entry.
/// </summary>
public sealed record NavigationEntry(Type PageType, object? Parameter = null);
