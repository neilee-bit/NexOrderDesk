using System.ComponentModel;
using System.Windows.Input;
using NexOrderDesk.Core;
namespace NexOrderDesk.Models;

/// <summary>
/// One column of the size table (header + its filtered size rows).
/// The two adult columns have a selectable garment category (Adults / Unisex / Gents / Lads) and sleeve type (S/S / L/S):
/// the header reads e.g. "Gents (L/S)" and every size row of the column is tagged with the chosen sleeve.
/// </summary>
public class SizeColumn : ObservableObject
{
    private readonly string _header;
    private string _category = "", _sleeve = "";

    public SizeColumn(string header, ICollectionView view, ICommand? addRowCommand = null)
    { _header = header; View = view; AddRowCommand = addRowCommand; }

    /// <summary>Adult column with a selectable category and sleeve type.</summary>
    public static SizeColumn Adult(string category, string sleeve, ICollectionView view)
    {
        var c = new SizeColumn("", view) { IsAdult = true };
        c._category = category; c._sleeve = sleeve;
        c.ApplySleeve();
        return c;
    }

    public bool IsAdult { get; private init; }
    /// <summary>Choices for the adult header drop-downs.</summary>
    public IReadOnlyList<string> Categories => NexOrderDesk.Data.DefaultOptions.GarmentCategories;
    public IReadOnlyList<string> Sleeves => NexOrderDesk.Data.DefaultOptions.GarmentSleeves;
    public bool IsFixed => !IsAdult;
    public string Header => IsAdult ? $"{_category} ({_sleeve})" : _header;

    public string Category
    {
        get => _category;
        set { if (IsAdult && value != null && Set(ref _category, value)) OnPropertyChanged(nameof(Header)); }
    }

    public string Sleeve
    {
        get => _sleeve;
        set { if (IsAdult && value != null && Set(ref _sleeve, value)) { ApplySleeve(); OnPropertyChanged(nameof(Header)); } }
    }

    private void ApplySleeve()
    {
        foreach (var e in Entries) e.SleeveTag = _sleeve;
    }

    public ICollectionView View { get; }
    /// <summary>All size rows of this column (also the hidden ones).</summary>
    public IEnumerable<SizeEntry> Entries => View.SourceCollection.OfType<SizeEntry>();
    /// <summary>True when at least one size of this column has a quantity (empty columns are left off the submitted sheet).</summary>
    public bool HasData => Entries.Any(e => e.IsActive && e.HasQuantity);

    /// <summary>Set for the "Optional sizes" column: the small + in its header adds another row.</summary>
    public ICommand? AddRowCommand { get; }
    public bool CanAddRows => AddRowCommand != null;
}
