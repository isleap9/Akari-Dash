using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;

namespace AkariDash.Framework.Collections;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that implements
/// <see cref="ISupportIncrementalLoading"/> so a ListView / GridView
/// can fetch pages of data on demand as the user scrolls.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public class IncrementalLoadingCollection<T> : ObservableCollection<T>, ISupportIncrementalLoading
{
    private readonly Func<CancellationToken, uint, Task<IEnumerable<T>>> _loadMore;
    private readonly uint _pageSize;
    private readonly CancellationTokenSource _cts = new();
    private bool _isLoading;
    private bool _hasMoreItems = true;

    /// <param name="loadMore">Receives a cancellation token and the requested page size, returns the next page of items (empty / null to stop).</param>
    /// <param name="pageSize">How many items to fetch per request.</param>
    public IncrementalLoadingCollection(
        Func<CancellationToken, uint, Task<IEnumerable<T>>> loadMore,
        uint pageSize = 20)
    {
        _loadMore = loadMore ?? throw new ArgumentNullException(nameof(loadMore));
        _pageSize = pageSize > 0 ? pageSize : throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    public bool HasMoreItems => _hasMoreItems;

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count) =>
        LoadMoreItemsAsyncCore(count).AsAsyncOperation();

    private async Task<LoadMoreItemsResult> LoadMoreItemsAsyncCore(uint count)
    {
        if (_isLoading || !_hasMoreItems)
        {
            return new LoadMoreItemsResult { Count = 0 };
        }

        _isLoading = true;
        try
        {
            var items = await _loadMore(_cts.Token, _pageSize);
            var list = items as IList<T> ?? items?.ToList();
            if (list is null || list.Count == 0)
            {
                _hasMoreItems = false;
                return new LoadMoreItemsResult { Count = 0 };
            }

            foreach (var item in list)
            {
                Add(item);
            }

            return new LoadMoreItemsResult { Count = (uint)list.Count };
        }
        catch (OperationCanceledException)
        {
            return new LoadMoreItemsResult { Count = 0 };
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Clears the collection and re-enables loading from the first page.</summary>
    public void Reset()
    {
        _hasMoreItems = true;
        Clear();
    }

    /// <summary>Marks the source as exhausted so no further pages are requested.</summary>
    public void StopLoading() => _hasMoreItems = false;

    /// <summary>Releases the internal cancellation token source.</summary>
    protected override void ClearItems()
    {
        base.ClearItems();
        _cts.Cancel();
    }
}
