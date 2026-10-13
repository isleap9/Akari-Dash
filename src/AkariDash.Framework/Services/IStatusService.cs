using CommunityToolkit.Mvvm.ComponentModel;

namespace AkariDash.Framework.Services;

/// <summary>
/// The status bar's left side: "Ready", or what is running now ("Uninstalling Microsoft Edge") with a
/// progress bar beside it. Work says what it is doing with <see cref="Begin"/> and disposes the result
/// when it ends; the newest job still running is the one shown.
/// </summary>
public interface IStatusService : System.ComponentModel.INotifyPropertyChanged
{
    string Text { get; }

    bool IsBusy { get; }

    /// <summary>The opposite of <see cref="IsBusy"/>, for the Ready dot.</summary>
    bool IsIdle { get; }

    IDisposable Begin(string text);
}

public sealed partial class StatusService : ObservableObject, IStatusService
{
    private const string Ready = "Ready";
    private readonly List<Job> _running = [];

    [ObservableProperty]
    public partial string Text { get; private set; } = Ready;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; private set; }

    public bool IsIdle => !IsBusy;

    public IDisposable Begin(string text)
    {
        var job = new Job(this, text);
        _running.Add(job);
        Show();
        return job;
    }

    private void End(Job job)
    {
        _running.Remove(job);
        Show();
    }

    private void Show()
    {
        IsBusy = _running.Count > 0;
        Text = IsBusy ? _running[^1].Text : Ready;
    }

    private sealed class Job(StatusService owner, string text) : IDisposable
    {
        private bool _ended;

        public string Text { get; } = text;

        public void Dispose()
        {
            if (!_ended)
            {
                _ended = true;
                owner.End(this);
            }
        }
    }
}
