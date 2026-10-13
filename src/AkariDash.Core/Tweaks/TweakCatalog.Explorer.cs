using AkariDash.Core.Machine;
using Microsoft.Win32;

namespace AkariDash.Core.Tweaks;

// The File Explorer page, the last of the three Customize Categories (#32), and the first page
// written under ADR-0005: the catalog is curated, not ported.
//
// Winhance's MIT snapshot has 48 Explorer values and a port of it would have been 34 rows. That is
// not 34 decisions, it is Folder Options' Advanced tab with our paint on it, so it is not what this
// page is. Ten rows ship: six that were already on the Windows page and moved here with their
// Original Values, and four that earn a place because a person setting up a gaming PC actually
// wants them. Everything else is in the list at the bottom of this comment, with a test for each
// asserting it is absent, so declining stays visible instead of looking like an oversight.
//
// Values come from Akari-OS's reg.reg (MIT), AkariOS-Ultimate (MIT) and Winhance's MIT snapshot
// (4dc441a0; see THIRD-PARTY-NOTICES.md), which is still what says how a setting is written. Taste
// again, so no Recommended Option.
//
// The four that earned their place:
//
//   Sync provider notifications is the advertising strip OneDrive puts inside File Explorer, above
//   the file list, offering backups and Microsoft 365. It is an ad in a file manager.
//
//   Files from Office.com fills Explorer's Home with documents from a work account the gaming PC
//   may not even be signed in to, and reaches the cloud to do it.
//
//   The "- Shortcut" suffix is the one piece of Explorer naming people go looking for a tool to fix.
//   One binary value, cleanly undone by deleting it.
//
//   The classic right-click menu is the single most asked-for Windows 11 change, and the user asked
//   for it by name on 2026-10-13.
//
// Left out, and why. Nearly all of it is one checkbox in Folder Options > View, and a row whose
// whole description would be "this is a checkbox in Folder Options" is a row Settings already has:
//
//   The Advanced tab, entire: ShowTypeOverlay, FolderContentsInfoTip, ShowInfoTip, ShowStatusBar,
//   ShowPreviewHandlers, ShowEncryptCompressedColor, HideMergeConflicts, AutoCheckSelect,
//   SharingWizardOn, ShowDriveLettersFirst, HideDrivesWithNoMedia, UseCompactMode, TypeAhead,
//   SeparateProcess and PersistBrowsers. Winhance has every one; none of them changes how a PC
//   plays games or what it sends Microsoft, which is what this catalog is for. (IconsOnly, the
//   thumbnails checkbox, is the exception that proves it: it ships, on the Gaming page with the
//   other visual effects, because thumbnails cost something to draw.)
//
//   ShowSuperHidden shows the protected operating system files. Windows itself warns before
//   turning it on, and a row that invites someone to delete a boot file is not a kindness.
//
//   The navigation pane rows — NavPaneShowAllCloudStates, NavPaneExpandToCurrentFolder,
//   NavPaneShowAllFolders, and the Libraries and Network CLSIDs under Software\Classes — are the
//   same checkbox list one level down.
//
//   Browse folders (a bit of CabinetState\Settings) and Click items as follows (a bit of ShellState
//   with IconUnderline) are expressible — the engine does single bits of a binary value — but they
//   are Windows 95 settings that nobody has changed in twenty years.
//
//   Take ownership and the Photo Viewer associations work by whole trees of keys holding commands
//   and file types, which is more than a key existing. (The classic right-click menu used to be in
//   this list for the same reason; it ships now, because RegistryKeyLocation lets Undo take a key
//   away. Open in Terminal is a value, not a key, and is out for being a developer's setting.)
//
//   LongPathsEnabled stays on the Windows page under System. It is not an Explorer setting; it is a
//   machine-wide limit that happens to be most visible in Explorer.
public static partial class TweakCatalog
{
    private const string ExplorerKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer";

    private static IEnumerable<DeclaredTweak> ExplorerTweaks() =>
    [
        OpenExplorerTo(),
        ExplorerToggle("explorer.frequent-folders", "Frequent folders", "Shows the folders you open most in File Explorer's Home and Quick access.",
            "Explorer's Home", ExplorerKey, "ShowFrequent"),
        ExplorerToggle("explorer.recent-files", "Recent files", "Shows the files you opened recently in File Explorer's Home.",
            "Explorer's Home", ExplorerKey, "ShowRecent"),
        ExplorerToggle("explorer.office-files", "Files from Office.com", "Puts documents from your Microsoft account in File Explorer's Home, which means Explorer reaches the cloud to fill a folder listing.",
            "Explorer's Home", ExplorerKey, "ShowCloudFilesInQuickAccess"),

        FileExtensions(),
        HiddenFiles(),
        ExplorerToggle("explorer.full-path", "Full path in the title bar", "Shows the whole folder path, like C:\\Users\\You\\Documents, at the top of File Explorer.",
            "Working with files", $@"{ExplorerKey}\CabinetState", "FullPath", absent: 0),
        ExplorerToggle("explorer.sync-notifications", "OneDrive adverts in Explorer", "The strip above the file list where OneDrive offers backups, Microsoft 365 and other things to buy. Windows calls these sync provider notifications.",
            "Working with files", ExplorerAdvanced, "ShowSyncProviderNotifications"),
        ShortcutSuffix(),
        ClassicContextMenu(),
    ];

