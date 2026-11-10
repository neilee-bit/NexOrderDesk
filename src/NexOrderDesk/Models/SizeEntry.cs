using NexOrderDesk.Core;
namespace NexOrderDesk.Models;

/// <summary>
/// One size row. Pre-school rows (tops) and optional rows (bottoms) are placeholders (IsActive=false) until their
/// column is switched on. Optional rows are "custom": the size label itself is typed by the user.
/// </summary>
public class SizeEntry : ObservableObject
{
    private int? _quantity;
    private bool _isActive;
    private string _label;
    public SizeEntry(string label, string sleeveTag, bool isPlaceholder = false, bool isCustom = false)
    { _label = label; _sleeveTag = sleeveTag; IsPreschool = isPlaceholder; IsCustom = isCustom; _isActive = !isPlaceholder; }
    public string Label { get => _label; set => Set(ref _label, value ?? ""); }
    private string _sleeveTag;
    /// <summary>"S/S" or "L/S" - printed next to the quantity on the submitted sheet (adult columns follow their sleeve choice).</summary>
    public string SleeveTag { get => _sleeveTag; set => Set(ref _sleeveTag, value ?? ""); }
    /// <summary>True for rows that start hidden (PS sizes / optional sizes).</summary>
    public bool IsPreschool { get; }
    /// <summary>True when the label is editable (optional sizes on the bottoms sheet).</summary>
    public bool IsCustom { get; }
    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
    public int? Quantity
    {
        get => _quantity;
        set { if (Set(ref _quantity, value)) { OnPropertyChanged(nameof(HasQuantity)); OnPropertyChanged(nameof(QtyText)); } }
    }
    /// <summary>
    /// What the quantity box shows / edits. Typing digits sets the quantity; clearing the box REMOVES it (null), so it no longer
    /// counts in the total and no red error box appears (binding an int? straight to a text box failed on an empty box).
    /// </summary>
    public string QtyText
    {
        get => Quantity?.ToString() ?? "";
        set
        {
            var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
            Quantity = digits.Length == 0 ? null : int.TryParse(digits, out var n) ? n : Quantity;
            OnPropertyChanged(nameof(QtyText));
        }
    }
    public bool HasQuantity => (Quantity ?? 0) > 0;
}

/// <summary>One flag size line: width x height (in the order's unit), single / double sided, quantity.</summary>
public class FlagSizeEntry : ObservableObject
{
    private string _w = "", _h = "";
    private int? _qty;
    private bool _double;
    public string WidthText { get => _w; set { if (Set(ref _w, value ?? "")) OnPropertyChanged(nameof(HasContent)); } }
    public string HeightText { get => _h; set { if (Set(ref _h, value ?? "")) OnPropertyChanged(nameof(HasContent)); } }
    public int? Quantity { get => _qty; set { if (Set(ref _qty, value)) { OnPropertyChanged(nameof(HasContent)); OnPropertyChanged(nameof(QtyText)); } } }
    /// <summary>
    /// What the quantity box shows / edits. Typing digits sets the quantity; clearing the box REMOVES it (null), so it no longer
    /// counts in the total and no red error box appears (binding an int? straight to a text box failed on an empty box).
    /// </summary>
    public string QtyText
    {
        get => Quantity?.ToString() ?? "";
        set
        {
            var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
            Quantity = digits.Length == 0 ? null : int.TryParse(digits, out var n) ? n : Quantity;
            OnPropertyChanged(nameof(QtyText));
        }
    }
    public bool IsDoubleSided
    {
        get => _double;
        set { if (Set(ref _double, value)) { OnPropertyChanged(nameof(IsSingleSided)); OnPropertyChanged(nameof(SidesText)); } }
    }
    public bool IsSingleSided { get => !_double; set => IsDoubleSided = !value; }
    public string SidesText => _double ? "Double-sided" : "Single-sided";
    public bool HasContent => (_qty ?? 0) > 0 || _w.Trim().Length > 0 || _h.Trim().Length > 0;
}
