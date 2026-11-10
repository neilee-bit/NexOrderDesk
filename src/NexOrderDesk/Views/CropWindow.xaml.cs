using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace NexOrderDesk.Views;

/// <summary>
/// Crop editor. Works on the ORIGINAL picture and only returns a crop rectangle (fractions 0..1), so cropping never
/// changes or loses the uploaded picture: it can be re-cropped or restored at any time.
/// Drag inside the box = move it, edges / corners = resize, outside the box = move the picture, wheel / slider = zoom.
/// </summary>
public partial class CropWindow : Window
{
    private readonly ImageSource _img;
    private readonly double _w, _h;                 // picture size in WPF units
    private readonly double _pxW, _pxH;             // picture size in pixels (for the info line)
    private Rect _crop;
    private Rect? _result;
    private double _zoom = 1, _ox, _oy;
    private double _ratio;                          // wanted width/height in pixels, 0 = free
    private bool _fitted, _syncSlider;
    private readonly List<Rectangle> _handles = new();

    private enum Mode { None, Move, Pan, Resize }
    private Mode _mode;
    private string _handle = "";
    private Point _start;
    private Rect _startCrop;
    private double _startOx, _startOy;

    private CropWindow(ImageSource img, Rect crop)
    {
        InitializeComponent();
        _img = img;
        _w = Math.Max(1, img.Width); _h = Math.Max(1, img.Height);
        if (img is BitmapSource b) { _pxW = b.PixelWidth; _pxH = b.PixelHeight; } else { _pxW = _w; _pxH = _h; }
        _crop = crop;
        Pic.Source = img;

        foreach (var (tag, cur) in new[] { ("nw", Cursors.SizeNWSE), ("n", Cursors.SizeNS), ("ne", Cursors.SizeNESW), ("e", Cursors.SizeWE),
                                           ("se", Cursors.SizeNWSE), ("s", Cursors.SizeNS), ("sw", Cursors.SizeNESW), ("w", Cursors.SizeWE) })
        {
            var r = new Rectangle { Width = 11, Height = 11, Fill = Brushes.White, Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)), StrokeThickness = 1, Tag = tag, Cursor = cur };
            _handles.Add(r); Stage.Children.Add(r);
        }
        Loaded += (_, _) => Stage.Focus();
    }

    /// <summary>Opens the editor. Returns the new crop, or null when cancelled.</summary>
    public static Rect? Edit(Window? owner, ImageSource img, Rect crop)
    {
        var w = new CropWindow(img, crop);
        if (owner != null && owner.IsLoaded) w.Owner = owner;
        return w.ShowDialog() == true ? w._result : null;
    }

    // ================================================================ layout
    private double ScreenW => _w * _zoom;
    private double ScreenH => _h * _zoom;

    private Rect ScreenCrop => new(_ox + _crop.X * ScreenW, _oy + _crop.Y * ScreenH, _crop.Width * ScreenW, _crop.Height * ScreenH);

    private void Layout()
    {
        double sw = Stage.ActualWidth, sh = Stage.ActualHeight;
        if (sw <= 0 || sh <= 0) return;
        Pic.Width = ScreenW; Pic.Height = ScreenH;
        Canvas.SetLeft(Pic, _ox); Canvas.SetTop(Pic, _oy);

        var s = ScreenCrop;
        Canvas.SetLeft(Box, s.X); Canvas.SetTop(Box, s.Y); Box.Width = Math.Max(1, s.Width); Box.Height = Math.Max(1, s.Height);
        var shade = new GeometryCollection { new RectangleGeometry(new Rect(0, 0, sw, sh)), new RectangleGeometry(s) };
        Shade.Data = new GeometryGroup { FillRule = FillRule.EvenOdd, Children = shade };
        var lines = new GeometryCollection();
        for (int i = 1; i <= 2; i++)
        {
            lines.Add(new LineGeometry(new Point(s.X + s.Width * i / 3, s.Y), new Point(s.X + s.Width * i / 3, s.Bottom)));
            lines.Add(new LineGeometry(new Point(s.X, s.Y + s.Height * i / 3), new Point(s.Right, s.Y + s.Height * i / 3)));
        }
        Thirds.Data = new GeometryGroup { Children = lines };

        foreach (var h in _handles)
        {
            var tag = (string)h.Tag;
            double x = tag.Contains('w') ? s.Left : tag.Contains('e') ? s.Right : s.X + s.Width / 2;
            double y = tag.Contains('n') ? s.Top : tag.Contains('s') ? s.Bottom : s.Y + s.Height / 2;
            Canvas.SetLeft(h, x - h.Width / 2); Canvas.SetTop(h, y - h.Height / 2);
        }

        _syncSlider = true; ZoomSlider.Value = Math.Clamp(_zoom, ZoomSlider.Minimum, ZoomSlider.Maximum); _syncSlider = false;
        int cw = (int)Math.Round(_crop.Width * _pxW), ch = (int)Math.Round(_crop.Height * _pxH);
        InfoText.Text = $"Crop: {cw} × {ch} px of {(int)_pxW} × {(int)_pxH}   ·   zoom {_zoom * 100:0}%   ·   " +
                        "drag inside the box to move it, edges / corners to resize, outside the box to move the picture";
    }

    private void Fit()
    {
        double sw = Stage.ActualWidth, sh = Stage.ActualHeight;
        if (sw <= 0 || sh <= 0) return;
        _zoom = Math.Clamp(Math.Min(sw / _w, sh / _h) * 0.9, ZoomSlider.Minimum, ZoomSlider.Maximum);
        _ox = (sw - ScreenW) / 2; _oy = (sh - ScreenH) / 2;
        Layout();
    }

    /// <summary>Zoom keeping the stage point <paramref name="at"/> still.</summary>
    private void ZoomTo(double z, Point at)
    {
        z = Math.Clamp(z, ZoomSlider.Minimum, ZoomSlider.Maximum);
        _ox = at.X - (at.X - _ox) * z / _zoom;
        _oy = at.Y - (at.Y - _oy) * z / _zoom;
        _zoom = z;
        Layout();
    }

    private Point StageCentre => new(Stage.ActualWidth / 2, Stage.ActualHeight / 2);

    // ================================================================ crop maths
    /// <summary>Wanted crop width / height in 0..1 units for the chosen shape (0 = free).</summary>
    private double NormRatio => _ratio <= 0 ? 0 : _ratio * _pxH / _pxW;

    private static Rect Clamp(Rect r)
    {
        double w = Math.Clamp(r.Width, 0.01, 1), h = Math.Clamp(r.Height, 0.01, 1);
        double x = Math.Clamp(r.X, 0, 1 - w), y = Math.Clamp(r.Y, 0, 1 - h);
        return new Rect(x, y, w, h);
    }

    /// <summary>Make the crop follow the chosen shape, centred on the current crop, as big as fits.</summary>
    private void ApplyRatio()
    {
        double nr = NormRatio;
        if (nr <= 0) return;
        double cx = _crop.X + _crop.Width / 2, cy = _crop.Y + _crop.Height / 2;
        double w = _crop.Width, h = w / nr;
        if (h > _crop.Height) { h = _crop.Height; w = h * nr; }
        if (w > 1) { w = 1; h = w / nr; }
        if (h > 1) { h = 1; w = h * nr; }
        _crop = Clamp(new Rect(cx - w / 2, cy - h / 2, w, h));
    }

    private Rect Resize(Rect start, string tag, double dx, double dy)
    {
        const double min = 0.02;
        double l = start.Left, t = start.Top, r = start.Right, b = start.Bottom;
        if (tag.Contains('w')) l = Math.Clamp(l + dx, 0, r - min);
        if (tag.Contains('e')) r = Math.Clamp(r + dx, l + min, 1);
        if (tag.Contains('n')) t = Math.Clamp(t + dy, 0, b - min);
        if (tag.Contains('s')) b = Math.Clamp(b + dy, t + min, 1);
        double nr = NormRatio;
        if (nr <= 0) return new Rect(l, t, r - l, b - t);

        // keep the chosen shape: corners follow the width, edges keep the other side centred
        double w = r - l, h = b - t;
        bool horizontalEdge = tag is "n" or "s";
        if (horizontalEdge) w = h * nr; else h = w / nr;
        // limit to the picture
        double maxW = tag.Contains('w') ? start.Right : tag.Contains('e') ? 1 - start.Left : 1;
        double maxH = tag.Contains('n') ? start.Bottom : tag.Contains('s') ? 1 - start.Top : 1;
        if (w > maxW) { w = maxW; h = w / nr; }
        if (h > maxH) { h = maxH; w = h * nr; }
        double x = tag.Contains('w') ? start.Right - w : tag.Contains('e') ? start.Left : start.X + start.Width / 2 - w / 2;
        double y = tag.Contains('n') ? start.Bottom - h : tag.Contains('s') ? start.Top : start.Y + start.Height / 2 - h / 2;
        return Clamp(new Rect(x, y, w, h));
    }

    // ================================================================ mouse
    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Stage);
        _startCrop = _crop; _startOx = _ox; _startOy = _oy;
        if (e.OriginalSource is Rectangle { Tag: string tag } && _handles.Any(h => (string)h.Tag == tag)) { _mode = Mode.Resize; _handle = tag; }
        else if (e.OriginalSource == Box) _mode = Mode.Move;
        else _mode = Mode.Pan;
        if (e.ClickCount == 2 && _mode == Mode.Pan) { Fit(); _mode = Mode.None; return; }
        Stage.CaptureMouse();
        Stage.Focus();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_mode == Mode.None) return;
        var p = e.GetPosition(Stage);
        double dx = (p.X - _start.X) / ScreenW, dy = (p.Y - _start.Y) / ScreenH;
        switch (_mode)
        {
            case Mode.Move:
                _crop = Clamp(new Rect(_startCrop.X + dx, _startCrop.Y + dy, _startCrop.Width, _startCrop.Height));
                break;
            case Mode.Resize:
                _crop = Resize(_startCrop, _handle, dx, dy);
                break;
            case Mode.Pan:
                _ox = _startOx + p.X - _start.X; _oy = _startOy + p.Y - _start.Y;
                break;
        }
        Layout();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_mode == Mode.None) return;
        _mode = Mode.None;
        Stage.ReleaseMouseCapture();
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomTo(_zoom * Math.Pow(1.15, e.Delta / 120.0), e.GetPosition(Stage));
        e.Handled = true;
    }

    private void OnStageSize(object sender, SizeChangedEventArgs e)
    {
        if (!_fitted) { _fitted = true; Fit(); } else Layout();
    }

    // ================================================================ tools
    private void OnZoomSlider(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncSlider || !IsLoaded) return;
        ZoomTo(e.NewValue, StageCentre);
    }
    private void OnZoomIn(object sender, RoutedEventArgs e) => ZoomTo(_zoom * 1.25, StageCentre);
    private void OnZoomOut(object sender, RoutedEventArgs e) => ZoomTo(_zoom / 1.25, StageCentre);
    private void OnFit(object sender, RoutedEventArgs e) => Fit();
    private void OnActual(object sender, RoutedEventArgs e)
    {
        // 100% = one picture pixel per screen pixel, centred on the crop
        double z = _img is BitmapSource b && b.PixelWidth > 0 ? _w / b.PixelWidth : 1;
        _zoom = Math.Clamp(z, ZoomSlider.Minimum, ZoomSlider.Maximum);
        var c = new Point(_crop.X + _crop.Width / 2, _crop.Y + _crop.Height / 2);
        _ox = Stage.ActualWidth / 2 - c.X * ScreenW; _oy = Stage.ActualHeight / 2 - c.Y * ScreenH;
        Layout();
    }

    private void OnRatio(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string t }) return;
        _ratio = t == "orig" ? _pxW / _pxH : double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
        if (!IsLoaded) return;
        ApplyRatio();
        Layout();
    }

    private void OnRestore(object sender, RoutedEventArgs e)
    {
        RFree.IsChecked = true; _ratio = 0;
        _crop = new Rect(0, 0, 1, 1);
        Fit();
    }

    private void OnApply(object sender, RoutedEventArgs e) { _result = _crop; DialogResult = true; }
    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; }
}
