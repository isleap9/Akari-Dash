namespace AkariDash.App.ViewModels;

/// <summary>A named cluster of related Tweaks inside a Category.</summary>
public sealed record TweakGroupViewModel(string Name, IReadOnlyList<TweakViewModel> Tweaks);
