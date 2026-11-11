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
    public static readonly string[] Keys =
    {
        "tshirt", "jacket", "cut", "press", "bottoms", "shorts", "flag", "dtf",
        // 2.9: more apparel
        "polo", "longsleeve", "shirt", "hoodie", "sweater", "tanktop", "vest", "jersey", "frock", "skirt",
        "trousers", "romper", "cap", "hat", "socks", "apron", "bag", "scarf"
    };

    /// <summary>Names shown as tool tips in the icon picker.</summary>
    public static string Title(string key) => key switch
    {
        "tshirt" => "T-shirt", "jacket" => "Jacket", "cut" => "Cut and print", "press" => "Print and press", "bottoms" => "Bottoms / pants",
        "shorts" => "Shorts", "flag" => "Flag", "dtf" => "DTF", "polo" => "Polo shirt", "longsleeve" => "Long sleeve", "shirt" => "Shirt",
        "hoodie" => "Hoodie", "sweater" => "Sweater", "tanktop" => "Tank top", "vest" => "Vest / waistcoat", "jersey" => "Sports jersey",
        "frock" => "Frock / dress", "skirt" => "Skirt", "trousers" => "Trousers", "romper" => "Baby romper", "cap" => "Cap", "hat" => "Hat",
        "socks" => "Socks", "apron" => "Apron", "bag" => "Bag", "scarf" => "Scarf", _ => key
    };

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
        "cap" => "M4,27 Q4,10 20,9 Q35,10 35,24 L35,27 Z M19,7 A1.6,1.6 0 1 1 21,7 A1.6,1.6 0 1 1 19,7 Z M28,27 L39,27 Q39,31 34,31 L22,31 Q20,31 21,28 Z",
        "frock" => "M14,2 L17,2 Q18.5,5 20,5 Q21.5,5 23,2 L26,2 L28,8 L26,16 L36,37 L4,37 L14,16 L12,8 Z",
        "skirt" => "M11,4 H29 V9 H11 Z M11,11 H29 L37,36 H3 Z",
        "polo" => "M8,3 L15,3 L17,10 L20,13 L23,10 L25,3 L32,3 L39,10 L33.5,15 L30.5,12.5 L30.5,36 L9.5,36 L9.5,12.5 L6.5,15 L1,10 Z M19.3,13.5 H20.7 V21 H19.3 Z",
        "hoodie" => "M12,4 Q20,-2 28,4 L33,7 L39,30 L34,32 L30,17 L30,37 L10,37 L10,17 L6,32 L1,30 L7,7 Z M15,7 Q20,13 25,7 Q20,4 15,7 Z",
        "tanktop" => "M11,2 L15,2 Q16,11 20,11 Q24,11 25,2 L29,2 Q29,10 32,14 L32,38 L8,38 L8,14 Q11,10 11,2 Z",
        "longsleeve" => "M12,2 L16,2 Q17.5,5 20,5 Q22.5,5 24,2 L28,2 L35,8 L39,34 L34,35 L30,15 L30,38 L10,38 L10,15 L6,35 L1,34 L5,8 Z",
        "shirt" => "M12,2 L17,2 L20,6 L23,2 L28,2 L35,8 L39,34 L34,35 L30,15 L30,38 L10,38 L10,15 L6,35 L1,34 L5,8 Z M19.4,9 H20.6 V36 H19.4 Z",
        "sweater" => "M12,3 Q20,8 28,3 L35,8 L39,32 L34,33 L30,15 L30,35 L32,38 L8,38 L10,35 L10,15 L6,33 L1,32 L5,8 Z",
        "trousers" => "M10,2 H30 L33,38 H23.5 L20,14 L16.5,38 H7 Z",
        "socks" => "M14,2 H24 V24 L32,30 Q36,34 32,37 Q29,39 25,36 L16,29 Q14,27 14,24 Z",
        "hat" => "M12,8 Q20,3 28,8 L30,24 H10 Z M2,26 Q20,21 38,26 Q38,31 20,30 Q2,31 2,26 Z",
        "apron" => "M14,2 L26,2 L26,8 Q32,9 34,6 L35,9 Q31,13 28,13 L30,38 L10,38 L12,13 Q9,13 5,9 L6,6 Q8,9 14,8 Z M16,24 H24 V30 H16 Z",
        "bag" => "M6,13 H34 L32,38 H8 Z M13,13 Q13,3 20,3 Q27,3 27,13 L24.5,13 Q24.5,6 20,6 Q15.5,6 15.5,13 Z",
        "scarf" => "M8,4 Q20,10 32,4 L34,9 Q27,13 25,14 L28,36 L22,37 L20,16 Q14,14 6,9 Z",
        "jersey" => "M10,2 L16,2 Q18,6 20,6 Q22,6 24,2 L30,2 L38,7 L35,14 L30,12 L30,37 L10,37 L10,12 L5,14 L2,7 Z M15.5,16 H24.5 V19 L20.5,31 H17.2 L21,19 H15.5 Z",
        "romper" => "M13,2 L17,2 Q18.5,6 20,6 Q21.5,6 23,2 L27,2 L33,8 L29,12 L29,26 L31,36 L22,36 L20,30 L18,36 L9,36 L11,26 L11,12 L7,8 Z",
        "vest" => "M10,2 L15,2 L20,12 L25,2 L30,2 L33,10 L33,38 L22,38 L20,16 L18,38 L7,38 L7,10 Z",
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

    /// <summary>WCAG relative luminance (0 = black, 1 = white).</summary>
    public static double Luminance(Color c)
    {
        static double Ch(byte v) { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
    }

    public static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static readonly Color DarkText = Color.FromRgb(0x1E, 0x1E, 0x1E);

    /// <summary>Text colour for a background: the app's near-black, or white when the background is dark.</summary>
    public static Color TextOn(Color background) =>
        Contrast(background, DarkText) >= Contrast(background, Colors.White) ? DarkText : Colors.White;

    public static SolidColorBrush Brush(string hex) => Brush(Parse(hex));
    public static SolidColorBrush Brush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
