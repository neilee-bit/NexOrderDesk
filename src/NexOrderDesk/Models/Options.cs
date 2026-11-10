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
    public MaterialColorEntry(string material, ColorOption? color, bool isPrimary)
    { _material = material; _color = color; IsPrimary = isPrimary; }
    public bool IsPrimary { get; }
    public string PillTitle => IsPrimary ? "Material" : "Extra";
    public string Material { get => _material; set { if (value != null) Set(ref _material, value); } }
    public ColorOption? SelectedColor { get => _color; set { if (value != null) Set(ref _color, value); } }
}

/// <summary>A tick-box option (pocket types, additional custom features). Only ticked ones appear on the submitted sheet.</summary>
public class CheckOption : ObservableObject
{
    private bool _isSelected;
    public CheckOption(string name, bool selected = false) { Name = name; _isSelected = selected; }
    public string Name { get; }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
}
