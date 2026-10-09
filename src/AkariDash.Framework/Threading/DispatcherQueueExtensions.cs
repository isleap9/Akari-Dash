using Microsoft.UI.Dispatching;

namespace AkariDash.Framework.Threading;

/// <summary>
/// Helpers for scheduling work on the UI thread via <see cref="DispatcherQueue"/>.
/// </summary>
public static class DispatcherQueueExtensions
{
    /// <summary>Runs an action on the UI thread, awaiting completion and marshaling exceptions back.</summary>
    public static Task RunOnUIThreadAsync(this DispatcherQueue dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(action);

        if (dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue work on the dispatcher queue."));
        }

        return tcs.Task;
    }

    /// <summary>Runs a value-returning function on the UI thread, awaiting completion and marshaling exceptions back.</summary>
    public static Task<T> RunOnUIThreadAsync<T>(this DispatcherQueue dispatcher, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(func);

        if (dispatcher.HasThreadAccess)
        {
            return Task.FromResult(func());
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue work on the dispatcher queue."));
        }

        return tcs.Task;
    }

    /// <summary>Runs an async action on the UI thread, awaiting its completion and marshaling exceptions back.</summary>
    public static Task RunOnUIThreadAsync(this DispatcherQueue dispatcher, Func<Task> funcAsync)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(funcAsync);

        if (dispatcher.HasThreadAccess)
        {
            return funcAsync();
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await funcAsync().ConfigureAwait(true);
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue work on the dispatcher queue."));
        }

        return tcs.Task;
    }
}
