using System.Windows;
using System.Windows.Controls;
using NexOrderDesk.Models;
namespace NexOrderDesk.Views.Sections;

/// <summary>
/// Front image card. Holds any number of pictures as free objects (move, resize with kept proportions, Ctrl+C / Ctrl+V).
/// Clicking the card makes it the paste target; double-click or the small upload button opens the file dialog.
/// </summary>
public partial class ImageUploadPanel : UserControl
{
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(ImageUploadPanel), new PropertyMetadata("UPLOAD IMAGE"));
    public static readonly DependencyProperty DocProperty = DependencyProperty.Register(
        nameof(Doc), typeof(CanvasDoc), typeof(ImageUploadPanel), new PropertyMetadata(null));
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(ImageUploadPanel), new PropertyMetadata(false));

    private static readonly List<ImageUploadPanel> Panels = new();
    private static ImageUploadPanel? _selected;

    public ImageUploadPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => { if (!Panels.Contains(this)) Panels.Add(this); };
        Unloaded += (_, _) => { Panels.Remove(this); if (_selected == this) _selected = null; };
        Pictures.Activated += (_, _) => Select();
    }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public CanvasDoc? Doc { get => (CanvasDoc?)GetValue(DocProperty); set => SetValue(DocProperty, value); }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    private void Select()
    {
        foreach (var p in Panels) if (p != this) p.IsSelected = false;
        _selected = this; IsSelected = true;
    }

    private void OnUpload(object sender, RoutedEventArgs e) { Select(); Pictures.Browse(); e.Handled = true; }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (Doc == null || Doc.Items.Count == 0) return;
        if (MessageBox.Show("Remove all pictures from this card?", "Remove pictures", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            Doc.Clear();
        e.Handled = true;
    }

    /// <summary>Ctrl+V fallback: the selected card, otherwise the first empty card on screen.</summary>
    public static bool PasteFromClipboard()
    {
        var target = _selected is { IsVisible: true } sel ? sel
                   : Panels.FirstOrDefault(p => p.IsVisible && p.Doc is { Items.Count: 0 }) ?? Panels.FirstOrDefault(p => p.IsVisible);
        return target != null && target.Pictures.Paste();
    }
}
