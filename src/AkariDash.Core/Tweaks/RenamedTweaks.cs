namespace AkariDash.Core.Tweaks;

/// <summary>
/// Tweaks that changed id. A Tweak's Original Values are filed under its id, so a Tweak that moves
/// to another Category — and so takes a new id — would otherwise look to Akari-Dash like a Tweak it
/// no longer has: Home would offer to Undo it as a retired Tweak, under a raw id, while the row on
/// its new page showed the change as nothing to do with Akari-Dash. Renaming the record instead
/// keeps the apply where the user can see it, with Undo on the Tweak's own row.
/// </summary>
public static class RenamedTweaks
{
    /// <summary>Old id to current id. An entry stays here for good: an old PC may still hold the record.</summary>
    public static IReadOnlyDictionary<string, string> Ids { get; } = new Dictionary<string, string>
    {
        // #30: the Windows page's Notifications Group became the Notifications Category.
        ["windows.tips-notifications"] = "notifications.tips",
        ["windows.welcome-experience"] = "notifications.welcome-experience",
        ["windows.finish-setup"] = "notifications.finish-setup",

        // #32: the Windows page's Start Group became the Start menu Category, under Customize.
        ["windows.start-recently-added"] = "start.recently-added",
        ["windows.start-most-used"] = "start.most-used",
        ["windows.start-recommendations"] = "start.recommendations",
        ["windows.start-account-notifications"] = "start.account-notifications",

        // #32: the Windows page's Taskbar Group became the Taskbar Category, under Customize.
        ["windows.taskbar-search"] = "taskbar.search",
        ["windows.taskbar-alignment"] = "taskbar.alignment",
        ["windows.task-view-button"] = "taskbar.task-view",
        ["windows.widgets"] = "taskbar.widgets",
        ["windows.end-task"] = "taskbar.end-task",
    };

    /// <summary>
    /// Files every record saved under an old id under its current one, and returns what it moved.
    /// Safe to run at every start: it does nothing once the records are already current.
    /// A record under the current id is never overwritten — the two were applied separately and
    /// their Original Values describe different moments, so the old one is left alone and Home
    /// offers it as a retired Tweak, which is the only honest thing left to do with it.
    /// </summary>
    public static IReadOnlyList<(string From, string To)> Migrate(IOriginalValuesStore store)
    {
        List<(string, string)> moved = [];

        foreach (var id in store.AppliedTweakIds())
        {
            if (!Ids.TryGetValue(id, out var current) || store.Get(current) is not null)
            {
                continue;
            }

            if (store.Get(id) is { } applied)
            {
                store.Save(current, applied);
                store.Clear(id);
                moved.Add((id, current));
            }
        }

        return moved;
    }
}
