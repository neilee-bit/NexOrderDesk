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

        double natural = MinCellWidth;
        foreach (var k in kids) { k.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); natural = Math.Max(natural, k.DesiredSize.Width); }
        int max = Math.Max(1, MaxColumns > 0 ? Math.Min(MaxColumns, kids.Count) : kids.Count);
        double width = double.IsInfinity(available.Width) ? natural * max + Gap * (max - 1) : available.Width;

        // try every column count that keeps cells at least MinCellWidth wide; keep the one with the lowest total height
        // (a long name then wraps onto two lines instead of forcing extra rows); more columns win a tie
        int bestCols = 1; double bestH = double.MaxValue, bestCell = MinCellHeight; int bestRows = kids.Count;
        for (int cols = max; cols >= 1; cols--)
        {
            double cellW = (width - Gap * (cols - 1)) / cols;
            if (cellW < MinCellWidth && cols > 1) continue;
            int rows = (int)Math.Ceiling(kids.Count / (double)cols);
            int balanced = (int)Math.Ceiling(kids.Count / (double)rows);           // 9 in 4 columns -> 3 x 3
            cellW = (width - Gap * (balanced - 1)) / balanced;
            double cellH = MinCellHeight;
            foreach (var k in kids) { k.Measure(new Size(Math.Max(1, cellW), double.PositiveInfinity)); cellH = Math.Max(cellH, k.DesiredSize.Height); }
            double total = rows * cellH + Gap * (rows - 1);
            if (total < bestH - 0.5) { bestH = total; bestCols = balanced; bestCell = cellH; bestRows = rows; }
        }
        _cols = bestCols; _rows = bestRows; _cellH = bestCell;
        double finalW = (width - Gap * (_cols - 1)) / _cols;
        foreach (var k in kids) k.Measure(new Size(Math.Max(1, finalW), _cellH));
        return new Size(double.IsInfinity(available.Width) ? width : available.Width, bestH);
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
    private double _scale = 1;
    /// <summary>1 = normal size; below 1 = the content was scaled down to fit.</summary>
    public double Scale => _scale;

    protected override Size MeasureOverride(Size available)
    {
        if (Child is not FrameworkElement child) return new Size();
        // the content's natural height at this width (unscaled)
        SetScale(child, 1);
        child.Measure(new Size(available.Width, double.PositiveInfinity));
        double need = child.DesiredSize.Height;
        double s = !double.IsInfinity(available.Height) && available.Height > 0 && need > available.Height + 0.5 ? available.Height / need : 1;
        SetScale(child, s);
        child.Measure(available);
        var d = child.DesiredSize;
        return new Size(Math.Min(d.Width, available.Width), Math.Min(d.Height, available.Height));
    }

    protected override Size ArrangeOverride(Size final)
    {
        Child?.Arrange(new Rect(final));
        return final;
    }

    // LayoutTransform: the layout system itself gives the content more room and shrinks it to fit
    private void SetScale(FrameworkElement child, double s)
    {
        if (Math.Abs(s - _scale) < 0.001 && (s == 1 ? child.LayoutTransform == Transform.Identity || child.LayoutTransform is null : child.LayoutTransform is ScaleTransform)) return;
        _scale = s;
        child.LayoutTransform = s >= 0.999 ? Transform.Identity : new ScaleTransform(s, s);
    }
}
