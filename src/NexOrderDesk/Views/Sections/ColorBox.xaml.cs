using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NexOrderDesk.Core;
using NexOrderDesk.Models;
using NexOrderDesk.Services;
namespace NexOrderDesk.Views.Sections;

/// <summary>
/// Material colour field (2.9). Type a colour name - the swatch is found automatically. While typing, matching colours are
/// suggested with their swatch (saved colours first, then the built-in colour names). Up / Down + Enter or a click picks one.
/// </summary>
public partial class ColorBox : UserControl
{
    public sealed record Suggestion(string Name, Brush Brush);
    private bool _filling;

    public ColorBox()
    {
        InitializeComponent();
        Picker.Picked += (name, hex) =>
        {
            (DataContext as MaterialColorEntry)?.Pick(name, hex);
            PickerPopup.IsOpen = false;
        };
    }

    public bool IsPickerOpen => PickerPopup.IsOpen;

    /// <summary>Opens the advanced colour picker on the current colour.</summary>
    public void OpenPicker()
    {
        if (DataContext is not MaterialColorEntry m || !IsHitTestVisibleChain()) return;
        DropPopup.IsOpen = false;
        Picker.Start(m.CurrentHex, m.ColorName);
        PickerPopup.IsOpen = true;
    }

    private void OnOpenPicker(object sender, RoutedEventArgs e) => OpenPicker();

    public IReadOnlyList<string> SuggestionNames => List.Items.OfType<Suggestion>().Select(x => x.Name).ToList();
    public bool IsDropDownOpen => DropPopup.IsOpen;

    /// <summary>Saved colours (in list order) followed by the built-in names, matched like the customer suggestions.</summary>
    public static List<Suggestion> Suggest(string? typed, int max = 40)
    {
        var saved = SettingsService.Instance.Colors.ToList();
        var names = saved.Select(c => c.Name).Concat(ColorNames.AllNames.Select(n => n.ToUpperInvariant()));
        var matches = NameSuggest.Match(names, typed, max);
        return matches.Select(n =>
        {
            var hex = saved.FirstOrDefault(c => c.Name.Equals(n, StringComparison.OrdinalIgnoreCase))?.Hex ?? ColorNames.HexOf(n) ?? "#9AA0A9";
            return new Suggestion(n, ColorNames.BrushOf(hex));
        }).ToList();
    }

    public int ShowSuggestions(string? typed)
    {
        var list = Suggest(typed);
        if (list.Count == 1 && string.Equals(list[0].Name, (typed ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) list.Clear();
        List.ItemsSource = list;
        List.SelectedIndex = -1;
        DropPopup.IsOpen = list.Count > 0 && Box.IsKeyboardFocusWithin && IsHitTestVisibleChain();
        return list.Count;
    }

    private bool IsHitTestVisibleChain()
    {
        for (DependencyObject? d = this; d != null; d = VisualTreeHelper.GetParent(d))
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
        DropPopup.IsOpen = false;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_filling || !Box.IsKeyboardFocusWithin) return;
        ShowSuggestions(Box.Text);
    }

    private void OnGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Box.SelectAll();
        ShowSuggestions("");
    }

    private void OnBoxClick(object sender, MouseButtonEventArgs e)
    {
        if (!DropPopup.IsOpen) ShowSuggestions(Box.SelectionLength == Box.Text.Length ? "" : Box.Text);
    }

    private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(() => { if (!Box.IsKeyboardFocusWithin) DropPopup.IsOpen = false; }), DispatcherPriority.Input);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (!DropPopup.IsOpen) ShowSuggestions(Box.Text);
                if (List.Items.Count > 0) { List.SelectedIndex = Math.Min(List.SelectedIndex + 1, List.Items.Count - 1); List.ScrollIntoView(List.SelectedItem); }
                e.Handled = true;
                break;
            case Key.Up:
                if (DropPopup.IsOpen && List.Items.Count > 0)
                { List.SelectedIndex = Math.Max(List.SelectedIndex - 1, 0); List.ScrollIntoView(List.SelectedItem); e.Handled = true; }
                break;
            case Key.Enter:
            case Key.Tab:
                if (DropPopup.IsOpen && List.SelectedItem is Suggestion pick) { Accept(pick.Name); e.Handled = e.Key == Key.Enter; }
                else DropPopup.IsOpen = false;
                break;
            case Key.Escape:
                if (DropPopup.IsOpen) { DropPopup.IsOpen = false; e.Handled = true; }
                break;
        }
    }

    private void OnPick(object sender, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(List, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item?.Content is Suggestion s) { Accept(s.Name); Box.Focus(); }
    }
}
