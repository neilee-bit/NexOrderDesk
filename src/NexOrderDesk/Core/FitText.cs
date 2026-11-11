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
        double min = GetMinSize(tb);
        double size = SizeFor(tb.Text, avail, b, min, face, dip);
        var wrap = TextWrapping.NoWrap;
        // very long text: use two lines inside the box (if it is tall enough) before going smaller
        // the box may be only one line tall (centred in its card) - the card's content area is the real limit
        double boxH = tb.ActualHeight;
        if (VisualTreeHelper.GetParent(tb) is FrameworkElement parent && parent.ActualHeight > boxH) boxH = parent.ActualHeight;
        boxH -= 2;
        if (size < b * 0.7 && boxH >= 2 * 1.25 * 8 && !string.IsNullOrEmpty(tb.Text))
        {
            double two = Math.Min(b, Math.Min(SizeFor(tb.Text, avail * 1.8, b, 1, face, dip), boxH / (2 * 1.3)));
            if (two > size)
            {
                // check that the text really fits in two lines at that size; step down if not
                for (double t = Math.Floor(two * 4) / 4; t >= min; t -= 0.25)
                {
                    var ft = new FormattedText(tb.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, t, Brushes.Black, dip) { MaxTextWidth = avail };
                    if (ft.Height <= boxH) { size = t; wrap = TextWrapping.Wrap; break; }
                }
            }
        }
        if (tb.TextWrapping != wrap) tb.TextWrapping = wrap;
        if (Math.Abs(tb.FontSize - size) > 0.01) tb.FontSize = size;
    }
}
