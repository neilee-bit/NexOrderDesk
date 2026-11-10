using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
namespace NexOrderDesk.Services;

/// <summary>
/// Small built-in SVG reader (no extra libraries): turns an SVG file into a vector WPF drawing, so it stays sharp at any size.
/// Supports path, rect, circle, ellipse, line, polygon, polyline, groups, use-less simple files, transforms
/// (matrix / translate / scale / rotate), fill / stroke / opacity (attributes and style=""), #hex / rgb() / named colours and
/// linear / radial gradients. Unsupported parts (text, filters, masks, images) are skipped instead of failing.
/// </summary>
public static class SvgReader
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static DrawingImage Load(string path)
    {
        // many SVG exports carry a <!DOCTYPE>: ignore it instead of refusing the file
        var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore, XmlResolver = null };
        XDocument doc;
        using (var reader = System.Xml.XmlReader.Create(path, settings)) doc = XDocument.Load(reader);
        var svg = doc.Root ?? throw new NotSupportedException("Empty SVG.");
        var defs = svg.Descendants().Where(e => e.Attribute("id") != null).ToDictionary(e => (string)e.Attribute("id")!, e => e);

        var group = new DrawingGroup();
        var state = new Style(Brushes.Black, null, 1, 1);
        foreach (var child in svg.Elements()) Add(child, group, state, defs);
        if (group.Children.Count == 0) throw new NotSupportedException("This SVG has no shapes that can be shown.");

        // honour the viewBox / width / height so the picture keeps its intended frame
        var vb = Nums((string?)svg.Attribute("viewBox"));
        Rect frame;
        if (vb.Length == 4 && vb[2] > 0 && vb[3] > 0) frame = new Rect(vb[0], vb[1], vb[2], vb[3]);
        else
        {
            double w = Len((string?)svg.Attribute("width")), h = Len((string?)svg.Attribute("height"));
            frame = w > 0 && h > 0 ? new Rect(0, 0, w, h) : group.Bounds;
        }
        var root = new DrawingGroup { ClipGeometry = new RectangleGeometry(frame) };
        // an invisible rectangle fixes the size to the frame
        root.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(frame)));
        root.Children.Add(group);
        var img = new DrawingImage(root);
        img.Freeze();
        return img;
    }

    private sealed record Style(Brush? Fill, Brush? Stroke, double StrokeWidth, double Opacity);

    private static void Add(XElement e, DrawingGroup parent, Style inherited, Dictionary<string, XElement> defs)
    {
        string tag = e.Name.LocalName;
        if (tag is "defs" or "title" or "desc" or "metadata" or "style" or "linearGradient" or "radialGradient" or "clipPath" or "mask"
                or "filter" or "text" or "image" or "symbol" or "pattern") return;
        if (Attr(e, "display") == "none" || Attr(e, "visibility") == "hidden") return;

        var st = Resolve(e, inherited, defs);
        Transform? tr = ParseTransform(Attr(e, "transform"));

        if (tag is "g" or "a" or "switch")
        {
            var g = new DrawingGroup { Transform = tr, Opacity = Num(Attr(e, "opacity"), 1) };
            foreach (var c in e.Elements()) Add(c, g, st with { Opacity = 1 }, defs);
            if (g.Children.Count > 0) parent.Children.Add(g);
            return;
        }
        if (tag == "use")
        {
            var href = (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == "href");
            if (href != null && href.StartsWith('#') && defs.TryGetValue(href[1..], out var target))
            {
                var g = new DrawingGroup();
                var tg = new TransformGroup();
                if (tr != null) tg.Children.Add(tr);
                tg.Children.Add(new TranslateTransform(Num(Attr(e, "x"), 0), Num(Attr(e, "y"), 0)));
                g.Transform = tg;
                if (target.Name.LocalName == "symbol") foreach (var c in target.Elements()) Add(c, g, st, defs);
                else Add(target, g, st, defs);
                if (g.Children.Count > 0) parent.Children.Add(g);
            }
            return;
        }

        Geometry? geo = null;
        try
        {
            switch (tag)
            {
                case "path":
                    var d = Attr(e, "d");
                    if (!string.IsNullOrWhiteSpace(d))
                    {
                        var pg = PathGeometry.CreateFromGeometry(Geometry.Parse(NormalizePath(d)));
                        pg.FillRule = Attr(e, "fill-rule") == "evenodd" ? FillRule.EvenOdd : FillRule.Nonzero;
                        geo = pg;
                    }
                    break;
                case "rect":
                    double rx = Num(Attr(e, "rx"), 0), ry = Num(Attr(e, "ry"), rx);
                    if (Attr(e, "ry") != null && Attr(e, "rx") == null) rx = ry;
                    geo = new RectangleGeometry(new Rect(Num(Attr(e, "x"), 0), Num(Attr(e, "y"), 0), Num(Attr(e, "width"), 0), Num(Attr(e, "height"), 0)), rx, ry);
                    break;
                case "circle":
                    double r = Num(Attr(e, "r"), 0);
                    geo = new EllipseGeometry(new Point(Num(Attr(e, "cx"), 0), Num(Attr(e, "cy"), 0)), r, r);
                    break;
                case "ellipse":
                    geo = new EllipseGeometry(new Point(Num(Attr(e, "cx"), 0), Num(Attr(e, "cy"), 0)), Num(Attr(e, "rx"), 0), Num(Attr(e, "ry"), 0));
                    break;
                case "line":
                    geo = new LineGeometry(new Point(Num(Attr(e, "x1"), 0), Num(Attr(e, "y1"), 0)), new Point(Num(Attr(e, "x2"), 0), Num(Attr(e, "y2"), 0)));
                    break;
                case "polygon":
                case "polyline":
                    var n = Nums(Attr(e, "points"));
                    if (n.Length >= 4)
                    {
                        var fig = new PathFigure { StartPoint = new Point(n[0], n[1]), IsClosed = tag == "polygon" };
                        for (int i = 2; i + 1 < n.Length; i += 2) fig.Segments.Add(new LineSegment(new Point(n[i], n[i + 1]), true));
                        geo = new PathGeometry(new[] { fig });
                    }
                    break;
            }
        }
        catch { geo = null; }          // one bad shape never breaks the whole picture
        if (geo == null) return;

        Pen? pen = st.Stroke != null && st.StrokeWidth > 0 ? new Pen(st.Stroke, st.StrokeWidth) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round } : null;
        Brush? fill = tag is "line" or "polyline" && Attr(e, "fill") == null && !HasStyle(e, "fill") ? null : st.Fill;
        if (fill == null && pen == null) return;
        var gd = new GeometryDrawing(fill, pen, geo);
        double op = Num(Attr(e, "opacity"), 1) * st.Opacity;
        if (tr != null || op < 1)
        {
            var g = new DrawingGroup { Transform = tr, Opacity = op };
            g.Children.Add(gd);
            parent.Children.Add(g);
        }
        else parent.Children.Add(gd);
    }

    // ---------------------------------------------------------------- styles
    private static string? Attr(XElement e, string name)
    {
        var style = (string?)e.Attribute("style");
        if (style != null)
            foreach (var part in style.Split(';'))
            {
                var kv = part.Split(':', 2);
                if (kv.Length == 2 && kv[0].Trim() == name) return kv[1].Trim();
            }
        return (string?)e.Attribute(name);
    }

    private static bool HasStyle(XElement e, string name) =>
        ((string?)e.Attribute("style"))?.Split(';').Any(p => p.Split(':')[0].Trim() == name) == true;

    private static Style Resolve(XElement e, Style inh, Dictionary<string, XElement> defs)
    {
        var fill = inh.Fill; var stroke = inh.Stroke; double sw = inh.StrokeWidth;
        var f = Attr(e, "fill"); if (f != null) fill = Paint(f, defs);
        var s = Attr(e, "stroke"); if (s != null) stroke = Paint(s, defs);
        var w = Attr(e, "stroke-width"); if (w != null) sw = Len(w);
        double fo = Num(Attr(e, "fill-opacity"), 1), so = Num(Attr(e, "stroke-opacity"), 1);
        if (fill != null && fo < 1) { fill = fill.Clone(); fill.Opacity = fo; }
        if (stroke != null && so < 1) { stroke = stroke.Clone(); stroke.Opacity = so; }
        return new Style(fill, stroke, sw, inh.Opacity);
    }

    private static Brush? Paint(string v, Dictionary<string, XElement> defs)
    {
        v = v.Trim();
        if (v == "none" || v == "transparent") return null;
        var m = Regex.Match(v, @"url\(\s*#([^)\s]+)\s*\)");
        if (m.Success) return defs.TryGetValue(m.Groups[1].Value, out var g) ? Gradient(g, defs) : Brushes.Black;
        var c = ParseColor(v);
        return c.HasValue ? new SolidColorBrush(c.Value) : Brushes.Black;
    }

    private static Brush? Gradient(XElement g, Dictionary<string, XElement> defs)
    {
        // stops may live on a referenced gradient (xlink:href)
        var stopsSrc = g;
        var href = (string?)g.Attributes().FirstOrDefault(a => a.Name.LocalName == "href");
        if (!g.Elements().Any(x => x.Name.LocalName == "stop") && href != null && href.StartsWith('#') && defs.TryGetValue(href[1..], out var h)) stopsSrc = h;
        var stops = new GradientStopCollection();
        foreach (var st in stopsSrc.Elements().Where(x => x.Name.LocalName == "stop"))
        {
            var col = ParseColor(Attr(st, "stop-color") ?? "#000") ?? Colors.Black;
            double op = Num(Attr(st, "stop-opacity"), 1);
            col.A = (byte)Math.Round(255 * Math.Clamp(op, 0, 1));
            var off = (Attr(st, "offset") ?? "0").Trim();
            double o = off.EndsWith('%') ? Num(off[..^1], 0) / 100 : Num(off, 0);
            stops.Add(new GradientStop(col, Math.Clamp(o, 0, 1)));
        }
        if (stops.Count == 0) return null;
        if (stops.Count == 1) return new SolidColorBrush(stops[0].Color);
        bool user = (string?)g.Attribute("gradientUnits") == "userSpaceOnUse";
        GradientBrush brush;
        if (g.Name.LocalName == "radialGradient")
        {
            var rb = new RadialGradientBrush(stops) { MappingMode = user ? BrushMappingMode.Absolute : BrushMappingMode.RelativeToBoundingBox };
            double cx = Frac(Attr(g, "cx"), 0.5), cy = Frac(Attr(g, "cy"), 0.5), r = Frac(Attr(g, "r"), 0.5);
            rb.Center = rb.GradientOrigin = new Point(cx, cy); rb.RadiusX = rb.RadiusY = r;
            brush = rb;
        }
        else
        {
            brush = new LinearGradientBrush(stops)
            {
                MappingMode = user ? BrushMappingMode.Absolute : BrushMappingMode.RelativeToBoundingBox,
                StartPoint = new Point(Frac(Attr(g, "x1"), 0), Frac(Attr(g, "y1"), 0)),
                EndPoint = new Point(Frac(Attr(g, "x2"), 1), Frac(Attr(g, "y2"), 0))
            };
        }
        var gt = ParseTransform((string?)g.Attribute("gradientTransform"));
        if (gt != null) brush.Transform = gt;
        return brush;
    }

    private static Color? ParseColor(string v)
    {
        v = v.Trim();
        try
        {
            if (v.StartsWith('#') && v.Length == 4)                       // #rgb
                v = "#" + string.Concat(v.Skip(1).Select(ch => $"{ch}{ch}"));
            var m = Regex.Match(v, @"rgba?\(\s*([\d.]+%?)\s*[, ]\s*([\d.]+%?)\s*[, ]\s*([\d.]+%?)\s*(?:[,/]\s*([\d.]+%?))?\s*\)");
            if (m.Success)
            {
                byte Ch(string s) => (byte)Math.Clamp(Math.Round(s.EndsWith('%') ? Num(s[..^1], 0) * 2.55 : Num(s, 0)), 0, 255);
                double a = m.Groups[4].Success ? (m.Groups[4].Value.EndsWith('%') ? Num(m.Groups[4].Value[..^1], 100) / 100 : Num(m.Groups[4].Value, 1)) : 1;
                return Color.FromArgb((byte)Math.Round(255 * Math.Clamp(a, 0, 1)), Ch(m.Groups[1].Value), Ch(m.Groups[2].Value), Ch(m.Groups[3].Value));
            }
            if (v == "currentColor") return Colors.Black;
            return (Color)ColorConverter.ConvertFromString(v);
        }
        catch { return null; }
    }

    // ---------------------------------------------------------------- transforms / numbers
    private static Transform? ParseTransform(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return null;
        var group = new TransformGroup();
        foreach (Match m in Regex.Matches(t, @"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)"))
        {
            var a = Nums(m.Groups[2].Value);
            switch (m.Groups[1].Value)
            {
                case "matrix" when a.Length >= 6: group.Children.Add(new MatrixTransform(a[0], a[1], a[2], a[3], a[4], a[5])); break;
                case "translate" when a.Length >= 1: group.Children.Add(new TranslateTransform(a[0], a.Length > 1 ? a[1] : 0)); break;
                case "scale" when a.Length >= 1: group.Children.Add(new ScaleTransform(a[0], a.Length > 1 ? a[1] : a[0])); break;
                case "rotate" when a.Length >= 1:
                    group.Children.Add(a.Length >= 3 ? new RotateTransform(a[0], a[1], a[2]) : new RotateTransform(a[0])); break;
                case "skewX" when a.Length >= 1: group.Children.Add(new SkewTransform(a[0], 0)); break;
                case "skewY" when a.Length >= 1: group.Children.Add(new SkewTransform(0, a[0])); break;
            }
        }
        if (group.Children.Count == 0) return null;
        // SVG lists transforms outer-first; WPF TransformGroup applies the first child first -> reverse
        var rev = new TransformGroup();
        for (int i = group.Children.Count - 1; i >= 0; i--) rev.Children.Add(group.Children[i]);
        return rev;
    }

    private static readonly Regex NumRx = new(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

    private static double[] Nums(string? s) =>
        string.IsNullOrWhiteSpace(s) ? Array.Empty<double>() : NumRx.Matches(s).Select(m => double.Parse(m.Value, Inv)).ToArray();

    private static double Num(string? s, double fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        var m = NumRx.Match(s);
        return m.Success ? double.Parse(m.Value, Inv) : fallback;
    }

    /// <summary>Length with optional unit (px default; pt, mm, cm, in converted to px).</summary>
    private static double Len(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        double v = Num(s, 0);
        s = s.Trim().ToLowerInvariant();
        if (s.EndsWith("pt")) return v * 96 / 72;
        if (s.EndsWith("mm")) return v * 96 / 25.4;
        if (s.EndsWith("cm")) return v * 96 / 2.54;
        if (s.EndsWith("in")) return v * 96;
        if (s.EndsWith('%')) return 0;
        return v;
    }

    private static double Frac(string? s, double fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        s = s.Trim();
        return s.EndsWith('%') ? Num(s[..^1], fallback * 100) / 100 : Num(s, fallback);
    }

    /// <summary>
    /// WPF's path parser is close to SVG's. Help it with the compact SVG forms it rejects: arc flags written without
    /// separators ("a1 1 0 01 2 2") and numbers glued together ("1.5.5" = 1.5 0.5).
    /// </summary>
    private static string NormalizePath(string d)
    {
        // split "1.5.5" -> "1.5 .5"
        d = Regex.Replace(d, @"(\.\d+)(?=\.)", "$1 ");
        // arc commands: make sure the two flags are separate tokens
        d = Regex.Replace(d, @"([aA])([^a-zA-Z]*)", m =>
        {
            var nums = new List<string>();
            var body = m.Groups[2].Value;
            int i = 0, k = 0;
            while (i < body.Length)
            {
                if (char.IsWhiteSpace(body[i]) || body[i] == ',') { i++; continue; }
                int pos = k % 7;
                if (pos == 3 || pos == 4) { nums.Add(body[i].ToString()); i++; k++; continue; }   // single-digit flag
                var nm = NumRx.Match(body, i);
                if (!nm.Success || nm.Index != i) { i++; continue; }
                nums.Add(nm.Value); i += nm.Length; k++;
            }
            return m.Groups[1].Value + " " + string.Join(" ", nums) + " ";
        });
        return d;
    }
}
