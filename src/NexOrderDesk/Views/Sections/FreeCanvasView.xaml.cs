using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using NexOrderDesk.Models;
using NexOrderDesk.Services;
using NexOrderDesk.Views;
namespace NexOrderDesk.Views.Sections;

/// <summary>
/// Free-form workspace. Text boxes: click once to type, drag the edge (or the ✥ tab) to move, corner to change the width.
/// Pictures: drag to move, corner to resize (aspect ratio kept). Ctrl+V pastes pictures or text, files can be dropped,
/// several pictures can be imported at once. After Submit / in print: no toolbar, no handles, read-only.
/// </summary>
public partial class FreeCanvasView : UserControl
{
    public static readonly DependencyProperty DocProperty = DependencyProperty.Register(
        nameof(Doc), typeof(CanvasDoc), typeof(FreeCanvasView), new PropertyMetadata(null, (d, e) => ((FreeCanvasView)d).Attach(e.OldValue as CanvasDoc, e.NewValue as CanvasDoc)));
    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable), typeof(bool), typeof(FreeCanvasView), new PropertyMetadata(true, (d, _) => ((FreeCanvasView)d).ApplyMode()));
    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(FreeCanvasView),
        new PropertyMetadata("Paste text or pictures here (Ctrl+V)\ndrag & drop picture files  ·  double-click for a text box", (d, _) => ((FreeCanvasView)d).ApplyMode()));

    public static readonly DependencyProperty ShowToolbarProperty = DependencyProperty.Register(
        nameof(ShowToolbar), typeof(bool), typeof(FreeCanvasView), new PropertyMetadata(true, (d, _) => ((FreeCanvasView)d).ApplyMode()));
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(FreeCanvasView), new PropertyMetadata(false, (d, _) => ((FreeCanvasView)d).ApplyMode()));

    public CanvasDoc? Doc { get => (CanvasDoc?)GetValue(DocProperty); set => SetValue(DocProperty, value); }
    /// <summary>False = no toolbar (front image cards).</summary>
    public bool ShowToolbar { get => (bool)GetValue(ShowToolbarProperty); set => SetValue(ShowToolbarProperty, value); }
    /// <summary>
    /// Picture-card mode (front sheet image cards): no frame / dots / hint, pictures only (no text boxes), the first picture fills
    /// the card, double-click browses for files.
    /// </summary>
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    /// <summary>Raised when the user clicks into this workspace (it becomes the Ctrl+V / Ctrl+C target).</summary>
    public event EventHandler? Activated;
    public bool IsEditable { get => (bool)GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    private static readonly string[] TextColors = { "#1E1E1E", "#D62828", "#2457C5" };
    private static readonly List<WeakReference<FreeCanvasView>> All = new();
    private static FreeCanvasView? _lastActive;
    // Ctrl+C keeps the original picture (full quality, SVG stays vector) for pasting back inside the app
    private static ImageSource? _copiedImage;
    private static Rect _copiedCrop = CanvasItem.FullCrop;
    private static string? _copiedId;
    private const string CopyFormat = "NexOrderDesk.PictureId";

    private sealed class ItemVisual
    {
        public Grid Host = null!;
        public Rectangle Outline = null!;
        public FrameworkElement[] Handles = Array.Empty<FrameworkElement>();
        public TextBox? Box;
        public ImageBrush? Picture;
    }

    private readonly Dictionary<CanvasItem, ItemVisual> _visuals = new();
    private CanvasItem? _selected;
    private int _z;

    public FreeCanvasView()
    {
        InitializeComponent();
        Loaded += (_, _) => { lock (All) { All.RemoveAll(w => !w.TryGetTarget(out _)); All.Add(new WeakReference<FreeCanvasView>(this)); } };
        Surface.SizeChanged += (_, e) => { if (e.HeightChanged || e.WidthChanged) KeepInside(); };
        Unloaded += (_, _) => { lock (All) All.RemoveAll(w => !w.TryGetTarget(out var v) || v == this); if (_lastActive == this) _lastActive = null; };
        ApplyMode();
    }

    /// <summary>The workspace Ctrl+V should go to: the last one used, if it is on screen and editable, else any visible one.</summary>
    public static FreeCanvasView? ActiveVisible()
    {
        if (_lastActive is { IsVisible: true, IsEditable: true }) return _lastActive;
        lock (All)
        {
            var visible = All.Select(w => w.TryGetTarget(out var v) ? v : null)
                             .Where(v => v is { IsVisible: true, IsEditable: true }).Cast<FreeCanvasView>().ToList();
            // nothing clicked yet: prefer a big workspace, then the first EMPTY picture card, then any
            return visible.FirstOrDefault(v => !v.Compact)
                   ?? visible.FirstOrDefault(v => v.Doc != null && v.Doc.Items.Count == 0)
                   ?? visible.FirstOrDefault();
        }
    }

    private void SetActive()
    {
        _lastActive = this;
        Activated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True when keyboard focus is in one of this workspace's own text boxes.</summary>
    public static bool IsCanvasTextBox(object? element) =>
        element is TextBox tb && tb.Tag is CanvasItem;

    private Size SurfaceSize => new(Math.Max(Surface.ActualWidth, 200), Math.Max(Surface.ActualHeight, 150));

    // ================================================================== document
    private void Attach(CanvasDoc? old, CanvasDoc? doc)
    {
        if (old != null) old.Items.CollectionChanged -= OnItemsChanged;
        foreach (var it in _visuals.Keys) it.PropertyChanged -= OnItemChanged;
        _visuals.Clear(); Surface.Children.Clear(); _selected = null;
        if (doc != null)
        {
            doc.Items.CollectionChanged += OnItemsChanged;
            foreach (var it in doc.Items) AddVisual(it);
        }
        ApplyMode();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var it in _visuals.Keys) it.PropertyChanged -= OnItemChanged;
            _visuals.Clear(); Surface.Children.Clear(); _selected = null;
            if (Doc != null) foreach (var it in Doc.Items) AddVisual(it);
        }
        else
        {
            if (e.OldItems != null) foreach (CanvasItem it in e.OldItems) RemoveVisual(it);
            if (e.NewItems != null) foreach (CanvasItem it in e.NewItems) AddVisual(it);
        }
        ApplyMode();
    }

    // ================================================================== visuals
    private void AddVisual(CanvasItem it)
    {
        if (_visuals.ContainsKey(it)) return;
        var v = new ItemVisual();
        var host = new Grid { Background = Brushes.Transparent, Width = it.W };
        v.Host = host;

        var outline = new Rectangle
        {
            StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false, Visibility = Visibility.Collapsed
        };
        outline.SetResourceReference(Shape.StrokeProperty, "AccentDarkBrush");
        v.Outline = outline;

        if (it.IsText)
        {
            // 5-unit draggable rim around the text box: grab the edge to move, click inside to type.
            var rim = new Border { Padding = new Thickness(5), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            var box = new TextBox
            {
                Tag = it, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, AcceptsTab = false,
                BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(1),
                MinWidth = 20, Cursor = Cursors.IBeam, FontFamily = new FontFamily("Segoe UI")
            };
            box.SetBinding(TextBox.TextProperty, new Binding(nameof(CanvasItem.Text)) { Source = it, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            box.GotKeyboardFocus += (_, _) => Select(it);
            rim.Child = box;
            v.Box = box;
            host.Children.Add(rim);
            HookDrag(rim, it, host);
            ApplyTextLook(it, v);
        }
        else
        {
            host.Height = it.H;
            host.Cursor = Cursors.SizeAll;
            // the ORIGINAL picture, showing only its crop area (Viewbox) - cropping never changes the picture itself
            var brush = new ImageBrush(it.Image) { Viewbox = it.Crop, ViewboxUnits = BrushMappingMode.RelativeToBoundingBox, Stretch = Stretch.Fill };
            var view = new Rectangle { Fill = brush, IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode(view, BitmapScalingMode.HighQuality);
            v.Picture = brush;
            host.Children.Add(view);
            HookDrag(host, it, host);
        }
        host.Children.Add(outline);

        // move tab (top-left), delete (top-right), resize grip (bottom-right)
        var tab = new Border
        {
            Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Cursor = Cursors.SizeAll, ToolTip = "Drag to move",
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(-9, -9, 0, 0),
            BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = "✥", FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0) }
        };
        tab.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
        HookDrag(tab, it, host);
        var del = new Button
        {
            Content = "✕", Width = 15, Height = 15, FontSize = 8, Padding = new Thickness(0), Cursor = Cursors.Hand, ToolTip = "Remove",
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -9, -9, 0),
            Style = TryFindResource("ActionButton") as Style
        };
        del.Click += (_, _) => Doc?.Items.Remove(it);
        var grip = new Thumb
        {
            Width = 10, Height = 10, Cursor = Cursors.SizeNWSE, ToolTip = it.IsText ? "Drag to change the width" : "Drag to resize (keeps proportions)",
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -5, -5)
        };
        grip.SetResourceReference(Control.BackgroundProperty, "AccentDarkBrush");
        grip.DragDelta += (_, e) => ResizeBy(it, host, e.HorizontalChange, e.VerticalChange);
        // rotation handle: round knob above the top edge
        var stem = new Rectangle { Width = 1.2, Height = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                                   Margin = new Thickness(0, -12, 0, 0), IsHitTestVisible = false };
        stem.SetResourceReference(Shape.FillProperty, "AccentDarkBrush");
        var knob = new Ellipse
        {
            Width = 12, Height = 12, Cursor = Cursors.Hand, StrokeThickness = 1.5, Fill = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -24, 0, 0),
            ToolTip = "Drag to rotate (snaps to straight angles, hold Shift for 15° steps) · double-click = straight"
        };
        knob.SetResourceReference(Shape.StrokeProperty, "AccentDarkBrush");
        HookRotate(knob, it, host);
        host.RenderTransformOrigin = new Point(0.5, 0.5);
        host.RenderTransform = new RotateTransform(it.Rotation);
        host.Children.Add(tab); host.Children.Add(del); host.Children.Add(grip); host.Children.Add(stem); host.Children.Add(knob);
        v.Handles = new FrameworkElement[] { tab, del, grip, stem, knob };
        if (!it.IsText)
        {
            // crop button (bottom-left) - also: double-click the picture
            var crop = new Button
            {
                Content = "\u2702", Width = 17, Height = 17, FontSize = 10, Padding = new Thickness(0), Cursor = Cursors.Hand,
                ToolTip = "Crop (or double-click the picture). The original is kept - restore it any time.",
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(-9, 0, 0, -9),
                Style = TryFindResource("ActionButton") as Style
            };
            crop.Click += (_, _) => CropItem(it);
            host.Children.Add(crop);
            v.Handles = v.Handles.Append(crop).ToArray();
        }
        foreach (var h in v.Handles) h.Visibility = Visibility.Collapsed;

        host.PreviewMouseLeftButtonDown += (_, _) => { Select(it); SetActive(); };
        Canvas.SetLeft(host, it.X); Canvas.SetTop(host, it.Y);
        Panel.SetZIndex(host, ++_z);

        _visuals[it] = v;
        it.PropertyChanged += OnItemChanged;
        Surface.Children.Add(host);
    }

    private void RemoveVisual(CanvasItem it)
    {
        if (!_visuals.TryGetValue(it, out var v)) return;
        it.PropertyChanged -= OnItemChanged;
        Surface.Children.Remove(v.Host);
        _visuals.Remove(it);
        if (_selected == it) _selected = null;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CanvasItem it || !_visuals.TryGetValue(it, out var v)) return;
        switch (e.PropertyName)
        {
            case nameof(CanvasItem.X): Canvas.SetLeft(v.Host, it.X); break;
            case nameof(CanvasItem.Y): Canvas.SetTop(v.Host, it.Y); break;
            case nameof(CanvasItem.W): v.Host.Width = it.W; break;
            case nameof(CanvasItem.H): if (!it.IsText) v.Host.Height = it.H; break;
            case nameof(CanvasItem.FontSize): case nameof(CanvasItem.IsBold): case nameof(CanvasItem.ColorHex): ApplyTextLook(it, v); break;
            case nameof(CanvasItem.Text): if (!IsEditable) ApplyMode(); break;
            case nameof(CanvasItem.Rotation): if (v.Host.RenderTransform is RotateTransform rt) rt.Angle = it.Rotation; break;
            case nameof(CanvasItem.Crop): if (v.Picture != null) v.Picture.Viewbox = it.Crop; break;
        }
    }

    private static void ApplyTextLook(CanvasItem it, ItemVisual v)
    {
        if (v.Box == null) return;
        v.Box.FontSize = it.FontSize;
        v.Box.FontWeight = it.IsBold ? FontWeights.Bold : FontWeights.SemiBold;
        v.Box.Foreground = ColorMath.Brush(it.ColorHex);
    }

    // ================================================================== selection / mode
    private void Select(CanvasItem? it)
    {
        _selected = it;
        foreach (var (item, v) in _visuals)
        {
            bool on = item == it && IsEditable;
            v.Outline.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            foreach (var h in v.Handles) h.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (on) Panel.SetZIndex(v.Host, ++_z);
        }
        if (it != null && it.IsText) ColorDot.Fill = ColorMath.Brush(it.ColorHex);
    }

    private void ApplyMode()
    {
        bool ed = IsEditable;
        Toolbar.Visibility = ed && ShowToolbar && !Compact ? Visibility.Visible : Visibility.Collapsed;
        // picture areas never crop: objects (and rotated corners) are drawn in full
        ClipHost.ClipToBounds = !Compact;
        Surface.ClipToBounds = !Compact;
        if (Compact)
        {
            Surface.Background = Brushes.Transparent;
            Frame.Background = Brushes.Transparent;
            Frame.BorderThickness = new Thickness(0);
        }
        else
        {
            Surface.Background = ed ? (Brush)Resources["DotGrid"] : Brushes.White;
            Frame.Background = Brushes.White;
            Frame.BorderThickness = new Thickness(1);
            Frame.BorderBrush = ed ? new SolidColorBrush(Color.FromRgb(0xD9, 0xDC, 0xE1)) : Brushes.Transparent;
        }
        bool empty = Doc == null || Doc.Items.Count == 0;
        EmptyHint.Text = EmptyText;
        EmptyHint.Visibility = ed && empty && !Compact ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (item, v) in _visuals)
        {
            if (v.Box != null)
            {
                v.Box.IsReadOnly = !ed; v.Box.IsHitTestVisible = ed; v.Box.Focusable = ed;
                // empty text boxes are not printed
                v.Host.Visibility = ed || item.HasContent ? Visibility.Visible : Visibility.Collapsed;
            }
            v.Host.IsHitTestVisible = ed;
        }
        if (!ed) Select(null); else Select(_selected);
    }

    // ================================================================== move / resize
    private void HookDrag(FrameworkElement handle, CanvasItem it, FrameworkElement host)
    {
        bool dragging = false; Point start = default; double sx = 0, sy = 0;
        handle.MouseLeftButtonDown += (_, e) =>
        {
            if (!IsEditable) return;
            Select(it); SetActive();
            Surface.Focus();                                   // so Ctrl+C / Ctrl+V / Del act on this object
            if (e.ClickCount == 2 && !it.IsText) { e.Handled = true; Dispatcher.BeginInvoke(new Action(() => CropItem(it))); return; }
            dragging = true; start = e.GetPosition(Surface); sx = it.X; sy = it.Y;
            handle.CaptureMouse(); e.Handled = true;
        };
        handle.MouseMove += (_, e) =>
        {
            if (!dragging) return;
            var p = e.GetPosition(Surface);
            var size = SurfaceSize;
            double w = host.ActualWidth, h = host.ActualHeight;
            it.X = Math.Clamp(sx + p.X - start.X, 0, Math.Max(0, size.Width - w));
            it.Y = Math.Clamp(sy + p.Y - start.Y, 0, Math.Max(0, size.Height - Math.Min(h, size.Height)));
        };
        handle.MouseLeftButtonUp += (_, _) => { if (!dragging) return; dragging = false; handle.ReleaseMouseCapture(); };
        handle.LostMouseCapture += (_, _) => dragging = false;
    }

    private void HookRotate(FrameworkElement knob, CanvasItem it, FrameworkElement host)
    {
        bool turning = false;
        knob.MouseLeftButtonDown += (_, e) =>
        {
            if (!IsEditable) return;
            Select(it); SetActive(); Surface.Focus();
            if (e.ClickCount == 2) { it.Rotation = 0; e.Handled = true; return; }
            turning = true; knob.CaptureMouse(); e.Handled = true;
        };
        knob.MouseMove += (_, e) =>
        {
            if (!turning) return;
            var p = e.GetPosition(Surface);
            double cx = it.X + host.ActualWidth / 2, cy = it.Y + host.ActualHeight / 2;
            double a = Math.Atan2(p.Y - cy, p.X - cx) * 180 / Math.PI + 90;      // knob sits above the centre
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) a = Math.Round(a / 15) * 15;
            else { double near = Math.Round(a / 90) * 90; if (Math.Abs(a - near) < 4) a = near; }
            it.Rotation = a;
        };
        knob.MouseLeftButtonUp += (_, _) => { if (!turning) return; turning = false; knob.ReleaseMouseCapture(); };
        knob.LostMouseCapture += (_, _) => turning = false;
    }

    private void ResizeBy(CanvasItem it, FrameworkElement host, double dx, double dy)
    {
        var size = SurfaceSize;
        double maxW = Math.Max(30, size.Width - it.X);
        if (it.IsText) { it.W = Math.Clamp(it.W + dx, 40, Math.Max(40, maxW)); return; }
        // pictures: follow the larger mouse movement, keep the aspect ratio, stay inside the sheet
        double r = it.AspectRatio;
        double byW = it.W + dx, byH = (it.H + dy) / r;
        double w = Math.Abs(dx) >= Math.Abs(dy * (1 / r)) ? byW : byH;
        double maxByH = Math.Max(20, (size.Height - it.Y) / r);
        w = Math.Clamp(w, 20, Math.Min(maxW, maxByH));
        it.W = w; it.H = w * r;
    }

    /// <summary>After the workspace gets smaller (e.g. back side A4 -> A5) pull objects back inside so nothing is cut off.</summary>
    private void KeepInside()
    {
        if (!IsEditable || Surface.ActualWidth <= 0 || Surface.ActualHeight <= 0) return;
        double sw = Surface.ActualWidth, sh = Surface.ActualHeight;
        foreach (var (it, v) in _visuals)
        {
            double w = it.IsText ? it.W : it.W, h = it.IsText ? v.Host.ActualHeight : it.H;
            if (!it.IsText && (w > sw || h > sh))
            {
                double k = Math.Min(sw / w, sh / h) * 0.98;
                it.W = w * k; it.H = h * k; w = it.W; h = it.H;
            }
            if (it.IsText && w > sw) { it.W = Math.Max(40, sw); w = it.W; }
            it.X = Math.Clamp(it.X, 0, Math.Max(0, sw - w));
            it.Y = Math.Clamp(it.Y, 0, Math.Max(0, sh - Math.Min(h, sh)));
        }
    }

    // ================================================================== adding content
    private Point NextSpot()
    {
        int n = Doc?.Items.Count ?? 0;
        double step = 22;
        var size = SurfaceSize;
        double x = 16 + (n % 8) * step, y = 16 + (n % 8) * step;
        return new Point(Math.Min(x, size.Width * 0.6), Math.Min(y, size.Height * 0.6));
    }

    public void AddText(string text, Point? at = null, bool focus = true)
    {
        if (Doc == null) return;
        var p = at ?? NextSpot();
        var size = SurfaceSize;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        double fs = 13;
        double width = Math.Clamp(lines.Max(l => l.Length) * fs * 0.56 + 24, 120, size.Width * 0.7);
        var it = CanvasItem.NewText(text.TrimEnd(), Math.Min(p.X, Math.Max(0, size.Width - width)), Math.Min(p.Y, Math.Max(0, size.Height - 30)), width, fs);
        Doc.Items.Add(it);
        Select(it); SetActive();
        if (focus && _visuals.TryGetValue(it, out var v) && v.Box != null)
            Dispatcher.BeginInvoke(new Action(() => { v.Box.Focus(); v.Box.CaretIndex = v.Box.Text.Length; }), DispatcherPriority.Input);
    }

    public void AddImages(IEnumerable<ImageSource> images, Point? at = null, Rect? crop = null)
    {
        var c = crop ?? CanvasItem.FullCrop;
        if (Doc == null) return;
        var size = SurfaceSize;
        var list = images.ToList();
        // a single picture on an empty area fills the whole area (front / back image, Flags / DTF workspace)
        bool single = Doc.Items.Count == 0 && list.Count == 1;
        CanvasItem? last = null;
        int i = 0;
        foreach (var src in list)
        {
            double iw = Math.Max(1, src.Width * c.Width), ih = Math.Max(1, src.Height * c.Height), r = ih / iw;
            double w, h;
            Point p;
            if (single || (Compact && Doc.Items.Count == 0))
            {
                // picture card: the first picture fills the card (like before), centred
                w = size.Width * 0.96; if (w * r > size.Height * 0.96) w = size.Height * 0.96 / r;
                h = w * r;
                p = new Point((size.Width - w) / 2, (size.Height - h) / 2);
            }
            else
            {
                // fit into ~45% of the width and ~60% of the height, never upscale (cards: ~70%)
                double fw = Compact ? 0.7 : 0.45, fh = Compact ? 0.7 : 0.6;
                w = Math.Min(iw, size.Width * fw);
                if (w * r > size.Height * fh) w = size.Height * fh / r;
                h = w * r;
                p = at.HasValue ? new Point(at.Value.X + i * 18, at.Value.Y + i * 18) : NextSpot();
            }
            double x = Math.Clamp(p.X, 0, Math.Max(0, size.Width - w)), y = Math.Clamp(p.Y, 0, Math.Max(0, size.Height - h));
            last = CanvasItem.NewImage(src, x, y, w, h, c);
            Doc.Items.Add(last);
            i++;
        }
        if (last != null) { Select(last); SetActive(); Surface.Focus(); }
        // the area was not laid out yet (sheet just opened): fit the single picture again once its real size is known
        if (single && last != null && (Surface.ActualWidth < 1 || Surface.ActualHeight < 1))
        {
            var item = last;
            Dispatcher.BeginInvoke(new Action(() => FitToArea(item)), DispatcherPriority.Loaded);
        }
    }

    /// <summary>Scales a picture to fill the area (keeping its proportions) and centres it.</summary>
    public void FitToArea(CanvasItem it)
    {
        if (Doc == null || !Doc.Items.Contains(it) || it.IsText) return;
        var size = SurfaceSize;
        double r = it.AspectRatio;
        double w = size.Width * 0.96; if (w * r > size.Height * 0.96) w = size.Height * 0.96 / r;
        double h = w * r;
        it.W = w; it.H = h;
        it.X = (size.Width - w) / 2; it.Y = (size.Height - h) / 2;
    }

    /// <summary>Ctrl+V: pictures first (files, PNG, bitmap), otherwise the clipboard text becomes a new movable text box.</summary>
    public bool Paste()
    {
        if (!IsEditable || Doc == null) return false;
        // a picture copied inside the app (Ctrl+C): paste the original, slightly offset from it
        try
        {
            if (_copiedImage != null && Clipboard.ContainsData(CopyFormat) && Clipboard.GetData(CopyFormat) as string == _copiedId)
            {
                Point? at = _selected is { IsText: false } sel ? new Point(sel.X + 18, sel.Y + 18) : null;
                AddImages(new[] { _copiedImage }, at, _copiedCrop);
                return true;
            }
        }
        catch { /* clipboard busy */ }
        var imgs = ImageLoader.FromClipboard();
        if (imgs.Count > 0) { AddImages(imgs); return true; }
        if (Compact) return false;                                   // picture cards take pictures only
        var text = ImageLoader.ClipboardText();
        if (!string.IsNullOrWhiteSpace(text)) { AddText(text, null, focus: false); return true; }
        return false;
    }

    /// <summary>Ctrl+C: copies the selected picture (or text box) to the clipboard - other apps get a normal picture.</summary>
    public bool CopySelected()
    {
        if (_selected == null) return false;
        try
        {
            if (_selected.IsText) { if (_selected.Text.Length == 0) return false; Clipboard.SetText(_selected.Text); return true; }
            if (_selected.Image is not { } src) return false;
            var bmp = ToBitmap(src, _selected.Crop);            // other apps get the cropped picture
            var data = new DataObject();
            data.SetImage(bmp);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
            var ms = new System.IO.MemoryStream(); png.Save(ms); ms.Position = 0;
            data.SetData("PNG", ms, false);
            _copiedId = Guid.NewGuid().ToString("N");
            data.SetData(CopyFormat, _copiedId);
            Clipboard.SetDataObject(data, true);
            _copiedImage = src;
            _copiedCrop = _selected.Crop;                      // inside the app: original + crop (crop stays editable)
            return true;
        }
        catch { return false; }
    }

    private static BitmapSource ToBitmap(ImageSource src, Rect crop)
    {
        if (src is BitmapSource b)
        {
            if (CanvasItem.IsFull(crop)) return b;
            // exact pixels of the crop area, no resampling
            int x = (int)Math.Floor(crop.X * b.PixelWidth), y = (int)Math.Floor(crop.Y * b.PixelHeight);
            int w = Math.Max(1, Math.Min(b.PixelWidth - x, (int)Math.Round(crop.Width * b.PixelWidth)));
            int h = Math.Max(1, Math.Min(b.PixelHeight - y, (int)Math.Round(crop.Height * b.PixelHeight)));
            var cb = new CroppedBitmap(b, new Int32Rect(x, y, w, h));
            cb.Freeze();
            return cb;
        }
        // vector (SVG): render the crop area sharply
        double vw = Math.Max(1, src.Width * crop.Width), vh = Math.Max(1, src.Height * crop.Height), k = Math.Min(4, Math.Max(1, 2000 / Math.Max(vw, vh)));
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, vw * k, vh * k));
            var brush = new ImageBrush(src) { Viewbox = crop, ViewboxUnits = BrushMappingMode.RelativeToBoundingBox, Stretch = Stretch.Fill };
            dc.DrawRectangle(brush, null, new Rect(0, 0, vw * k, vh * k));
        }
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(vw * k), (int)Math.Ceiling(vh * k), 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv); rtb.Freeze();
        return rtb;
    }

    /// <summary>
    /// Opens the crop tool for a picture. The picture keeps its on-sheet scale and centre; the visible area changes.
    /// "Restore original" in the tool brings back the whole picture.
    /// </summary>
    public void CropItem(CanvasItem it)
    {
        if (!IsEditable || it.IsText || it.Image == null) return;
        var old = it.Crop;
        var result = CropWindow.Edit(Window.GetWindow(this), it.Image, old);
        if (result is not Rect c || c == old) return;
        // same on-sheet scale: new size follows the change of the visible area
        double scale = it.W / Math.Max(1e-6, it.Image.Width * old.Width);
        double cx = it.X + it.W / 2, cy = it.Y + it.H / 2;
        it.Crop = c;
        double w = Math.Max(20, it.Image.Width * it.Crop.Width * scale), h = w * it.AspectRatio;
        // keep it inside the area
        var size = SurfaceSize;
        double k = Math.Min(1, Math.Min(size.Width / w, size.Height / h));
        w *= k; h *= k;
        it.W = w; it.H = h;
        it.X = Math.Clamp(cx - w / 2, 0, Math.Max(0, size.Width - w));
        it.Y = Math.Clamp(cy - h / 2, 0, Math.Max(0, size.Height - h));
        Select(it);
    }

    /// <summary>Open the file dialog and add the chosen pictures.</summary>
    public void Browse()
    {
        if (!IsEditable || Doc == null) return;
        SetActive();
        var dlg = new OpenFileDialog { Filter = ImageLoader.OpenFilter, Multiselect = true, Title = "Import pictures" };
        if (dlg.ShowDialog() != true) return;
        var imgs = dlg.FileNames.Select(f => ImageLoader.LoadFile(f)).OfType<ImageSource>().ToList();
        if (imgs.Count > 0) AddImages(imgs);
    }

    // ================================================================== events
    private void OnSurfaceDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEditable || e.OriginalSource != Surface) return;
        SetActive();
        Select(null);
        Surface.Focus();
        if (e.ClickCount == 2) { if (Compact) Browse(); else AddText("", e.GetPosition(Surface)); }
        e.Handled = true;
    }

    private void OnSurfaceKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsEditable || _selected == null || e.OriginalSource is TextBox) return;   // typing in a text box is not "delete object"
        if (e.Key is Key.Delete or Key.Back) { Doc?.Items.Remove(_selected); e.Handled = true; }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsEditable && (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText))
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!IsEditable) return;
        var at = e.GetPosition(Surface);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            var imgs = files.Where(ImageLoader.IsImageFile).Select(f => ImageLoader.LoadFile(f)).OfType<ImageSource>().ToList();
            if (imgs.Count > 0) AddImages(imgs, at);
        }
        else if (!Compact && e.Data.GetData(DataFormats.UnicodeText) is string text && !string.IsNullOrWhiteSpace(text)) AddText(text, at, false);
        e.Handled = true;
    }

    // ================================================================== toolbar
    private void OnAddText(object sender, RoutedEventArgs e) => AddText("");

    private void OnAddImages(object sender, RoutedEventArgs e) => Browse();

    private CanvasItem? SelectedText => _selected is { IsText: true } t ? t : null;
    private void OnFontMinus(object sender, RoutedEventArgs e) { if (SelectedText is { } t) t.FontSize -= 1; }
    private void OnFontPlus(object sender, RoutedEventArgs e) { if (SelectedText is { } t) t.FontSize += 1; }
    private void OnBold(object sender, RoutedEventArgs e) { if (SelectedText is { } t) t.IsBold = !t.IsBold; }
    private void OnColor(object sender, RoutedEventArgs e)
    {
        if (SelectedText is not { } t) return;
        int i = Array.IndexOf(TextColors, t.ColorHex.ToUpperInvariant());
        t.ColorHex = TextColors[(i + 1) % TextColors.Length];
        ColorDot.Fill = ColorMath.Brush(t.ColorHex);
    }
    private void OnRotate90(object sender, RoutedEventArgs e) { if (_selected != null) _selected.Rotation += 90; }
    private void OnCrop(object sender, RoutedEventArgs e) { if (_selected is { IsText: false } it) CropItem(it); }
    private void OnDeleteSelected(object sender, RoutedEventArgs e) { if (_selected != null) Doc?.Items.Remove(_selected); }
    private void OnClear(object sender, RoutedEventArgs e)
    {
        if (Doc == null || Doc.Items.Count == 0) return;
        if (MessageBox.Show("Remove every text box and picture from this workspace?", "Clear", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            Doc.Clear();
    }
}
