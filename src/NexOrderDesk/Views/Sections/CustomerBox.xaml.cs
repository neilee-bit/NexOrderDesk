using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NexOrderDesk.Core;
using NexOrderDesk.Services;
namespace NexOrderDesk.Views.Sections;

/// <summary>
/// Customer field with suggestions (2.8). Clicking the field shows the customer list; typing filters it (partial match).
/// Up / Down choose, Enter or a click fills the field, Esc closes. Any other name can simply be typed.
/// </summary>
public partial class CustomerBox : UserControl
{
    private bool _filling;

    public CustomerBox() { InitializeComponent(); }

    private static IEnumerable<string> Names => SettingsService.Instance.Customers;

    /// <summary>Current suggestions (also used by the self-test).</summary>
    public IReadOnlyList<string> Suggestions => List.Items.Cast<string>().ToList();
    public bool IsDropDownOpen => Drop.IsOpen;

    /// <summary>Shows the names matching the text in the box. Returns how many there are.</summary>
    public int ShowSuggestions(string? typed)
    {
        var matches = NameSuggest.Match(Names, typed);
        // the exact name already in the box is not worth suggesting on its own
        if (matches.Count == 1 && string.Equals(matches[0], (typed ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) matches.Clear();
        List.ItemsSource = matches;
        List.SelectedIndex = -1;
        Drop.IsOpen = matches.Count > 0 && Box.IsKeyboardFocusWithin && Box.IsEnabled && IsHitTestVisibleChain();
        return matches.Count;
    }

    private bool IsHitTestVisibleChain()
    {
        for (DependencyObject? d = this; d != null; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
            if (d is UIElement u && !u.IsHitTestVisible) return false;
        return true;
    }

    public void Accept(string name)
    {
        _filling = true;
        Box.Text = name;
        Box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        Box.CaretIndex = Box.Text.Length;
        _filling = false;
        Drop.IsOpen = false;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_filling || !Box.IsKeyboardFocusWithin) return;
        ShowSuggestions(Box.Text);
    }

    private void OnGotFocus(object sender, KeyboardFocusChangedEventArgs e) => ShowSuggestions(Box.Text);

    private void OnBoxClick(object sender, MouseButtonEventArgs e)
    {
        if (!Drop.IsOpen) ShowSuggestions(Box.Text);
    }

    private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // a click on a suggestion is handled before the pop-up closes
        Dispatcher.BeginInvoke(new Action(() => { if (!Box.IsKeyboardFocusWithin) Drop.IsOpen = false; }), DispatcherPriority.Input);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (!Drop.IsOpen) { ShowSuggestions(Box.Text); if (!Drop.IsOpen) ShowSuggestions(""); }
                if (List.Items.Count > 0) { List.SelectedIndex = Math.Min(List.SelectedIndex + 1, List.Items.Count - 1); List.ScrollIntoView(List.SelectedItem); }
                e.Handled = true;
                break;
            case Key.Up:
                if (Drop.IsOpen && List.Items.Count > 0)
                {
                    List.SelectedIndex = Math.Max(List.SelectedIndex - 1, 0);
                    List.ScrollIntoView(List.SelectedItem);
                    e.Handled = true;
                }
                break;
            case Key.Enter:
            case Key.Tab:
                if (Drop.IsOpen && List.SelectedItem is string pick) { Accept(pick); e.Handled = e.Key == Key.Enter; }
                else Drop.IsOpen = false;
                break;
            case Key.Escape:
                if (Drop.IsOpen) { Drop.IsOpen = false; e.Handled = true; }
                break;
        }
    }

    private void OnPick(object sender, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(List, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item?.Content is string name) { Accept(name); Box.Focus(); }
    }

    private void OnManage(object sender, RoutedEventArgs e)
    {
        Drop.IsOpen = false;
        Dispatcher.BeginInvoke(new Action(() => OptionsManager.Show("customers")), DispatcherPriority.Background);
    }
}
