using Microsoft.Extensions.Logging;
using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using AkariDash.Framework.Services;

namespace AkariDash.App.Services;

/// <summary>
/// On the very first launch, offers to create a System Restore point before anything is changed
/// (like Winhance does). It asks once: the answer is remembered in the app's settings.
/// </summary>
public sealed class FirstRunRestorePoint(
    TweakEngine engine,
    IDialogService dialogs,
    IInfoBarService infoBar,
    ISettingsService settings,
    ILogger<FirstRunRestorePoint> logger)
{
    private const string AskedKey = "FirstRunRestorePointAsked";
    private const string Title = "Restore point";

    public async Task OfferAsync()
    {
        // A Dry Run build writes nothing, so it has nothing to protect.
        if (BuildInfo.IsDryRunOnly || await settings.GetAsync(AskedKey, false))
        {
            return;
        }

        var create = await dialogs.ConfirmAsync(
            "Create a restore point?",
            "Before you change anything, Akari-Dash can create a System Restore point. If a Tweak ever causes trouble, "
                + "you can bring Windows back to how it is now from System Restore, even when Undo is not enough."
                + Environment.NewLine + Environment.NewLine
                + "Akari-Dash asks only this once. Creating it can take a minute.",
            confirmText: "Create restore point",
            cancelText: "Not now");

        await settings.SetAsync(AskedKey, true);
        if (create)
        {
            await CreateAsync();
        }
    }

    private async Task CreateAsync()
    {
        infoBar.ShowInfo(Title, "Creating a restore point; this can take a minute.");
        try
        {
            await Task.Run(engine.CreateRestorePointNow);
            infoBar.ShowSuccess(Title, "Restore point created. You can find it in System Restore.");
        }
        catch (SystemProtectionOffException)
        {
            var turnOn = await dialogs.ConfirmAsync(
                "System Protection is off",
                "Windows cannot create restore points because System Protection is off for the Windows drive. "
                    + "Turn it on and create the restore point?",
                confirmText: "Turn on and create",
                cancelText: "Not now");
            if (!turnOn)
            {
                infoBar.ShowWarning(Title, "No restore point was created: System Protection is off.");
                return;
            }

            infoBar.ShowInfo(Title, "Turning on System Protection and creating a restore point; this can take a minute.");
            try
            {
                await Task.Run(() =>
                {
                    engine.TurnOnSystemProtection();
                    engine.CreateRestorePointNow();
                });
                infoBar.ShowSuccess(Title, "System Protection is on and a restore point was created.");
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private void Fail(Exception ex)
    {
        logger.LogWarning(ex, "The first-run restore point was not created");
        infoBar.ShowWarning(Title, $"No restore point was created: {ex.Message}");
    }
}
