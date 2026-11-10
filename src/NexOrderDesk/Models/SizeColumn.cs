using System.ComponentModel;
using System.Windows.Input;
namespace NexOrderDesk.Models;

/// <summary>One column of the size table (header + its filtered size rows).</summary>
public class SizeColumn
{
    public SizeColumn(string header, ICollectionView view, ICommand? addRowCommand = null)
    { Header = header; View = view; AddRowCommand = addRowCommand; }
    public string Header { get; }
    public ICollectionView View { get; }
    /// <summary>Set for the "Optional sizes" column: the small + in its header adds another row.</summary>
    public ICommand? AddRowCommand { get; }
    public bool CanAddRows => AddRowCommand != null;
}