    /// <summary>
    /// A Show/Hide Tweak over one current-user DWORD, where 1 shows and 0 hides. Explorer reads
    /// these when it starts, so a sign-out is the first moment every one of them is certainly in
    /// effect.
    /// </summary>
    private static DeclaredTweak ExplorerToggle(
        string id, string title, string description, string group, string key, string name, int absent = 1)
    {
        var target = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, key, name),
            AbsentMeans: RegistryValue.DWord(absent));

        return new DeclaredTweak(
            id, title, description, Category.Explorer, group, [target],
            [DWordOption(target, "show", "Show", 1), DWordOption(target, "hide", "Hide", 0)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak OpenExplorerTo()
    {
        // Akari-OS opens to This PC (1); Windows opens to Home (2) when LaunchTo is missing.
        var launchTo = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, ExplorerAdvanced, "LaunchTo"),
            AbsentMeans: RegistryValue.DWord(2));

        return new DeclaredTweak(
            "explorer.open-to",
            "Open File Explorer to",
            "Which page File Explorer shows when you open it.",
            Category.Explorer, "Explorer's Home", [launchTo],
            [DWordOption(launchTo, "home", "Home", 2), DWordOption(launchTo, "this-pc", "This PC", 1)],
            Activation: Activation.Immediately);
    }

    private static DeclaredTweak FileExtensions()
    {
        // HideFileExt is backwards from the rest: 0 shows the extension, 1 hides it, and Windows
        // hides it when the value is missing.
        var hide = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, ExplorerAdvanced, "HideFileExt"),
            AbsentMeans: RegistryValue.DWord(1));

        return new DeclaredTweak(
            "explorer.file-extensions",
            "File name extensions",
            "Shows the end of file names, like .exe or .pdf, so you can tell what kind of file it is — and so a file called invoice.pdf.exe cannot pretend to be a document.",
            Category.Explorer, "Working with files", [hide],
            [DWordOption(hide, "show", "Show", 0), DWordOption(hide, "hide", "Hide", 1)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak HiddenFiles()
    {
        // Hidden is 1 to show and 2 to hide, not 0, and Windows hides when the value is missing.
        var hidden = new TweakTarget(
            new RegistryLocation(RegistryHive.CurrentUser, ExplorerAdvanced, "Hidden"),
            AbsentMeans: RegistryValue.DWord(2));

        return new DeclaredTweak(
            "explorer.hidden-files",
            "Hidden files and folders",
            "Shows files and folders that are marked hidden, such as AppData — which is where most games keep their saves, configs and crash logs.",
            Category.Explorer, "Working with files", [hidden],
            [DWordOption(hidden, "show", "Show", 1), DWordOption(hidden, "hide", "Hide", 2)],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak ClassicContextMenu()
    {
        // Akari-OS's reg.reg, AkariOS-Ultimate and Winhance's MIT snapshot all turn it on the same way:
        // an empty InprocServer32 under this CLSID in the user's classes, which overrides the Windows 11
        // menu's handler. What brings the Windows 11 menu back is the key going away, so AkariOS-
        // Ultimate's revert deletes the whole {86ca1aa0-...} key, and that is what Undo does here when
        // the key did not exist before. (Winhance's revert only deletes the value and leaves the key,
        // which is the half-undo that kept this row out until the engine could delete a key.)
        const string clsid = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
        var key = new TweakTarget(new RegistryKeyLocation(RegistryHive.CurrentUser, clsid));
        var handler = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, $@"{clsid}\InprocServer32", ""));

        return new DeclaredTweak(
            "explorer.classic-context-menu",
            "Classic right-click menu",
            "Puts back the Windows 10 right-click menu, with every option in one list, instead of the shorter Windows 11 one where most of them are behind Show more options. Applies to the desktop and File Explorer.",
            Category.Explorer, "Working with files", [key, handler],
            [
                new TweakOption("windows-11", "Windows 11 menu", new Dictionary<TweakTarget, MachineValue?> { [key] = null, [handler] = null }),
                new TweakOption("classic", "Classic (Windows 10) menu", new Dictionary<TweakTarget, MachineValue?> { [key] = KeyPresence.Present, [handler] = RegistryValue.String("") }),
            ],
            Activation: Activation.AfterSignOut);
    }

    private static DeclaredTweak ShortcutSuffix()
    {
        // Winhance writes four zero bytes to the binary value "link"; deleting it brings the suffix
        // back, which is what Windows does with no value there at all.
        var link = new TweakTarget(new RegistryLocation(RegistryHive.CurrentUser, ExplorerKey, "link"));

        return new DeclaredTweak(
            "explorer.shortcut-suffix",
            "\"- Shortcut\" on new shortcuts",
            "Whether Windows adds \"- Shortcut\" to the name of every shortcut you create. Shortcuts that already have it keep it; this only decides what new ones are called.",
            Category.Explorer, "Working with files", [link],
            [
                Option(link, "keep", "Add it", null),
                Option(link, "drop", "Plain name", new RegistryValue(RegistryValueKind.Binary, new byte[] { 0, 0, 0, 0 })),
            ],
            Activation: Activation.AfterSignOut);
    }
}
