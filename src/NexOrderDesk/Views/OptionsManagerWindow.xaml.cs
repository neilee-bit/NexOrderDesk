using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NexOrderDesk.Models;
using NexOrderDesk.Services;
namespace NexOrderDesk.Views;

/// <summary>
/// Add / rename / delete / reorder for any option list. Edits apply to the live lists immediately.
/// (Fix: the old window crashed on open - the colour box fired TextChanged during InitializeComponent, before the
/// swatch existed. Every handler here is now null-safe and the colour box is filled only after the window is built.)
/// </summary>
public partial class OptionsManagerWindow : Window
{
    private readonly SettingsService _s = SettingsService.Instance;
    private readonly ObservableCollection<string>? _strings;
    private readonly bool _colors;
    private bool _ready;

    public OptionsManagerWindow(string key)
    {
        InitializeComponent();
        _colors = key == "colors";
        var (title, list) = _s.GetList(key);
        _strings = _colors ? null : list;
        Heading.Text = title;
        Title = "Edit list — " + title;

        if (_colors)
        {
            Items.ItemsSource = _s.Colors;
            Items.ItemTemplate = (DataTemplate)Resources["ColorItem"];
            HexRow.Visibility = Visibility.Visible;
            Palette.ItemsSource = ColorMath.Presets;
            HexBox.Text = "#808080";
        }
        else Items.ItemsSource = _strings;

        _ready = true;
        UpdateSwatch(); UpdateCount();
        Loaded += (_, _) => NameBox.Focus();
    }

    private int Count => _colors ? _s.Colors.Count : _strings?.Count ?? 0;
    private string NameAt(int i) => _colors ? _s.Colors[i].Name : _strings![i];

    private bool Exists(string name, int except = -1)
    {
        for (int i = 0; i < Count; i++)
            if (i != except && NameAt(i).Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private bool Validate(string name, int except = -1)
    {
        if (name.Length == 0) { NameBox.Focus(); return false; }
        if (Exists(name, except)) { MessageBox.Show(this, $"\"{name}\" is already in the list.", "Duplicate"); return false; }
        if (_colors && ColorMath.NormalizeHex(HexBox.Text) == null)
        { MessageBox.Show(this, "Colour must look like #1B2A4A.", "Invalid colour"); return false; }
        return true;
    }

    private void OnAdd(object s, RoutedEventArgs e)
    {
        if (!_colors && _strings == null) return;
        var name = NameBox.Text.Trim(); if (!Validate(name)) return;
        if (_colors) _s.Colors.Add(new ColorOption(name.ToUpperInvariant(), ColorMath.NormalizeHex(HexBox.Text)!));
        else _strings!.Add(name);
        _s.Save();
        Items.SelectedIndex = -1;
        NameBox.Clear(); NameBox.Focus();
        Items.ScrollIntoView(Items.Items[Count - 1]);
        UpdateCount();
    }

    private void OnUpdate(object s, RoutedEventArgs e)
    {
        int i = Items.SelectedIndex; if (i < 0 || i >= Count) return;
        var name = NameBox.Text.Trim(); if (!Validate(name, i)) return;
        if (_colors)
        {
            _s.Colors[i].Name = name.ToUpperInvariant();
            _s.Colors[i].Hex = ColorMath.NormalizeHex(HexBox.Text)!;
            Items.Items.Refresh();
        }
        else _strings![i] = name;
        _s.Save();
        Items.SelectedIndex = i;
    }

    private void OnDelete(object s, RoutedEventArgs e)
    {
        int i = Items.SelectedIndex; if (i < 0 || i >= Count) return;
        if (MessageBox.Show(this, $"Delete \"{NameAt(i)}\" from the list?", "Delete", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        if (_colors) _s.Colors.RemoveAt(i); else _strings!.RemoveAt(i);
        _s.Save();
        if (Count > 0) Items.SelectedIndex = Math.Min(i, Count - 1); else NameBox.Clear();
        UpdateCount();
    }

    private void Move(int delta)
    {
        int i = Items.SelectedIndex, j = i + delta; if (i < 0 || j < 0 || j >= Count) return;
        if (_colors) _s.Colors.Move(i, j); else _strings!.Move(i, j);
        _s.Save();
        Items.SelectedIndex = j;
    }
    private void OnUp(object s, RoutedEventArgs e) => Move(-1);
    private void OnDown(object s, RoutedEventArgs e) => Move(1);
    private void OnClose(object s, RoutedEventArgs e) => Close();

    private void OnSelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        int i = Items.SelectedIndex;
        UpdateButton.IsEnabled = i >= 0;
        if (i < 0 || i >= Count) return;
        NameBox.Text = NameAt(i);
        if (_colors) HexBox.Text = _s.Colors[i].Hex;
        NameBox.SelectAll();
    }

    private void OnNameKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        OnAdd(s, e);                                   // Enter always adds; "Rename" changes the selected item
    }

    private void OnNameChanged(object s, TextChangedEventArgs e)
    {
        if (Placeholder != null && NameBox != null) Placeholder.Visibility = NameBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_ready && _colors && NameBox != null && HexBox != null && FoundText != null)
        {
            // the colour comes from the name (saved colours first, then the built-in colour names)
            var (hex, hint) = MaterialColorEntry.LookUp(NameBox.Text);
            HexBox.Text = hex ?? "#9AA0A9";
            FoundText.Text = NameBox.Text.Trim().Length == 0 ? "Type a colour name - the colour is found automatically"
                           : hex == null ? "Not a known colour - it gets a grey swatch" : hint;
        }
    }

    private void OnHexChanged(object s, TextChangedEventArgs e) => UpdateSwatch();

    private void UpdateSwatch()
    {
        if (!_ready || Swatch == null || HexBox == null) return;
        var hex = ColorMath.NormalizeHex(HexBox.Text);
        if (hex != null) Swatch.Background = ColorMath.Brush(hex);
    }

    private void OnPaletteClick(object s, RoutedEventArgs e)
    {
        if (s is FrameworkElement fe && fe.Tag is string hex) HexBox.Text = hex;
    }

    private void UpdateCount()
    {
        if (CountText != null) CountText.Text = Count == 1 ? "1 item" : $"{Count} items";
    }
}
