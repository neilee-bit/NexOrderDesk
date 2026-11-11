using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace NexOrderDesk.Core;

/// <summary>
/// h:FitText.Enabled="True" on a TextBox: when the text is too long for the box, the font gets smaller
/// (down to MinSize) so the whole text stays visible - while typing, on the submitted sheet, in print and PDF.
/// </summary>
public static class FitText
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(FitText), new PropertyMetadata(false, OnEnabled));
    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);

    public static readonly DependencyProperty MinSizeProperty = DependencyProperty.RegisterAttached(
        "MinSize", typeof(double), typeof(FitText), new PropertyMetadata(7.0));
    public static double GetMinSize(DependencyObject d) => (double)d.GetValue(MinSizeProperty);
    public static void SetMinSize(DependencyObject d, double v) => d.SetValue(MinSizeProperty, v);

    // the font size the box was designed with
    private static readonly DependencyProperty BaseSizeProperty = DependencyProperty.RegisterAttached(
        "BaseSize", typeof(double), typeof(FitText), new PropertyMetadata(0.0));

    private static void OnEnabled(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb || e.NewValue is not true) return;
        tb.TextChanged += (_, _) => Fit(tb);
        tb.SizeChanged += (_, _) => Fit(tb);
        tb.Loaded += (_, _) => Fit(tb);
    }

    /// <summary>Font size that makes <paramref name="text"/> fit <paramref name="width"/> (never above the design size).</summary>
    public static double SizeFor(string text, double width, double baseSize, double minSize, Typeface face, double pixelsPerDip)
    {
        if (string.IsNullOrEmpty(text) || width <= 0) return baseSize;
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, baseSize, Brushes.Black, pixelsPerDip);
        double need = ft.WidthIncludingTrailingWhitespace;
        if (need <= width) return baseSize;
        return Math.Max(minSize, Math.Floor(baseSize * width / need * 4) / 4);
    }

    public static void Fit(TextBox tb)
    {
        double b = (double)tb.GetValue(BaseSizeProperty);
        if (b <= 0) { b = tb.FontSize; tb.SetValue(BaseSizeProperty, b); }
        double avail = tb.ActualWidth - tb.Padding.Left - tb.Padding.Right - 6;
        if (avail <= 0) return;
        double dip = 1;
        try { dip = VisualTreeHelper.GetDpi(tb).PixelsPerDip; } catch { }
        var face = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch);
        double size = SizeFor(tb.Text, avail, b, GetMinSize(tb), face, dip);
        if (Math.Abs(tb.FontSize - size) > 0.01) tb.FontSize = size;
    }
}
