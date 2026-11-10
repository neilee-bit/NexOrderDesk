using System.Windows.Media;
using NexOrderDesk.Core;
namespace NexOrderDesk.Models;

/// <summary>Which sheet layout an order type uses.</summary>
public enum SheetLayout
{
    /// <summary>T-shirt / jacket style: neck, collar, sleeve, placket, buttons, piping + Adults S/S, L/S, Kids sizes.</summary>
    Tops,
    /// <summary>Bottoms / shorts: bottom type, waist, pockets, leg opening, additional custom + XS-4XL size strip.</summary>
    Bottoms,
    /// <summary>Flags: artwork workspace, flag sizes (ft / in, single / double sided, qty), flag materials at the bottom.</summary>
    Flags,
    /// <summary>DTF: the 5 order fields + one big free workspace for artwork (text + pictures).</summary>
    Dtf,
    /// <summary>Jackets: jacket type, collar, sleeve &amp; cuff, closure, pockets, additional customisation; Adults sizes (+ Kids / PS optional).</summary>
    Jacket
}

/// <summary>An order type shown in the "NEW ORDER - choose type" menu. Users can add their own and change every colour.</summary>
public class OrderTypeDef : ObservableObject
{
    private string _name, _sheetTitle, _accentHex = "#FFC914", _iconKey;
    private SheetLayout _layout;
    private Brush _brush = Brushes.Gold;

    public OrderTypeDef(string id, string name, string sheetTitle, string accentHex, SheetLayout layout, string iconKey)
    {
        Id = id; _name = name; _sheetTitle = sheetTitle; _layout = layout; _iconKey = OrderIcons.Normalize(iconKey);
        _accentHex = ColorMath.NormalizeHex(accentHex) ?? "#FFC914";
        _brush = ColorMath.Brush(_accentHex);
    }

    public string Id { get; }
    public string Name { get => _name; set => Set(ref _name, value ?? ""); }
    public string SheetTitle { get => _sheetTitle; set => Set(ref _sheetTitle, value ?? ""); }
    public SheetLayout Layout { get => _layout; set { if (Set(ref _layout, value)) OnPropertyChanged(nameof(LayoutText)); } }
    public string LayoutText => _layout switch
    {
        SheetLayout.Bottoms => "Bottoms layout",
        SheetLayout.Flags => "Flags layout",
        SheetLayout.Dtf => "DTF workspace",
        SheetLayout.Jacket => "Jacket layout",
        _ => "Tops layout"
    };
    public string IconKey
    {
        get => _iconKey;
        set { if (Set(ref _iconKey, OrderIcons.Normalize(value))) OnPropertyChanged(nameof(Icon)); }
    }
    public Geometry Icon => OrderIcons.Get(_iconKey);
    public string AccentHex
    {
        get => _accentHex;
        set
        {
            var hex = ColorMath.NormalizeHex(value) ?? _accentHex;
            if (!Set(ref _accentHex, hex)) return;
            _brush = ColorMath.Brush(hex);
            OnPropertyChanged(nameof(Dot));
        }
    }
    public Brush Dot => _brush;

    // ---- uploaded icon (optional): replaces the built-in icon everywhere (banner, menu, tabs) ----
    private string? _iconFile;
    private System.Windows.Media.ImageSource? _iconImage;
    /// <summary>Full path of the uploaded icon (stored in %LocalAppData%\NexOrderDesk\icons), or null for the built-in icon.</summary>
    public string? IconFile
    {
        get => _iconFile;
        set
        {
            if (!Set(ref _iconFile, string.IsNullOrWhiteSpace(value) ? null : value)) return;
            _iconImage = null; _iconLoaded = false;
            OnPropertyChanged(nameof(IconImage)); OnPropertyChanged(nameof(HasIconImage)); OnPropertyChanged(nameof(NoIconImage));
        }
    }
    private bool _iconLoaded;
    public System.Windows.Media.ImageSource? IconImage
    {
        get
        {
            if (!_iconLoaded)
            {
                _iconLoaded = true;
                _iconImage = _iconFile != null && System.IO.File.Exists(_iconFile) ? Services.ImageLoader.LoadFile(_iconFile, false) : null;
            }
            return _iconImage;
        }
    }
    public bool HasIconImage => IconImage != null;
    public bool NoIconImage => IconImage == null;
}

/// <summary>Icons available for order types (drawn in a 40 x 40 box).</summary>
public static class OrderIcons
{
    public static readonly string[] Keys = { "tshirt", "jacket", "cut", "press", "bottoms", "shorts", "flag", "dtf" };

    public static string Normalize(string? key) => Keys.Contains(key ?? "") ? key! : "tshirt";

    public static string Data(string key) => key switch
    {
        "jacket" => "M7,2 L15,2 L20,7 L25,2 L33,2 L39,10 L37,31 L31,31 L31,14 L21,16 L21,31 L19,31 L19,16 L9,14 L9,31 L3,31 L1,10 Z",
        "cut" => "M10,4 H30 V12 H10 Z M3,14 H37 V30 H3 Z M10,24 H30 V36 H10 Z",
        "press" => "M2,28 L38,28 L38,22 Q36,10 22,10 L12,10 Q8,10 7,16 Z",
        "bottoms" => "M9,2 H31 L34,38 H23 L20,16 L17,38 H6 Z",
        "shorts" => "M7,6 H33 L37,30 H24 L20,18 L16,30 H3 Z",
        "flag" => "M5,2 H8 V38 H5 Z M8,4 H35 L29,12.5 L35,21 H8 Z",
        "dtf" => "M7,2 H27 L34,9 V38 H7 Z M12,15 H29 V19 H22.5 V32 H18.5 V19 H12 Z",
        _ => "M8,2 L14,2 Q16,5.5 20,5.5 Q24,5.5 26,2 L32,2 L39,9 L33.5,14 L30.5,11.5 L30.5,31 L9.5,31 L9.5,11.5 L6.5,14 L1,9 Z"
    };

    private static readonly Dictionary<string, Geometry> Cache = new();
    public static Geometry Get(string? key)
    {
        key = Normalize(key);
        if (!Cache.TryGetValue(key, out var g)) { g = Geometry.Parse(Data(key)); g.Freeze(); Cache[key] = g; }
        return g;
    }
}

/// <summary>Small colour helpers used by the theme generator and the colour pickers.</summary>
public static class ColorMath
{
    /// <summary>Quick-pick swatches shown in every colour picker.</summary>
    public static readonly string[] Presets =
    {
        "#FFC914", "#FF9A1F", "#F77F00", "#E63946", "#D62828", "#C2185B", "#F28CB1", "#A678E8",
        "#6A4C93", "#3B8BEB", "#2457C5", "#1B2A4A", "#00A6A6", "#43B649", "#2E9E4F", "#8D939B"
    };

    public static string? NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        hex = hex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        if (hex.Length != 7) return null;
        foreach (var ch in hex[1..]) if (!Uri.IsHexDigit(ch)) return null;
        return hex.ToUpperInvariant();
    }

    public static Color Parse(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(NormalizeHex(hex) ?? "#808080"); }
        catch { return Colors.Gray; }
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Mix <paramref name="a"/> towards <paramref name="b"/> by t (0 = a, 1 = b).</summary>
    public static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

    public static Color Lighten(Color c, double t) => Mix(c, Colors.White, t);
    public static Color Darken(Color c, double t) => Mix(c, Colors.Black, t);

    public static SolidColorBrush Brush(string hex) => Brush(Parse(hex));
    public static SolidColorBrush Brush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
