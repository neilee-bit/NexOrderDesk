using System.Globalization;
using System.IO;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NexOrderDesk.Core;
using NexOrderDesk.ViewModels;
using NexOrderDesk.Views.Sections;
namespace NexOrderDesk.Services;

/// <summary>How sheets are placed on paper.</summary>
public enum PrintLayout
{
    /// <summary>A. One sheet, as large as possible, on a full A4 page (landscape).</summary>
    Single,
    /// <summary>B. Two copies of the same sheet on one A4 page (portrait): top half + bottom half, with a cut line.</summary>
    TwoUp,
    /// <summary>C. Like B, printed on both sides: front sheet x2 on side 1, back side x2 on side 2 (long-edge flip, aligned).</summary>
    Duplex,
    /// <summary>A5 landscape, the sheet fills the page.</summary>
    A5,
    /// <summary>A4 portrait, one sheet at the top of the page.</summary>
    Half
}

/// <summary>One sheet image placed on a page (units: 1/96 inch, origin top-left).</summary>
public readonly record struct Placement(BitmapSource Image, Rect Box);

/// <summary>One physical page.</summary>
public sealed class PageSpec
{
    public PageSpec(double w, double h) { Width = w; Height = h; }
    public double Width { get; }
    public double Height { get; }
    public List<Placement> Items { get; } = new();
    /// <summary>Y of the dashed cut line (two copies per page), or null.</summary>
    public double? CutLineY { get; set; }
}

/// <summary>
/// Output of an order sheet. Each side is rendered off-screen in its locked "output" look (no arrows, outlines, toolbars or
/// action bar). The page layout (single / two copies / double-sided / A5 / half A4) is computed once as PageSpecs and the same
/// specs feed the preview, the printer and the PDF writer, so what you preview is exactly what prints.
/// </summary>
public static class SheetOutput
{
    public const double SheetW = 840, SheetH = 592;           // design canvas of the sheet (A5 landscape)
    public const double SheetA4H = 1188;                       // back side on A4 portrait: 840 x 1188 (same scale)
    private const double A4S = 793.7, A4L = 1122.5;            // A4 short / long side, 1/96 inch
    private const double A5S = 559.4, A5L = 793.7;             // A5 short / long side
    private const double Margin = 18.9;                        // 5 mm
    public const string AutoFolderName = "NexOrderDesk Orders";

    public static PrintLayout ParseLayout(string? s) => Enum.TryParse<PrintLayout>(s, true, out var l) ? l : PrintLayout.Single;

    public static string LayoutTitle(PrintLayout l) => l switch
    {
        PrintLayout.TwoUp => "Two copies on one A4",
        PrintLayout.Duplex => "Two copies, double-sided",
        PrintLayout.A5 => "A5 landscape",
        PrintLayout.Half => "Half A4 (sheet at the top)",
        _ => "Single sheet - full A4"
    };

    // ---------------------------------------------------------------- render one side
    /// <summary>Front (and back side if it has content) as bitmaps.</summary>
    public static (BitmapSource Front, BitmapSource? Back) RenderSides(OrderViewModel o, int dpi)
    {
        var front = o.RenderAs(false, () => RenderSide(o, dpi));
        var back = o.Back.HasContent ? o.RenderAs(true, () => RenderSide(o, dpi)) : null;
        return (front, back);
    }

    private static BitmapSource RenderSide(OrderViewModel o, int dpi)
    {
        double sh = o.SheetHeight;                                  // 592, or 1188 for an A4 back side
        var sheet = new OrderSheetView { DataContext = o };
        OutputMode.SetIsPrint(sheet, true);
        var root = new Border
        {
            Width = SheetW, Height = sh, Child = sheet,
            Background = Application.Current.TryFindResource("SheetBrush") as Brush ?? Brushes.White
        };
        root.Measure(new Size(SheetW, sh));
        root.Arrange(new Rect(0, 0, SheetW, sh));
        root.UpdateLayout();
        root.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);   // let item containers / bindings settle
        root.Measure(new Size(SheetW, sh));
        root.Arrange(new Rect(0, 0, SheetW, sh));
        root.UpdateLayout();

