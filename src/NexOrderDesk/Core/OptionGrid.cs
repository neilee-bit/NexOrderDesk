using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace NexOrderDesk.Core;

/// <summary>
/// Shared option layout (2.9.1) for every selectable option area (print types, additional custom, jacket extras …).
/// Works out how many columns fit from the real text widths, adds rows automatically, balances the rows and gives every
/// option the same full cell. When a name is longer than a cell the option card wraps it onto two lines - never clipped.
/// The panel asks for exactly the height its rows need, so a box sized "Auto" always has room for every option.
/// </summary>
public class OptionGrid : Panel
{
    public static readonly DependencyProperty MinCellWidthProperty = DependencyProperty.Register(nameof(MinCellWidth), typeof(double), typeof(OptionGrid),
        new FrameworkPropertyMetadata(60.0, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(nameof(MaxColumns), typeof(int), typeof(OptionGrid),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty MinCellHeightProperty = DependencyProperty.Register(nameof(MinCellHeight), typeof(double), typeof(OptionGrid),
        new FrameworkPropertyMetadata(22.0, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(nameof(Gap), typeof(double), typeof(OptionGrid),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>A cell is never narrower than this (but grows with the longest option name).</summary>
    public double MinCellWidth { get => (double)GetValue(MinCellWidthProperty); set => SetValue(MinCellWidthProperty, value); }
    public int MaxColumns { get => (int)GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }
    public double MinCellHeight { get => (double)GetValue(MinCellHeightProperty); set => SetValue(MinCellHeightProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private int _cols = 1, _rows;
    private double _cellH;

    private List<UIElement> Visible() => InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToList();

    protected override Size MeasureOverride(Size available)
    {
        var kids = Visible();
        if (kids.Count == 0) { _rows = 0; return new Size(0, 0); }

        // 1. natural width of the longest option
        double natural = MinCellWidth;
        foreach (var k in kids) { k.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); natural = Math.Max(natural, k.DesiredSize.Width); }

        // 2. columns that fit, then balanced rows (9 options in max 4 columns -> 3 rows of 3, not 4+4+1)
        double width = double.IsInfinity(available.Width) ? natural * Math.Min(kids.Count, Math.Max(1, MaxColumns)) : available.Width;
        int fit = Math.Max(1, (int)Math.Floor((width + Gap) / (natural + Gap)));
        int max = MaxColumns > 0 ? Math.Min(MaxColumns, kids.Count) : kids.Count;
        int cols = Math.Max(1, Math.Min(fit, max));
        // if even one column is narrower than the text, the cards wrap their text (step 3)
        _rows = (int)Math.Ceiling(kids.Count / (double)cols);
        _cols = (int)Math.Ceiling(kids.Count / (double)_rows);

        // 3. measure every option in its real cell: wrapped text makes a cell taller
        double cellW = Math.Max(1, (width - Gap * (_cols - 1)) / _cols);
        _cellH = MinCellHeight;
        foreach (var k in kids) { k.Measure(new Size(cellW, double.PositiveInfinity)); _cellH = Math.Max(_cellH, k.DesiredSize.Height); }

        return new Size(double.IsInfinity(available.Width) ? cellW * _cols + Gap * (_cols - 1) : width, _rows * _cellH + Gap * (_rows - 1));
    }

    protected override Size ArrangeOverride(Size final)
    {
        var kids = Visible();
        if (kids.Count == 0) return final;
        double cellW = Math.Max(1, (final.Width - Gap * (_cols - 1)) / _cols);
        for (int i = 0; i < kids.Count; i++)
        {
            int r = i / _cols, c = i % _cols;
            kids[i].Arrange(new Rect(c * (cellW + Gap), r * (_cellH + Gap), cellW, _cellH));
        }
        foreach (UIElement k in InternalChildren) if (k.Visibility == Visibility.Collapsed) k.Arrange(new Rect());
        return new Size(final.Width, _rows * _cellH + Gap * (_rows - 1));
    }
}

/// <summary>
/// Last-resort guard (2.9.1): its content normally fills the box exactly as before. Only when the content needs more
/// height than the box has (for example a very long option list), the whole content is scaled down a little to fit -
/// so nothing on the sheet is ever cut off or hidden.
/// </summary>
public class FitBox : Decorator
{
    private double _scale = 1, _need;
    /// <summary>1 = normal size; below 1 = the content was scaled down to fit.</summary>
    public double Scale => _scale;

    protected override Size MeasureOverride(Size available)
    {
        if (Child == null) return new Size();
        // how tall does the content need to be at this width?
        Child.Measure(new Size(available.Width, double.PositiveInfinity));
        _need = Child.DesiredSize.Height;
        if (!double.IsInfinity(available.Height) && available.Height > 0 && _need > available.Height + 0.5)
        {
            _scale = available.Height / _need;
            Child.Measure(new Size(available.Width / _scale, _need));
        }
        else
        {
            _scale = 1;
            Child.Measure(available);
        }
        var d = Child.DesiredSize;
        return new Size(Math.Min(d.Width * _scale, available.Width), Math.Min(d.Height * _scale, available.Height));
    }

    protected override Size ArrangeOverride(Size final)
    {
        if (Child == null) return final;
        if (_scale < 1)
        {
            double s = Math.Min(_scale, final.Height / Math.Max(1, _need));
            Child.Arrange(new Rect(0, 0, final.Width / s, _need));
            Child.RenderTransform = new ScaleTransform(s, s);
        }
        else
        {
            Child.Arrange(new Rect(final));
            Child.RenderTransform = Transform.Identity;
        }
        return final;
    }
}
