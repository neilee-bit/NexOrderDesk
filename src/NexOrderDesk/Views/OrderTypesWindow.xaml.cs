using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NexOrderDesk.Models;
using NexOrderDesk.Services;
namespace NexOrderDesk.Views;

/// <summary>Add / edit / reorder / delete the order types of the New Order menu, with live theme-colour preview.</summary>
public partial class OrderTypesWindow : Window
{
    private readonly SettingsService _s = SettingsService.Instance;
    private bool _loading = true;
    private readonly List<RadioButton> _iconButtons = new();

    public OrderTypesWindow()
    {
        InitializeComponent();
        Palette.ItemsSource = ColorMath.Presets;
        foreach (var key in OrderIcons.Keys)
        {
            var rb = new RadioButton
            {
                Style = (Style)Resources["ChoiceToggle"], GroupName = "Icon", Tag = key, Width = 38, Height = 34, Padding = new Thickness(0),
                ToolTip = OrderIcons.Title(key), Margin = new Thickness(0, 0, 5, 5),
                Content = new Viewbox { Width = 20, Height = 20, Child = new Path { Data = OrderIcons.Get(key), Fill = Brushes.Black } }
            };
            rb.Checked += (s, e) => { if (!_loading && Current is { } t) IconUpload.Clear(t); OnFieldChanged(s, e); };   // built-in icon chosen
            _iconButtons.Add(rb); IconRow.Children.Add(rb);
        }
        TypeList.ItemsSource = _s.OrderTypes;
        _loading = false;
        TypeList.SelectedIndex = _s.OrderTypes.Count > 0 ? 0 : -1;
        if (_s.OrderTypes.Count == 0) Editor.IsEnabled = false;
    }

    private OrderTypeDef? Current => TypeList.SelectedItem as OrderTypeDef;

    private void OnTypeSelected(object sender, SelectionChangedEventArgs e)
    {
        var t = Current;
        Editor.IsEnabled = t != null;
        if (t == null) return;
        _loading = true;
        NameBox.Text = t.Name;
        TitleBox.Text = t.SheetTitle;
        LayoutTops.IsChecked = t.Layout == SheetLayout.Tops;
        LayoutBottoms.IsChecked = t.Layout == SheetLayout.Bottoms;
        LayoutFlags.IsChecked = t.Layout == SheetLayout.Flags;
        LayoutDtf.IsChecked = t.Layout == SheetLayout.Dtf;
        LayoutJacket.IsChecked = t.Layout == SheetLayout.Jacket;
        foreach (var rb in _iconButtons) if (t.HasIconImage) rb.IsChecked = false;
        foreach (var rb in _iconButtons) rb.IsChecked = (string)rb.Tag == t.IconKey;
        HexBox.Text = t.AccentHex;
        _loading = false;
        RefreshPreview();
    }

    /// <summary>Every field applies straight to the selected type (and through it to open orders and the menu).</summary>
    private void OnFieldChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || Current is not { } t) return;
        if (NameBox.Text.Trim().Length > 0) t.Name = NameBox.Text.Trim();
        if (TitleBox.Text.Trim().Length > 0) t.SheetTitle = TitleBox.Text.Trim();
        t.Layout = LayoutBottoms.IsChecked == true ? SheetLayout.Bottoms
                 : LayoutFlags.IsChecked == true ? SheetLayout.Flags
                 : LayoutDtf.IsChecked == true ? SheetLayout.Dtf
                 : LayoutJacket.IsChecked == true ? SheetLayout.Jacket
                 : SheetLayout.Tops;
        var icon = t.HasIconImage ? null : _iconButtons.FirstOrDefault(b => b.IsChecked == true);
        if (icon != null) t.IconKey = (string)icon.Tag;
        var hex = ColorMath.NormalizeHex(HexBox.Text);
        if (hex != null) { t.AccentHex = hex; ThemeService.Apply(hex); }
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (Current is not { } t) return;
        var hex = ColorMath.NormalizeHex(HexBox.Text) ?? t.AccentHex;
        Preview.Background = ColorMath.Brush(hex);
        Swatch.Background = ColorMath.Brush(hex);
        PreviewTitle.Text = string.IsNullOrWhiteSpace(TitleBox.Text) ? t.SheetTitle : TitleBox.Text.Trim().ToUpperInvariant();
        PreviewIcon.Data = t.Icon;
        PreviewImage.Source = t.IconImage;
        PreviewIcon.Visibility = t.HasIconImage ? Visibility.Collapsed : Visibility.Visible;
        ClearIconBtn.IsEnabled = t.HasIconImage;
    }

    private void OnUploadIcon(object sender, RoutedEventArgs e)
    {
        if (Current is not { } t) return;
        if (!IconUpload.Choose(t, this)) return;
        _loading = true; foreach (var rb in _iconButtons) rb.IsChecked = false; _loading = false;
        _s.Save(); RefreshPreview();
    }

    private void OnClearIcon(object sender, RoutedEventArgs e)
    {
        if (Current is not { } t) return;
        IconUpload.Clear(t);
        _loading = true; foreach (var rb in _iconButtons) rb.IsChecked = (string)rb.Tag == t.IconKey; _loading = false;
        _s.Save(); RefreshPreview();
    }

    private void OnPaletteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string hex) HexBox.Text = hex;
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        var used = _s.OrderTypes.Select(t => t.AccentHex).ToHashSet();
        var color = ColorMath.Presets.FirstOrDefault(p => !used.Contains(p)) ?? "#00A6A6";
        var t = new OrderTypeDef(Guid.NewGuid().ToString("N"), "New Order Type", "NEW ORDER SPECIFICATION SHEET", color, SheetLayout.Tops, "tshirt");
        _s.OrderTypes.Add(t);
        _s.Save();
        TypeList.SelectedItem = t;
        TypeList.ScrollIntoView(t);
        NameBox.Focus(); NameBox.SelectAll();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Current is not { } t) return;
        if (_s.OrderTypes.Count <= 1) { MessageBox.Show(this, "At least one order type must stay in the list.", "Order types"); return; }
        if (MessageBox.Show(this, $"Delete the order type \"{t.Name}\"?\nOpen orders of this type stay open.", "Delete order type",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        int i = _s.OrderTypes.IndexOf(t);
        _s.OrderTypes.Remove(t);
        _s.Save();
        TypeList.SelectedIndex = Math.Min(i, _s.OrderTypes.Count - 1);
    }

    private void Move(int delta)
    {
        int i = TypeList.SelectedIndex, j = i + delta;
        if (i < 0 || j < 0 || j >= _s.OrderTypes.Count) return;
        _s.OrderTypes.Move(i, j);
        _s.Save();
        TypeList.SelectedIndex = j;
    }
    private void OnUp(object sender, RoutedEventArgs e) => Move(-1);
    private void OnDown(object sender, RoutedEventArgs e) => Move(1);
    private void OnClose(object sender, RoutedEventArgs e) { _s.Save(); Close(); }
}