        double k = dpi / 96.0;
        var rtb = new RenderTargetBitmap((int)Math.Round(SheetW * k), (int)Math.Round(sh * k), dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(root);
        rtb.Freeze();
        return rtb;
    }

    // ---------------------------------------------------------------- page layout
    private static Rect Fit(double x, double y, double w, double h, double ratio = SheetH / SheetW)
    {
        // largest rectangle of the given height/width ratio, centred in (x, y, w, h)
        double sw = w, sh = w * ratio;
        if (sh > h) { sh = h; sw = h / ratio; }
        return new Rect(x + (w - sw) / 2, y + (h - sh) / 2, sw, sh);
    }

    private static bool IsPortrait(BitmapSource b) => b.PixelHeight > b.PixelWidth;

    /// <summary>A4 back side: always its own full A4 portrait page.</summary>
    private static PageSpec A4BackPage(BitmapSource back)
    {
        var p = new PageSpec(A4S, A4L);
        p.Items.Add(new Placement(back, Fit(Margin, Margin, A4S - 2 * Margin, A4L - 2 * Margin, (double)back.PixelHeight / back.PixelWidth)));
        return p;
    }

    public static List<PageSpec> Compose(PrintLayout layout, BitmapSource front, BitmapSource? back, bool includeBack)
    {
        // A back side set to A4 does not share the A5 layouts: it prints on its own A4 portrait page in every layout
        if (includeBack && back != null && IsPortrait(back))
        {
            var pagesA4 = Compose(layout, front, null, false);
            if (layout == PrintLayout.Duplex && pagesA4.Count == 2) pagesA4.RemoveAt(1);   // side 2 = the A4 back side
            pagesA4.Add(A4BackPage(back));
            return pagesA4;
        }
        var sides = new List<BitmapSource> { front };
        if (includeBack && back != null) sides.Add(back);
        var pages = new List<PageSpec>();

        switch (layout)
        {
            case PrintLayout.Single:                               // A4 landscape, sheet fills the page
                foreach (var s in sides)
                {
                    var p = new PageSpec(A4L, A4S);
                    p.Items.Add(new Placement(s, Fit(Margin, Margin, A4L - 2 * Margin, A4S - 2 * Margin)));
                    pages.Add(p);
                }
                break;

            case PrintLayout.TwoUp:
            case PrintLayout.Duplex:
            {
                var list = new List<BitmapSource>(sides);
                // double-sided always has a second side: the back side, or the front again when there is no back side
                if (layout == PrintLayout.Duplex && list.Count == 1) list.Add(front);
                double half = A4L / 2;
                foreach (var s in list)
                {
                    var p = new PageSpec(A4S, A4L) { CutLineY = half };
                    // identical positions on both sides of the paper -> front and back line up on long-edge flip
                    p.Items.Add(new Placement(s, Fit(Margin, Margin, A4S - 2 * Margin, half - 1.5 * Margin)));
                    p.Items.Add(new Placement(s, Fit(Margin, half + Margin / 2, A4S - 2 * Margin, half - 1.5 * Margin)));
                    pages.Add(p);
                }
                break;
            }

            case PrintLayout.A5:
                foreach (var s in sides)
                {
                    var p = new PageSpec(A5L, A5S);
                    p.Items.Add(new Placement(s, Fit(A5L * 0.02, A5S * 0.02, A5L * 0.96, A5S * 0.96)));
                    pages.Add(p);
                }
                break;

            case PrintLayout.Half:
                foreach (var s in sides)
                {
                    var p = new PageSpec(A4S, A4L);
                    double w = A4S - 2 * Margin;
                    p.Items.Add(new Placement(s, new Rect(Margin, Margin, w, w * SheetH / SheetW)));
                    pages.Add(p);
                }
                break;
        }
        return pages;
    }

