using System.Windows.Media;
using NexOrderDesk.Models;
namespace NexOrderDesk.Core;

/// <summary>Colour picker helpers (2.9): colour families for the palette, nearest colour name, HSV, shades.</summary>
public static class ColorFamilies
{
    public sealed record Swatch(string Name, string Hex)
    {
        public SolidColorBrush Brush => ColorMath.Brush(Hex);
    }

    public static readonly string[] Names =
    {
        "Whites & Creams", "Greys & Blacks", "Blues & Navy", "Teals & Turquoise", "Greens", "Yellows & Golds",
        "Oranges & Peach", "Reds & Maroons", "Pinks", "Purples", "Browns & Beige"
    };

    /// <summary>Family of a colour by its hue, saturation and lightness.</summary>
    public static string FamilyOf(string hex)
    {
        var (h, s, l) = Hsl(ColorMath.Parse(hex));
        if (l >= 0.9 || (l >= 0.82 && s < 0.45)) return "Whites & Creams";
        if (s < 0.13 || l < 0.1) return "Greys & Blacks";
        if (h >= 15 && h < 50 && (l < 0.42 || s < 0.42)) return "Browns & Beige";
        if ((h >= 300 || h < 12) && l < 0.33) return h >= 300 && h < 330 ? "Purples" : "Reds & Maroons";   // burgundy, wine, plum
        if (h < 12 || h >= 345)
        {
            if (l >= 0.55 && h >= 5 && h < 12) return "Oranges & Peach";                                    // coral, salmon, tomato
            return l >= 0.62 ? "Pinks" : "Reds & Maroons";
        }
        if (h < 40) return "Oranges & Peach";
        if (h < 66) return "Yellows & Golds";
        if (h < 165) return "Greens";
        if (h < 197) return "Teals & Turquoise";
        if (h < 255) return "Blues & Navy";
        if (h < 300) return "Purples";
        return "Pinks";
    }

    private static List<Swatch>? _library;
    /// <summary>All built-in colours (one per name), ordered light to dark inside each family.</summary>
    public static IReadOnlyList<Swatch> Library => _library ??= ColorNames.AllNames
        .Select(n => new Swatch(n.ToUpperInvariant(), ColorNames.HexOf(n)!))
        .GroupBy(sw => sw.Hex).Select(g => g.OrderByDescending(x => x.Name.Length).First())   // same colour under two names: once, fuller name (NAVY BLUE)
        .OrderByDescending(sw => Hsl(ColorMath.Parse(sw.Hex)).L).ToList();

    public static IEnumerable<Swatch> InFamily(string family) => Library.Where(sw => FamilyOf(sw.Hex) == family);

    /// <summary>The built-in colour name closest to a colour (perceptual "redmean" distance).</summary>
    public static string NearestName(string hex)
    {
        var c = ColorMath.Parse(hex);
        Swatch? best = null; double bestD = double.MaxValue;
        foreach (var sw in Library)
        {
            var d = Distance(c, ColorMath.Parse(sw.Hex));
            if (d < bestD) { bestD = d; best = sw; }
        }
        return best?.Name ?? "CUSTOM";
    }

    public static double Distance(Color a, Color b)
    {
        double rm = (a.R + b.R) / 2.0, dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return Math.Sqrt((2 + rm / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rm) / 256) * db * db);
    }

    /// <summary>Seven shades of a colour, light to dark: PALE, LIGHT, SOFT, the colour, RICH, DARK, DEEP.</summary>
    public static List<Swatch> Shades(string hex, string baseName)
    {
        var c = ColorMath.Parse(hex);
        var name = (baseName ?? "").Trim().ToUpperInvariant();
        foreach (var p in new[] { "LIGHT ", "PALE ", "DARK ", "DEEP ", "SOFT ", "RICH " }) if (name.StartsWith(p)) name = name[p.Length..];
        if (name.Length == 0) name = NearestName(hex);
        var list = new List<Swatch>();
        var steps = new (double Amount, string Prefix)[] { (0.7, "PALE "), (0.45, "LIGHT "), (0.22, "SOFT "), (0, ""), (0.2, "RICH "), (0.38, "DARK "), (0.55, "DEEP ") };
        for (int i = 0; i < steps.Length; i++)
        {
            var col = i < 3 ? ColorMath.Lighten(c, steps[i].Amount) : i == 3 ? c : ColorMath.Darken(c, steps[i].Amount);
            list.Add(new Swatch(steps[i].Prefix + name, ColorMath.ToHex(col)));
        }
        return list;
    }

    // ------------------------------------------------------------------ HSV / HSL
    public static (double H, double S, double L) Hsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2, d = max - min;
        if (d < 1e-9) return (0, 0, l);
        double s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60, s, l);
    }

    public static (double H, double S, double V) ToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = 0;
        if (d > 1e-9) h = (max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4) * 60;
        return (h, max <= 0 ? 0 : d / max, max);
    }

    public static Color FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360; s = Math.Clamp(s, 0, 1); v = Math.Clamp(v, 0, 1);
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        var (r, g, b) = h < 60 ? (c, x, 0d) : h < 120 ? (x, c, 0d) : h < 180 ? (0d, c, x) : h < 240 ? (0d, x, c) : h < 300 ? (x, 0d, c) : (c, 0d, x);
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
