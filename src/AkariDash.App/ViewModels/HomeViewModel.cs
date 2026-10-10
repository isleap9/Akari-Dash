using System.Text;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AkariDash.App.Views;
using AkariDash.Core.Tweaks;
using AkariDash.Framework.Navigation;
using AkariDash.Framework.Services;
using AkariDash.Framework.ViewModels;

namespace AkariDash.App.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IInfoBarService _infoBar;
    private readonly TweakEngine _engine;
    private readonly ILogger<HomeViewModel> _logger;

    public HomeViewModel(
        INavigationService navigation,
        IDialogService dialogs,
        IInfoBarService infoBar,
        TweakEngine engine,
        ILogger<HomeViewModel> logger)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _infoBar = infoBar;
        _engine = engine;
        _logger = logger;
        Title = "Home";
    }

    [RelayCommand]
    private void OpenSettings() => _navigation.NavigateTo<SettingsPage>();

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        await _dialogs.ShowInfoAsync(
            $"About {App.AppName}",
            $"{App.AppName}\nVersion {App.AppVersion}\n\nSettings are stored in:\n{App.SettingsFilePath}");
    }

    /// <summary>
    /// Shows one combined preview of putting every Tweak into its Recommended Option, then applies
    /// them one by one and reports how each went. The command is disabled while it runs.
    /// </summary>
    [RelayCommand]
    private async Task ApplyAllRecommendedAsync()
    {
        const string title = "Apply all recommended";
        try
        {
            var plan = _engine.PlanRecommended(TweakCatalog.All);
            var skipped = plan.Skipped.Count == 0
                ? string.Empty
                : Paragraph("Skipped:", plan.Skipped.Select(skip => $"{skip.Tweak.Title}: {skip.Reason}"));

            if (plan.ToApply.Count == 0)
            {
                await _dialogs.ShowInfoAsync(title, "Nothing to change." + skipped);
                return;
            }

            var preview = "What will change:" + string.Concat(plan.ToApply.Select(planned =>
                Paragraph($"{planned.Tweak.Title} → {planned.Option.Label}", planned.Changes.Select(change => change.ToString()))));

            var confirmed = await _dialogs.ConfirmAsync(title, preview + skipped + BuildInfo.PreviewNote, "Apply all", "Cancel");

            if (!confirmed)
            {
                return;
            }

            _logger.LogInformation(
                "Applying all recommended: {Tweaks}",
                string.Join("; ", plan.ToApply.Select(planned => $"{planned.Tweak.Id} -> {planned.Option.Id}")));

            if (_engine.RestorePointDue)
            {
                _infoBar.ShowInfo(title, BuildInfo.RestorePointNote);
            }

            // Off the UI thread: the session's first apply waits for a restore point, which can take a while.
            var results = await Task.Run(() => _engine.ApplyRecommended(plan));
            var restorePointFailure = _engine.TakeRestorePointFailure();
            if (restorePointFailure is not null)
            {
                _logger.LogWarning(restorePointFailure, "No restore point was created");
            }

            foreach (var result in results)
            {
                if (result.Succeeded)
                {
                    _logger.LogInformation("Applied {Tweak} -> {Option}", result.Tweak.Id, result.Option.Id);
                }
                else
                {
                    _logger.LogError(result.Error, "{Tweak} -> {Option} failed", result.Tweak.Id, result.Option.Id);
                }
            }

            var succeeded = results.Where(result => result.Succeeded).ToList();
            var failed = results.Where(result => !result.Succeeded).ToList();
            var applied = BuildInfo.IsDryRunOnly ? "recorded as a Dry Run" : "applied";

            var report = new StringBuilder();
            if (restorePointFailure is not null)
            {
                report.Append(Paragraph("No restore point was created; the Tweaks were applied anyway:", [restorePointFailure.Message]));
            }

            if (succeeded.Count > 0)
            {
                report.Append(Paragraph(
                    BuildInfo.IsDryRunOnly ? "Recorded as a Dry Run:" : "Applied:",
                    succeeded.Select(result => $"{result.Tweak.Title} → {result.Option.Label}")));
            }

            if (failed.Count > 0)
            {
                // The engine's own errors already name the Tweak and say whether it was rolled back.
                report.Append(Paragraph("Failed:", failed.Select(result => result.Error is TweakApplyException or TweakUnavailableException
                    ? result.Error.Message
                    : $"{result.Tweak.Title}: {result.Error!.Message}")));
            }

            var waiting = succeeded.Where(result => result.Tweak.Activation != Activation.Immediately).ToList();
            if (waiting.Count > 0)
            {
                report.Append(Paragraph(
                    waiting.Any(result => result.Tweak.Activation == Activation.AfterRestart) ? "Restart to finish:" : "Sign out to finish:",
                    waiting.Select(result => result.Tweak.Title)));
            }

            report.Append(skipped);

            if (failed.Count > 0)
            {
                _infoBar.ShowWarning(title, $"{succeeded.Count} Tweak(s) {applied}, {failed.Count} failed; see the details for each.");
            }
            else if (restorePointFailure is not null)
            {
                _infoBar.ShowWarning(title, $"{succeeded.Count} Tweak(s) {applied}, but no restore point was created; see the details.");
            }
            else
            {
                _infoBar.ShowSuccess(title, $"{succeeded.Count} Tweak(s) {applied}.");
            }

            await _dialogs.ShowInfoAsync(title, report.ToString().TrimStart());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply all recommended failed");
            _infoBar.ShowError(title, ex.Message);
        }
    }

    /// <summary>A blank line, <paramref name="heading"/>, then each line indented beneath it.</summary>
    private static string Paragraph(string heading, IEnumerable<string> lines) =>
        Environment.NewLine + Environment.NewLine + heading +
        string.Concat(lines.Select(line => Environment.NewLine + "    " + line));
}
