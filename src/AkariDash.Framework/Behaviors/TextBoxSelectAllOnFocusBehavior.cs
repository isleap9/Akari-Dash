using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;

namespace AkariDash.Framework.Behaviors;

/// <summary>Selects all text when the associated <see cref="TextBox"/> receives focus.</summary>
public sealed class TextBoxSelectAllOnFocusBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.GotFocus += OnGotFocus;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.GotFocus -= OnGotFocus;
        base.OnDetaching();
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        AssociatedObject.SelectAll();
    }
}
