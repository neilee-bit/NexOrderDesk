using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NexOrderDesk.Core;
using NexOrderDesk.Models;
using NexOrderDesk.Services;
namespace NexOrderDesk.Views.Sections;

/// <summary>
/// Advanced colour picker (2.9). Palette: every built-in colour grouped into families, plus "My colours" (the saved list).
/// Custom: any colour from the shade square and hue bar; the nearest colour name is suggested. Shades: lighter / darker
/// versions of the current colour. A click on a swatch uses that colour straight away.
/// </summary>
public partial class ColorPicker : UserControl
{
    public const string MyColours = "My colours";
    private double _h, _s, _v;
    private string _hex = "#FFFFFF";
    private bool _nameEdited, _busy;

    /// <summary>Raised with (name, #RRGGBB) when a colour is chosen.</summary>
    public event Action<string, string>? Picked;

    public ColorPicker()
    {
        InitializeComponent();
        Families.ItemsSource = new[] { MyColours }.Concat(ColorFamilies.Names).ToList();
        NameBox.TextChanged += (_, _) => { if (!_busy) _nameEdited = true; };
        SvArea.SizeChanged += (_, _) => PlaceThumbs();
        HueArea.SizeChanged += (_, _) => PlaceThumbs();
    }

    public string CurrentHex => _hex;
    public string CurrentName => NameBox.Text.Trim();
    public IReadOnlyList<ColorFamilies.Swatch> VisibleSwatches => Swatches.Items.OfType<ColorFamilies.Swatch>().ToList();
    public IReadOnlyList<ColorFamilies.Swatch> ShadeSwatches => ShadeRow.Items.OfType<ColorFamilies.Swatch>().ToList();

    /// <summary>Opens on the current colour: its family is selected and its shades are shown.</summary>
    public void Start(string? hex, string? name)
    {
        var h = ColorMath.NormalizeHex(hex) ?? "#FFFFFF";
        SetColour(h, string.IsNullOrWhiteSpace(name) ? ColorFamilies.NearestName(h) : name!.Trim().ToUpperInvariant(), fromName: true);
        _nameEdited = false;
        ShowFamily(SettingsService.Instance.Colors.Count > 0 && hex == null ? MyColours : ColorFamilies.FamilyOf(h));
        PaletteTab.IsChecked = true;
    }

