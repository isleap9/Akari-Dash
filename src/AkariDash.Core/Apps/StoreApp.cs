namespace AkariDash.Core.Apps;

/// <summary>Which of the Store apps tab's filters a package falls under.</summary>
public enum StoreAppKind
{
    /// <summary>An app someone installed or Windows shipped as an app.</summary>
    App,

    /// <summary>Signed by Windows itself: shell parts, Settings, the Start menu host.</summary>
    System,

    /// <summary>A runtime other packages depend on, such as VCLibs or the .NET runtime.</summary>
    Framework,
}

/// <summary>
/// One Store (Appx/MSIX) package as the Store apps tab shows it. <paramref name="Name"/> is the
/// package name (<c>Microsoft.BingNews</c>), which is what the keep-list and the restore list match on.
/// </summary>
public sealed record StoreApp(
    string Name,
    string DisplayName,
    string Publisher,
    string Version,
    string Architecture,
    string FullName,
    string FamilyName,
    StoreAppKind Kind,
    string? IconPath = null);
