using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using NexOrderDesk.Core;
namespace NexOrderDesk.Models;

/// <summary>
/// One line of the bottoms / shorts size strip (2.9): Adults (+ other sizes), Kids, PS.
/// While editing a line shows all its sizes (Kids / PS only when switched on); on the submitted sheet only filled sizes,
/// and a line without quantities is left out. Few filled sizes keep a normal width and sit next to the total.
/// </summary>
public class StripLine : ObservableObject
{
    public const double CellWidth = 64, CompactLimit = 300;
    private readonly ObservableCollection<SizeEntry> _entries;
    private readonly Func<bool> _editing, _enabled;

    public StripLine(string title, ObservableCollection<SizeEntry> entries, Func<bool> editing, Func<bool> enabled)
    {
        Title = title; _entries = entries; _editing = editing; _enabled = enabled;
        var v = new ListCollectionView(entries);
        v.Filter = o => o is SizeEntry e && (_editing() ? (!e.IsCustom || e.IsActive) : e.IsActive && e.HasQuantity);
        View = v;
    }

    public string Title { get; }
    public ICollectionView View { get; }
    public IEnumerable<SizeEntry> Entries => _entries;
    public int Filled => _entries.Count(e => e.IsActive && e.HasQuantity);
    public bool IsShown => _editing() ? _enabled() : Filled > 0;
    public bool Compact => !_editing() && Filled * CellWidth <= CompactLimit;
    /// <summary>Width of the size cells (NaN = use the full width).</summary>
    public double Width => Compact ? Math.Max(1, Filled) * CellWidth : double.NaN;

    public void Refresh()
    {
        View.Refresh();
        OnPropertyChanged(nameof(IsShown)); OnPropertyChanged(nameof(Compact)); OnPropertyChanged(nameof(Width)); OnPropertyChanged(nameof(Filled));
    }
}