    public void ShowFamily(string family)
    {
        List<ColorFamilies.Swatch> list = family == MyColours
            ? SettingsService.Instance.Colors.Select(c => new ColorFamilies.Swatch(c.Name, c.Hex)).ToList()
            : ColorFamilies.InFamily(family).ToList();
        Swatches.ItemsSource = list;
        Swatches.Tag = family;
        EmptyText.Visibility = family == MyColours && list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HighlightChip();
        // the chips may not exist yet (picker just opened): highlight again once they are built
        Dispatcher.BeginInvoke(new Action(HighlightChip), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public string CurrentFamily => Swatches.Tag as string ?? "";

    private void HighlightChip()
    {
        foreach (var rb in FindChips()) rb.IsChecked = Equals(rb.Content, Swatches.Tag);
    }

    private IEnumerable<RadioButton> FindChips()
    {
        for (int i = 0; i < Families.Items.Count; i++)
            if (Families.ItemContainerGenerator.ContainerFromIndex(i) is ContentPresenter cp && VisualTreeHelper.GetChildrenCount(cp) > 0
                && VisualTreeHelper.GetChild(cp, 0) is RadioButton rb) yield return rb;
    }

    /// <summary>Makes a colour current (preview, shades, square, name).</summary>
    public void SetColour(string hex, string? name, bool fromName = false)
    {
        _hex = ColorMath.NormalizeHex(hex) ?? _hex;
        var c = ColorMath.Parse(_hex);
        (_h, _s, _v) = ColorFamilies.ToHsv(c);
        Preview.Background = ColorMath.Brush(_hex);
        HueFill.Background = new SolidColorBrush(ColorFamilies.FromHsv(_h, 1, 1));
        var nearest = ColorFamilies.NearestName(_hex);
        NearText.Text = "Closest colour name: " + nearest;
        if (!_nameEdited || fromName)
        {
            _busy = true; NameBox.Text = name ?? nearest; _busy = false;
        }
        ShadeRow.ItemsSource = ColorFamilies.Shades(_hex, NameBox.Text);
        PlaceThumbs();
    }

    private void PlaceThumbs()
    {
        double w = SvArea.ActualWidth, h = SvArea.ActualHeight;
        if (w > 0 && h > 0)
        {
            Canvas.SetLeft(SvThumb, _s * w - SvThumb.Width / 2);
            Canvas.SetTop(SvThumb, (1 - _v) * h - SvThumb.Height / 2);
        }
        double hh = HueArea.ActualHeight;
        if (hh > 0) Canvas.SetTop(HueThumb, _h / 360 * hh - HueThumb.Height / 2);
    }

    /// <summary>Uses a colour: tells the material row and closes.</summary>
    public void Use(string name, string hex)
    {
        var n = (name ?? "").Trim().ToUpperInvariant();
        if (n.Length == 0) n = ColorFamilies.NearestName(hex);
        Picked?.Invoke(n, ColorMath.NormalizeHex(hex) ?? "#FFFFFF");
    }

    // ------------------------------------------------------------------ events
    private void OnTab(object sender, RoutedEventArgs e)
    {
        if (PalettePanel == null || CustomPanel == null) return;
        bool custom = CustomTab.IsChecked == true;
        PalettePanel.Visibility = custom ? Visibility.Collapsed : Visibility.Visible;
        CustomPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        Dispatcher.BeginInvoke(new Action(PlaceThumbs), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnFamily(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Content: string f } && !Equals(Swatches.Tag, f)) { Swatches.Tag = f; ShowFamily(f); }
    }

    private void OnSwatch(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ColorFamilies.Swatch sw) Use(sw.Name, sw.Hex);
    }

    private void OnUse(object sender, RoutedEventArgs e) => Use(NameBox.Text, _hex);

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = CurrentName.Length > 0 ? CurrentName : ColorFamilies.NearestName(_hex);
        var s = SettingsService.Instance;
        var existing = s.Colors.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) existing.Hex = _hex; else s.Colors.Add(new ColorOption(name, _hex));
        s.Save();
        Use(name, _hex);
    }

    private void FromSquare(Point p)
    {
        double w = Math.Max(1, SvArea.ActualWidth), h = Math.Max(1, SvArea.ActualHeight);
        _s = Math.Clamp(p.X / w, 0, 1); _v = 1 - Math.Clamp(p.Y / h, 0, 1);
        CustomChanged();
    }

    private void FromHue(Point p)
    {
        _h = Math.Clamp(p.Y / Math.Max(1, HueArea.ActualHeight), 0, 0.9999) * 360;
        CustomChanged();
    }

    /// <summary>Custom tab: the square / hue bar moved.</summary>
    public void SetCustom(double hue, double sat, double val)
    {
        _h = hue; _s = sat; _v = val;
        CustomChanged();
    }

    private void CustomChanged()
    {
        var (h, s, v) = (_h, _s, _v);
        var c = ColorFamilies.FromHsv(h, s, v);
        _hex = ColorMath.ToHex(c);
        Preview.Background = ColorMath.Brush(_hex);
        HueFill.Background = new SolidColorBrush(ColorFamilies.FromHsv(h, 1, 1));
        var nearest = ColorFamilies.NearestName(_hex);
        NearText.Text = "Closest colour name: " + nearest;
        if (!_nameEdited) { _busy = true; NameBox.Text = nearest; _busy = false; }
        ShadeRow.ItemsSource = ColorFamilies.Shades(_hex, NameBox.Text);
        PlaceThumbs();
        (_h, _s, _v) = (h, s, v);       // keep the exact position (the hex rounds)
    }

    private void OnSvDown(object sender, MouseButtonEventArgs e) { SvArea.CaptureMouse(); FromSquare(e.GetPosition(SvArea)); }
    private void OnSvMove(object sender, MouseEventArgs e) { if (SvArea.IsMouseCaptured) FromSquare(e.GetPosition(SvArea)); }
    private void OnHueDown(object sender, MouseButtonEventArgs e) { HueArea.CaptureMouse(); FromHue(e.GetPosition(HueArea)); }
    private void OnHueMove(object sender, MouseEventArgs e) { if (HueArea.IsMouseCaptured) FromHue(e.GetPosition(HueArea)); }
    private void OnUp(object sender, MouseButtonEventArgs e) => (sender as UIElement)?.ReleaseMouseCapture();
}
