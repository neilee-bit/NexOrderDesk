using System.Windows;
using System.Windows.Media;
using NexOrderDesk.Models;
namespace NexOrderDesk.Services;

/// <summary>
/// Builds the active colour theme from ONE accent colour, so any order type (or any single order) can have any colour.
/// The generated dictionary replaces the previous theme dictionary in Application resources; everything that uses
/// {DynamicResource AccentBrush} etc. updates instantly.
/// </summary>
public static class ThemeService
{
    private const string Marker = "__NexTheme";
    private static string? _current;

    public static void Apply(string accentHex)
    {
        var hex = ColorMath.NormalizeHex(accentHex) ?? "#FFC914";
        if (hex == _current) return;
        _current = hex;

        var a = ColorMath.Parse(hex);
        var light = ColorMath.Lighten(a, 0.45);
        var dark = ColorMath.Darken(a, 0.16);
        var soft = ColorMath.Lighten(a, 0.84);
        var tint = ColorMath.Lighten(a, 0.5);

        var d = new ResourceDictionary { [Marker] = hex };
        d["AccentBrush"] = ColorMath.Brush(a);
        d["AccentLightBrush"] = ColorMath.Brush(light);
        d["AccentDarkBrush"] = ColorMath.Brush(dark);
        d["AccentSoftBrush"] = ColorMath.Brush(soft);
        d["AccentTintBrush"] = ColorMath.Brush(tint);
        d["BannerBrush"] = Gradient(new Point(0, 0), new Point(1, 0), (light, 0), (a, 0.12), (a, 0.8), (dark, 1));
        // text on coloured backgrounds: dark on light themes, white on dark themes (order number, totals, print method …)
        d["OnAccentBrush"] = ColorMath.Brush(ColorMath.TextOn(a));
        d["OnAccentTintBrush"] = ColorMath.Brush(ColorMath.TextOn(tint));
        d["OnAccentLightBrush"] = ColorMath.Brush(ColorMath.TextOn(light));
        d["OnBannerBrush"] = ColorMath.Brush(ColorMath.TextOn(ColorMath.Mix(light, a, 0.6)));
        d["SheetBrush"] = Gradient(new Point(0, 0), new Point(0, 1), (Colors.White, 0), (Colors.White, 0.7), (soft, 1));

        var dicts = Application.Current.Resources.MergedDictionaries;
        var existing = dicts.FirstOrDefault(x => x.Contains(Marker))
                       ?? dicts.FirstOrDefault(x => x.Source != null && x.Source.OriginalString.Contains("Themes/Theme."));
        if (existing != null) dicts[dicts.IndexOf(existing)] = d; else dicts.Insert(0, d);
    }

    private static LinearGradientBrush Gradient(Point start, Point end, params (Color c, double o)[] stops)
    {
        var g = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        foreach (var (c, o) in stops) g.GradientStops.Add(new GradientStop(c, o));
        g.Freeze();
        return g;
    }
}
