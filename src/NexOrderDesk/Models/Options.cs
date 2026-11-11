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
    /// <summary>Every row is labelled "Material" (2.9 - extra rows no longer say "Extra").</summary>
    public string PillTitle => "Material";

    private string _note = "";
    /// <summary>Free words between the material and the colour, e.g. "FRONT &amp; BACK" (2.9).</summary>
    public string Note { get => _note; set { if (value != null && Set(ref _note, value)) OnPropertyChanged(nameof(HasNote)); } }
    public bool HasNote => _note.Trim().Length > 0;
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
        set
        {
            if (value == null) return;
            if (!_picking && _pickedHex != null && !string.Equals(value, _colorName, StringComparison.Ordinal)) _pickedHex = null;   // typed: look the name up again
            if (Set(ref _colorName, value)) Resolve();
        }
    }

    private string? _pickedHex;
    private bool _picking;
    /// <summary>Exact colour chosen in the colour picker (palette, shades or custom), or null when the colour comes from the name.</summary>
    public string? PickedHex => _pickedHex;

    /// <summary>Colour picker: sets the name and the exact colour together.</summary>
    public void Pick(string name, string hex)
    {
        _picking = true;
        try
        {
            _pickedHex = ColorMath.NormalizeHex(hex);
            if (!Set(ref _colorName, (name ?? "").Trim().ToUpperInvariant(), nameof(ColorName))) { }
            Resolve();
        }
        finally { _picking = false; }
    }

    /// <summary>The colour now shown in the swatch (#RRGGBB), or null when the name is unknown.</summary>
    public string? CurrentHex => _pickedHex ?? LookUp(_colorName).Hex;
    public Brush ColorBrush { get => _colorBrush; private set => Set(ref _colorBrush, value); }
    /// <summary>False when the typed name is not a known colour (the swatch then shows "?").</summary>
    public bool ColorFound { get => _colorFound; private set => Set(ref _colorFound, value); }
    public string ColorHint { get => _colorHint; private set => Set(ref _colorHint, value); }

    private void Resolve()
    {
        if (_pickedHex != null)
        {
            ColorFound = true;
            ColorBrush = NexOrderDesk.Core.ColorNames.BrushOf(_pickedHex);
            ColorHint = _colorName.Length > 0 ? _colorName + " (picked)" : "Picked colour";
            OnPropertyChanged(nameof(CurrentHex));
            return;
        }
        var (hex, hint) = LookUp(_colorName);
        ColorFound = hex != null || string.IsNullOrWhiteSpace(_colorName);
        ColorBrush = hex != null ? NexOrderDesk.Core.ColorNames.BrushOf(hex) : Brushes.Transparent;
        ColorHint = hint;
        OnPropertyChanged(nameof(CurrentHex));
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