    /// <summary>A page as a WPF visual (used by the preview and the printer).</summary>
    public static FrameworkElement BuildPageVisual(PageSpec page)
    {
        var c = new Canvas { Width = page.Width, Height = page.Height, Background = Brushes.White, ClipToBounds = true };
        foreach (var it in page.Items)
        {
            var img = new Image { Source = it.Image, Width = it.Box.Width, Height = it.Box.Height, Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            Canvas.SetLeft(img, it.Box.X); Canvas.SetTop(img, it.Box.Y);
            c.Children.Add(img);
        }
        if (page.CutLineY is double y)
        {
            c.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = Margin / 2, X2 = page.Width - Margin / 2, Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), StrokeThickness = 0.6, StrokeDashArray = new DoubleCollection { 6, 5 }
            });
        }
        c.Measure(new Size(page.Width, page.Height));
        c.Arrange(new Rect(0, 0, page.Width, page.Height));
        return c;
    }

    // ---------------------------------------------------------------- print
    public static void Print(OrderViewModel o, PrintLayout layout, bool includeBack)
    {
        try
        {
            var pd = new PrintDialog();
            ApplyTicket(pd, layout);
            if (pd.ShowDialog() != true) return;
            ApplyTicket(pd, layout);

            Mouse.OverrideCursor = Cursors.Wait;
            var (front, back) = RenderSides(o, 300);
            var pages = Compose(layout, front, back, includeBack);
            Mouse.OverrideCursor = null;
            pd.PrintDocument(new SpecPaginator(pages), $"Order #{o.OrderNumber}");
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            MessageBox.Show("Could not print: " + ex.Message, "Print");
        }
    }

    private static void ApplyTicket(PrintDialog pd, PrintLayout layout)
    {
        try
        {
            switch (layout)
            {
                case PrintLayout.A5:
                    pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA5);
                    pd.PrintTicket.PageOrientation = PageOrientation.Landscape;
                    break;
                case PrintLayout.Single:
                    pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                    pd.PrintTicket.PageOrientation = PageOrientation.Landscape;
                    break;
                default:
                    pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                    pd.PrintTicket.PageOrientation = PageOrientation.Portrait;
                    break;
            }
            // double-sided: flip on the long edge so the top copy stays on top on both sides
            pd.PrintTicket.Duplexing = layout == PrintLayout.Duplex ? Duplexing.TwoSidedLongEdge : Duplexing.OneSided;
        }
        catch { /* printer without that paper / duplex unit: pages are still laid out correctly */ }
    }

    private sealed class SpecPaginator : DocumentPaginator
    {
        private readonly List<PageSpec> _pages;
        public SpecPaginator(List<PageSpec> pages) { _pages = pages; PageSize = new Size(pages[0].Width, pages[0].Height); }
        public override bool IsPageCountValid => true;
        public override int PageCount => _pages.Count;
        public override Size PageSize { get; set; }
        public override IDocumentPaginatorSource? Source => null;
        public override DocumentPage GetPage(int pageNumber)
        {
            var p = _pages[pageNumber];
            var v = BuildPageVisual(p);
            var r = new Rect(0, 0, p.Width, p.Height);
            return new DocumentPage(v, r.Size, r, r);
        }
    }

    // ---------------------------------------------------------------- PDF
    public static string SafeName(OrderViewModel o)
    {
        string raw = string.IsNullOrWhiteSpace(o.OrderNumber) ? "order" : o.OrderNumber.Trim();
        if (!string.IsNullOrWhiteSpace(o.OrderName)) raw += "_" + o.OrderName.Trim();
        var safe = string.Concat(raw.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return safe.Length > 80 ? safe[..80] : safe;
    }

    public static string AutoFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AutoFolderName);

    /// <summary>Called by Submit: writes Documents\NexOrderDesk Orders\Order_&lt;no&gt;_&lt;name&gt;.pdf (overwrites the same order). No dialogs.</summary>
    public static string? AutoSavePdf(OrderViewModel o)
    {
        try
        {
            Directory.CreateDirectory(AutoFolder);
            // one file per order number: re-submitting replaces it instead of making duplicates
            string prefix = "Order_" + string.Concat((string.IsNullOrWhiteSpace(o.OrderNumber) ? "order" : o.OrderNumber.Trim())
                .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)) + "_";
            foreach (var old in Directory.GetFiles(AutoFolder, prefix + "*.pdf")) { try { File.Delete(old); } catch { } }
            var path = Path.Combine(AutoFolder, $"Order_{SafeName(o)}.pdf");
            Mouse.OverrideCursor = Cursors.Wait;
            var (front, back) = RenderSides(o, 250);
            var layout = ParseLayout(SettingsService.Instance.PrintLayout);
            WritePdf(path, Compose(layout, front, back, true));
            return path;
        }
        catch { return null; }
        finally { Mouse.OverrideCursor = null; }
    }

    public static void OpenFolder(string? file)
    {
        try
        {
            if (file != null && File.Exists(file))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file}\"");
            else
            {
                Directory.CreateDirectory(AutoFolder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AutoFolder) { UseShellExecute = true });
            }
        }
        catch { }
    }

    /// <summary>Minimal PDF 1.4 writer. Each distinct sheet image is stored once (JPEG) and drawn wherever the page places it.</summary>
    public static void WritePdf(string path, List<PageSpec> pages)
    {
        var inv = CultureInfo.InvariantCulture;
        const double k = 0.75;                                       // 1/96 inch -> PDF points
        string F(double v) => v.ToString("0.##", inv);

        var images = pages.SelectMany(p => p.Items.Select(i => i.Image)).Distinct().ToList();
        var imgId = new Dictionary<BitmapSource, int>();
        // object ids: 1 catalog, 2 pages, then per page (page, content), then images
        int next = 3;
        var pageIds = new List<(int page, int content)>();
        foreach (var _ in pages) { pageIds.Add((next, next + 1)); next += 2; }
        foreach (var img in images) imgId[img] = next++;
        int total = next - 1;

        using var fs = File.Create(path);
        var offsets = new Dictionary<int, long>();
        void Raw(string s) { var b = Encoding.ASCII.GetBytes(s); fs.Write(b, 0, b.Length); }
        void Begin(int id) { offsets[id] = fs.Position; Raw($"{id} 0 obj\n"); }

        Raw("%PDF-1.4\n"); fs.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }, 0, 6);
        Begin(1); Raw("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Begin(2); Raw($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(p => $"{p.page} 0 R"))}] /Count {pages.Count} >>\nendobj\n");

        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            double pw = p.Width * k, ph = p.Height * k;
            var used = p.Items.Select(it => it.Image).Distinct().ToList();
            var xobj = string.Join(" ", used.Select(im => $"/Im{imgId[im]} {imgId[im]} 0 R"));
            Begin(pageIds[i].page);
            Raw($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(pw)} {F(ph)}] " +
                $"/Resources << /XObject << {xobj} >> >> /Contents {pageIds[i].content} 0 R >>\nendobj\n");

            var sb = new StringBuilder();
            foreach (var it in p.Items)
            {
                double w = it.Box.Width * k, h = it.Box.Height * k, x = it.Box.X * k, y = ph - (it.Box.Y + it.Box.Height) * k;
                sb.Append($"q {F(w)} 0 0 {F(h)} {F(x)} {F(y)} cm /Im{imgId[it.Image]} Do Q\n");
            }
            if (p.CutLineY is double cy)
            {
                double y = ph - cy * k;
                sb.Append($"q 0.69 G 0.45 w [4.5 3.75] 0 d {F(Margin / 2 * k)} {F(y)} m {F(pw - Margin / 2 * k)} {F(y)} l S Q\n");
            }
            var content = sb.ToString();
            Begin(pageIds[i].content); Raw($"<< /Length {content.Length} >>\nstream\n{content}endstream\nendobj\n");
        }

        foreach (var img in images)
        {
            var enc = new JpegBitmapEncoder { QualityLevel = 92 };
            enc.Frames.Add(BitmapFrame.Create(img));
            byte[] jpg;
            using (var ms = new MemoryStream()) { enc.Save(ms); jpg = ms.ToArray(); }
            Begin(imgId[img]);
            Raw($"<< /Type /XObject /Subtype /Image /Width {img.PixelWidth} /Height {img.PixelHeight} " +
                $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpg.Length} >>\nstream\n");
            fs.Write(jpg, 0, jpg.Length);
            Raw("\nendstream\nendobj\n");
        }

        long xref = fs.Position;
        Raw($"xref\n0 {total + 1}\n0000000000 65535 f \n");
        for (int id = 1; id <= total; id++) Raw(offsets[id].ToString("D10", inv) + " 00000 n \n");
        Raw($"trailer\n<< /Size {total + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }
}
