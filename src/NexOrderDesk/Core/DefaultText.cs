using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace NexOrderDesk.Core;

/// <summary>
/// TextBox helper: the box starts with a default value (e.g. "T-shirts match"). While the box still shows exactly that
/// default, clicking / tabbing into it selects it all, so the first key typed (or a paste) REPLACES it - the default never
/// stays next to the user's text. The user can still edit, clear or type anything.
/// Usage: h:DefaultText.Value="T-shirts match"
/// </summary>
public static class DefaultText
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached(
        "Value", typeof(string), typeof(DefaultText), new PropertyMetadata(null, OnChanged));
    public static string? GetValue(DependencyObject o) => (string?)o.GetValue(ValueProperty);
    public static void SetValue(DependencyObject o, string? v) => o.SetValue(ValueProperty, v);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        tb.GotKeyboardFocus -= OnFocus; tb.PreviewMouseLeftButtonDown -= OnMouseDown; tb.PreviewTextInput -= OnTextInput;
        if (e.NewValue is string)
        {
            tb.GotKeyboardFocus += OnFocus; tb.PreviewMouseLeftButtonDown += OnMouseDown; tb.PreviewTextInput += OnTextInput;
        }
    }

    private static bool IsDefault(TextBox tb) => GetValue(tb) is { } v && tb.Text == v;

    private static void OnFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox tb && IsDefault(tb)) tb.SelectAll();
    }

    // first click into a box that still shows the default: focus + select all (a normal click would just place the caret)
    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox tb || tb.IsKeyboardFocusWithin || !IsDefault(tb)) return;
        tb.Focus();
        tb.SelectAll();
        e.Handled = true;
    }

    // safety net: typing while the default is shown (caret placed some other way) still replaces it
    private static void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox tb && IsDefault(tb) && tb.SelectionLength != tb.Text.Length) tb.SelectAll();
    }
}
