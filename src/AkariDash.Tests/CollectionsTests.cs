using System.Collections.Specialized;
using System.ComponentModel;
using AkariDash.Framework.Collections;
using Xunit;

namespace AkariDash.Tests;

public class CollectionsTests
{
    [Fact]
    public void RangeObservableCollection_AddRange_raises_single_notification()
    {
        var collection = new RangeObservableCollection<int>();
        var notifications = 0;
        collection.CollectionChanged += (_, _) => notifications++;

        collection.AddRange([1, 2, 3]);

        Assert.Equal(3, collection.Count);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void RangeObservableCollection_AddRange_empty_raises_nothing()
    {
        var collection = new RangeObservableCollection<int>();
        var notifications = 0;
        collection.CollectionChanged += (_, _) => notifications++;

        collection.AddRange([]);

        Assert.Equal(0, notifications);
        Assert.Empty(collection);
    }

    [Fact]
    public void RangeObservableCollection_RemoveRange_raises_single_notification()
    {
        var collection = new RangeObservableCollection<int>([1, 2, 3, 4]);
        var notifications = 0;
        collection.CollectionChanged += (_, _) => notifications++;

        collection.RemoveRange([1, 2]);

        Assert.Equal([3, 4], collection);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void RangeObservableCollection_ReplaceAll_replaces_content()
    {
        var collection = new RangeObservableCollection<int>([1, 2]);
        var notifications = 0;
        collection.CollectionChanged += (_, _) => notifications++;

        collection.ReplaceAll([5, 6, 7]);

        Assert.Equal([5, 6, 7], collection);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void RangeObservableCollection_Clear_raises_reset()
    {
        var collection = new RangeObservableCollection<int>([1, 2, 3]);
        var notifications = 0;
        NotifyCollectionChangedEventArgs? args = null;
        collection.CollectionChanged += (_, e) => { notifications++; args = e; };

        collection.Clear();

        Assert.Empty(collection);
        Assert.Equal(1, notifications);
        Assert.Equal(NotifyCollectionChangedAction.Reset, args!.Action);
    }

    [Fact]
    public void RangeObservableCollection_Add_uses_add_notification()
    {
        var collection = new RangeObservableCollection<int>([1]);
        NotifyCollectionChangedEventArgs? args = null;
        collection.CollectionChanged += (_, e) => args = e;

        collection.Add(2);

        Assert.Equal(NotifyCollectionChangedAction.Add, args!.Action);
        Assert.Single(args.NewItems!);
        Assert.Equal(2, args.NewItems![0]!);
    }

    [Fact]
    public void ObservableGroupCollection_exposes_key_and_items()
    {
        var group = new ObservableGroupCollection<string, int>("Numbers", [1, 2, 3]);

        Assert.Equal("Numbers", group.Key);
        Assert.Equal(3, group.Count);
        Assert.Equal([1, 2, 3], group);
    }

    [Fact]
    public void ObservableGroupCollection_NotifyGroupChanged_raises_property_changed()
    {
        var group = new ObservableGroupCollection<string, int>("Numbers");
        var changes = new List<string>();
        ((INotifyPropertyChanged)group).PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        group.DisplayLabel = "One, two";
        group.NotifyGroupChanged();

        Assert.Contains(nameof(group.DisplayLabel), changes);
        Assert.Contains(nameof(group.Count), changes);
    }

    [Fact]
    public void IncrementalLoadingCollection_rejects_invalid_constructor_args()
    {
        Assert.Throws<ArgumentNullException>(() => new IncrementalLoadingCollection<string>(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IncrementalLoadingCollection<string>((_, _) => Task.FromResult<IEnumerable<string>>([]), 0));
    }

    [Fact]
    public async Task IncrementalLoadingCollection_loads_pages_until_empty()
    {
        var pages = new Queue<IEnumerable<string>>();
        pages.Enqueue(["a", "b", "c"]);
        pages.Enqueue(["d"]);
        pages.Enqueue([]);

        var collection = new IncrementalLoadingCollection<string>(
            (_, _) => Task.FromResult(pages.Count > 0 ? pages.Dequeue() : []),
            pageSize: 20);

        Assert.True(collection.HasMoreItems);

        var first = await collection.LoadMoreItemsAsync(20);
        Assert.Equal(3u, first.Count);
        Assert.Equal(["a", "b", "c"], collection);

        var second = await collection.LoadMoreItemsAsync(20);
        Assert.Equal(1u, second.Count);
        Assert.Equal(4, collection.Count);

        var third = await collection.LoadMoreItemsAsync(20);
        Assert.Equal(0u, third.Count);
        Assert.False(collection.HasMoreItems);

        var guarded = await collection.LoadMoreItemsAsync(20);
        Assert.Equal(0u, guarded.Count);
        Assert.Equal(4, collection.Count);
    }

    [Fact]
    public async Task IncrementalLoadingCollection_StopLoading_blocks_further_loads()
    {
        var calls = 0;
        var collection = new IncrementalLoadingCollection<string>(
            (_, _) => { calls++; return Task.FromResult<IEnumerable<string>>(["x"]); });

        await collection.LoadMoreItemsAsync(20);
        collection.StopLoading();

        Assert.False(collection.HasMoreItems);
        Assert.Equal(1, calls);

        await collection.LoadMoreItemsAsync(20);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task IncrementalLoadingCollection_Reset_allows_loading_again()
    {
        var calls = 0;
        var collection = new IncrementalLoadingCollection<string>(
            (_, _) => { calls++; return Task.FromResult<IEnumerable<string>>(["x"]); });

        await collection.LoadMoreItemsAsync(20);
        collection.Reset();

        Assert.True(collection.HasMoreItems);
        Assert.Empty(collection);

        await collection.LoadMoreItemsAsync(20);
        Assert.Equal(2, calls);
        Assert.Equal(["x"], collection);
    }
}
