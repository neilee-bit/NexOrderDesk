using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace NexOrderDesk.Services;

/// <summary>
/// Loads pictures from files and the clipboard. PNG, JPG/JPEG, BMP, GIF, TIF/TIFF, ICO, WEBP (Windows codec),
/// HEIC/AVIF when a Windows codec is installed, and SVG (vector, built-in reader - stays sharp at any size).
/// Files are read into memory so they are never left locked. Huge photos are scaled down to max 3000 px (still print quality).
/// </summary>
public static class ImageLoader
{
    private const int MaxSide = 3000;
    public static readonly string[] Extensions =
        { ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".webp", ".svg", ".heic", ".avif" };

    public const string OpenFilter =
        "Images|*.png;*.jpg;*.jpeg;*.jfif;*.bmp;*.gif;*.tif;*.tiff;*.ico;*.webp;*.svg;*.heic;*.avif|All files|*.*";

    public static bool IsImageFile(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Returns null (and shows why) if the file cannot be read.</summary>
    public static ImageSource? LoadFile(string path, bool showErrors = true)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase)) return LoadSvg(path);
            var bytes = File.ReadAllBytes(path);
            return Decode(bytes) ?? throw new NotSupportedException(Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".heic", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".avif", StringComparison.OrdinalIgnoreCase)
                ? "Windows has no codec for this format. Install \"WebP / HEIF Image Extensions\" from the Microsoft Store, or save the picture as PNG / JPG."
                : "This picture format could not be read.");
        }
        catch (Exception ex)
        {
            if (showErrors) MessageBox.Show($"Could not open {Path.GetFileName(path)}:\n{ex.Message}", "Image", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    /// <summary>Decode raster bytes with Windows imaging (WIC): PNG, JPG, BMP, GIF, TIFF, ICO, and WEBP / HEIC / AVIF
    /// through the Windows codecs (WEBP is built into Windows 10 / 11).</summary>
    public static BitmapSource? Decode(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return Finish(dec.Frames[0]);
        }
        catch { return null; }
    }

    private static BitmapSource Finish(BitmapSource src)
    {
        int max = Math.Max(src.PixelWidth, src.PixelHeight);
        if (max > MaxSide)
        {
            double k = (double)MaxSide / max;
            src = new TransformedBitmap(src, new ScaleTransform(k, k));
        }
        // normalise DPI to 96 so Width/Height are the pixel size (stable aspect ratio and sizing)
        if (Math.Abs(src.DpiX - 96) > 0.5 || Math.Abs(src.DpiY - 96) > 0.5)
        {
            var conv = new FormatConvertedBitmap(src, PixelFormats.Pbgra32, null, 0);
            int stride = conv.PixelWidth * 4;
            var px = new byte[stride * conv.PixelHeight];
            conv.CopyPixels(px, stride, 0);
            src = BitmapSource.Create(conv.PixelWidth, conv.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, px, stride);
        }
        if (src.CanFreeze) src.Freeze();
        return src;
    }

    private static ImageSource LoadSvg(string path) => SvgReader.Load(path);   // built-in, vector

    // ------------------------------------------------------------------ clipboard
    /// <summary>All pictures on the clipboard: copied files, PNG data (keeps transparency), or a plain bitmap.</summary>
    public static List<ImageSource> FromClipboard()
    {
        var list = new List<ImageSource>();
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                foreach (string? f in Clipboard.GetFileDropList())
                    if (f != null && IsImageFile(f) && LoadFile(f) is { } img) list.Add(img);
                if (list.Count > 0) return list;
            }
            if (Clipboard.ContainsData("PNG") && Clipboard.GetData("PNG") is MemoryStream png && Decode(png.ToArray()) is { } p)
            { list.Add(p); return list; }
            if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } bmp) list.Add(Finish(bmp));
        }
        catch { /* clipboard busy or unreadable */ }
        return list;
    }

    public static bool ClipboardHasImage()
    {
        try
        {
            if (Clipboard.ContainsImage() || Clipboard.ContainsData("PNG")) return true;
            return Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Cast<string>().Any(IsImageFile);
        }
        catch { return false; }
    }

    public static string? ClipboardText()
    {
        try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; } catch { return null; }
    }
}
