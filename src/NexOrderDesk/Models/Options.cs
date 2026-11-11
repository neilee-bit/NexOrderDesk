using System.Windows.Media;
using NexOrderDesk.Core;
namespace NexOrderDesk.Models;

public class ColorOption : ObservableObject
{
    private string _name, _hex = "#808080";
    private Brush _brush = Brushes.Gray;
    public ColorOption(string name, string hex) { _name = name; Hex = hex; }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Hex
    {
        get => _hex;
        set
        {
            if (!Set(ref _hex, value)) return;
            try { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); b.Freeze(); _brush = b; }
            catch { _brush = Brushes.Gray; }
            OnPropertyChanged(nameof(Brush));
        }
    }
    public Brush Brush => _brush;
}

public class PrintMethodOption : ObservableObject
{
    private bool _isSelected;
    public PrintMethodOption(string name) { Name = name; }
    public string Name { get; }
    public int SelectionOrder { get; set; }
    public string BannerName =>
        Name.Equals("SUBLIMATION (full)", StringComparison.OrdinalIgnoreCase) ? "FULL SUBLIMATION" :
        Name.Equals("SUBLIMATION (part)", StringComparison.OrdinalIgnoreCase) ? "PART SUBLIMATION" : Name.ToUpperInvariant();
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
}

/// <summary>One material + colour combination (a sheet can carry up to three).</summary>
public class MaterialColorEntry : ObservableObject
{
    private string _material; private ColorOption? _color;
    private string _colorName = "";
    private Brush _colorBrush = Brushes.White;
    private bool _colorFound = true;
    private string _colorHint = "";

    public MaterialColorEntry(string material, ColorOption? color, bool isPrimary)
    {
        _material = material; _color = color; IsPrimary = isPrimary;
        ColorName = color?.Name ?? "WHITE";
    }
    public bool IsPrimary { get; }
    public string PillTitle => IsPrimary ? "Material" : "Extra";
    public string Material { get => _material; set { if (value != null) Set(ref _material, value); } }

    /// <summary>Picking a saved colour fills the colour name.</summary>
    public ColorOption? SelectedColor
    {
        get => _color;
        set { if (value != null && Set(ref _color, value)) ColorName = value.Name; }
    }

    /// <summary>
    /// The colour as typed on the sheet (2.9). The swatch is found automatically: first the saved colour list, then the
    /// built-in colour names (navy blue, bottle green, light pink, …). No colour codes needed.
    /// </summary>
    public string ColorName
    {
        get => _colorName;
        set { if (value != null && Set(ref _colorName, value)) Resolve(); }
    }
    public Brush ColorBrush { get => _colorBrush; private set => Set(ref _colorBrush, value); }
    /// <summary>False when the typed name is not a known colour (the swatch then shows "?").</summary>
    public bool ColorFound { get => _colorFound; private set => Set(ref _colorFound, value); }
    public string ColorHint { get => _colorHint; private set => Set(ref _colorHint, value); }

    private void Resolve()
    {
        var (hex, hint) = LookUp(_colorName);
        ColorFound = hex != null || string.IsNullOrWhiteSpace(_colorName);
        ColorBrush = hex != null ? NexOrderDesk.Core.ColorNames.BrushOf(hex) : Brushes.Transparent;
        ColorHint = hint;
    }

    /// <summary>Colour for a name: saved list first, then the built-in names. Returns (null, …) when unknown.</summary>
    public static (string? Hex, string Hint) LookUp(string? name)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) return (null, "Type a colour, e.g. NAVY BLUE");
        try
        {
            var key = NexOrderDesk.Core.ColorNames.Key(n);
            var saved = NexOrderDesk.Services.SettingsService.Instance.Colors
                .FirstOrDefault(c => NexOrderDesk.Core.ColorNames.Key(c.Name) == key);
            if (saved != null) return (saved.Hex, saved.Name);
        }
        catch { /* settings not ready */ }
        var m = NexOrderDesk.Core.ColorNames.Find(n);
        if (m == null) return (null, $"\"{n}\" is not a known colour - it is printed as typed");
        return (m.Hex, m.Exact ? m.Name : "Colour found: " + m.Name);
    }
}

/// <summary>A tick-box option (pocket types, additional custom features). Only ticked ones appear on the submitted sheet.</summary>
public class CheckOption : ObservableObject
{
    private bool _isSelected;
    public CheckOption(string name, bool selected = false) { Name = name; _isSelected = selected; }
    public string Name { get; }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
}
