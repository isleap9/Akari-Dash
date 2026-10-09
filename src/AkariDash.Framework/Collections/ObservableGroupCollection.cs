using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AkariDash.Framework.Collections;

/// <summary>
/// A group header plus an <see cref="ObservableCollection{T}"/> of items,
/// suitable for grouping in a ListView / GridView / ItemsRepeater.
/// </summary>
public class ObservableGroupCollection<TKey, T> : ObservableCollection<T>
{
    public ObservableGroupCollection(TKey key, IEnumerable<T>? items = null)
    {
        Key = key;
        if (items is not null)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }
    }

    public TKey Key { get; }

    /// <summary>Human-readable label for the group header.</summary>
    public string? DisplayLabel { get; set; }

    /// <summary>Raises PropertyChanged on the group header so grouping UIs can re-render.</summary>
    public void NotifyGroupChanged()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(DisplayLabel)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
    }
}
