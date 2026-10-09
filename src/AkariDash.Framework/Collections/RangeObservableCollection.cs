using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace AkariDash.Framework.Collections;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> with bulk-mutation helpers
/// (<see cref="AddRange"/>, <see cref="RemoveRange"/>, <see cref="ReplaceAll"/>)
/// that raise a single Reset notification per bulk operation.
/// </summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public RangeObservableCollection()
    {
    }

    public RangeObservableCollection(IEnumerable<T> items)
        : base(items)
    {
    }

    /// <summary>Adds many items at once, raising one collection-changed notification.</summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        using var enumerator = items.GetEnumerator();
        var any = false;

        _suppressNotification = true;
        try
        {
            while (enumerator.MoveNext())
            {
                Items.Add(enumerator.Current);
                any = true;
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        if (any)
        {
            RaiseBulkNotification();
        }
    }

    /// <summary>Removes the given items, raising one collection-changed notification.</summary>
    public void RemoveRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var any = false;

        _suppressNotification = true;
        try
        {
            foreach (var item in items)
            {
                any |= Items.Remove(item);
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        if (any)
        {
            RaiseBulkNotification();
        }
    }

    /// <summary>Replaces the entire contents of the collection.</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _suppressNotification = true;
        try
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        RaiseBulkNotification();
    }

    /// <summary>Clears the collection, raising one collection-changed notification.</summary>
    public new void Clear()
    {
        _suppressNotification = true;
        try
        {
            Items.Clear();
        }
        finally
        {
            _suppressNotification = false;
        }

        RaiseBulkNotification();
    }

    private void RaiseBulkNotification()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnPropertyChanged(e);
        }
    }
}
