using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;

namespace AkariDash.Framework.Behaviors;

/// <summary>
/// Scrolls a <see cref="ListViewBase"/> (ListView / GridView) so that the
/// <see cref="TargetItem"/> becomes visible whenever it changes.
/// </summary>
public sealed class ListViewScrollIntoViewBehavior : Behavior<ListViewBase>
{
    public static readonly DependencyProperty TargetItemProperty = DependencyProperty.Register(
        nameof(TargetItem),
        typeof(object),
        typeof(ListViewScrollIntoViewBehavior),
        new PropertyMetadata(null, OnTargetItemChanged));

    public object? TargetItem
    {
        get => GetValue(TargetItemProperty);
        set => SetValue(TargetItemProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;
        base.OnDetaching();
    }

    private static void OnTargetItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ListViewScrollIntoViewBehavior)d).ScrollToTarget();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ScrollToTarget();
    }

    private void ScrollToTarget()
    {
        if (TargetItem is not null && AssociatedObject is not null)
        {
            AssociatedObject.ScrollIntoView(TargetItem);
        }
    }
}
