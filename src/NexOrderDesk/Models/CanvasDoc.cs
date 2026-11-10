using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using NexOrderDesk.Core;
namespace NexOrderDesk.Models;

public enum CanvasItemKind { Text, Image }

/// <summary>One movable object on a free-form workspace: a text box or a picture. Position / size in sheet units.</summary>
public class CanvasItem : ObservableObject
{
    private double _x, _y, _w, _h;
    private string _text = "";
    private double _fontSize = 13;
    private bool _bold;
    private string _color = "#1E1E1E";
    private double _rotation;
    private Rect _crop = FullCrop;

    /// <summary>The whole picture (no crop).</summary>
    public static readonly Rect FullCrop = new(0, 0, 1, 1);

    private CanvasItem(CanvasItemKind kind) { Kind = kind; }

    public static CanvasItem NewText(string text, double x, double y, double width = 220, double fontSize = 13) =>
        new(CanvasItemKind.Text) { _text = text ?? "", _x = x, _y = y, _w = width, _h = 0, _fontSize = fontSize };

    public static CanvasItem NewImage(ImageSource src, double x, double y, double w, double h, Rect? crop = null) =>
        new(CanvasItemKind.Image) { Image = src, _x = x, _y = y, _w = w, _h = h, _crop = Normalize(crop ?? FullCrop) };

    public CanvasItemKind Kind { get; }
    public bool IsText => Kind == CanvasItemKind.Text;
    public ImageSource? Image { get; private init; }
    /// <summary>Height / width of the visible (cropped) picture - kept when resizing so pictures never distort.</summary>
    public double AspectRatio => Image == null || Image.Width <= 0 || _crop.Width <= 0 ? 1 : (Image.Height * _crop.Height) / (Image.Width * _crop.Width);

    /// <summary>
    /// Visible part of the ORIGINAL picture, as fractions 0..1 (x, y, width, height). The original is never changed, so the
    /// crop can be edited again or reset at any time.
    /// </summary>
    public Rect Crop
    {
        get => _crop;
        set
        {
            if (!Set(ref _crop, Normalize(value))) return;
            OnPropertyChanged(nameof(AspectRatio)); OnPropertyChanged(nameof(IsCropped));
        }
    }
    public bool IsCropped => !IsFull(_crop);
    public static bool IsFull(Rect r) => r.X <= 0.0005 && r.Y <= 0.0005 && r.Width >= 0.999 && r.Height >= 0.999;

    private static Rect Normalize(Rect r)
    {
        double x = Math.Clamp(r.X, 0, 0.99), y = Math.Clamp(r.Y, 0, 0.99);
        double w = Math.Clamp(r.Width, 0.01, 1 - x), h = Math.Clamp(r.Height, 0.01, 1 - y);
        return new Rect(x, y, w, h);
    }

    public double X { get => _x; set => Set(ref _x, value); }
    public double Y { get => _y; set => Set(ref _y, value); }
    public double W { get => _w; set => Set(ref _w, value); }
    /// <summary>Pictures: height. Text boxes: 0 = grows with the text.</summary>
    public double H { get => _h; set => Set(ref _h, value); }
    public string Text { get => _text; set => Set(ref _text, value ?? ""); }
    public double FontSize { get => _fontSize; set => Set(ref _fontSize, Math.Clamp(value, 6, 72)); }
    public bool IsBold { get => _bold; set => Set(ref _bold, value); }
    /// <summary>Text colour (#RRGGBB).</summary>
    public string ColorHex { get => _color; set => Set(ref _color, value ?? "#1E1E1E"); }
    /// <summary>Rotation in degrees around the centre (pictures and text boxes).</summary>
    public double Rotation
    {
        get => _rotation;
        set { var r = value % 360; if (r < 0) r += 360; Set(ref _rotation, Math.Round(r, 1)); }
    }
    public bool HasContent => Kind == CanvasItemKind.Image || !string.IsNullOrWhiteSpace(_text);
}

/// <summary>A free-form editable workspace (back side, DTF sheet, flag artwork): any number of text boxes and pictures.</summary>
public class CanvasDoc
{
    public ObservableCollection<CanvasItem> Items { get; } = new();
    public bool HasContent => Items.Any(i => i.HasContent);
    public void Clear() => Items.Clear();
}
