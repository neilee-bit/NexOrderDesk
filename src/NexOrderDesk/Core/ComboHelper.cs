using System.Windows;
namespace NexOrderDesk.Core;

/// <summary>Attach ManageKey to a ComboBox to show a "+ Add / edit list…" footer in its drop-down.</summary>
public static class ComboHelper
{
    public static readonly DependencyProperty ManageKeyProperty =
        DependencyProperty.RegisterAttached("ManageKey", typeof(string), typeof(ComboHelper), new PropertyMetadata(null));
    public static string? GetManageKey(DependencyObject o) => (string?)o.GetValue(ManageKeyProperty);
    public static void SetManageKey(DependencyObject o, string? v) => o.SetValue(ManageKeyProperty, v);
}
