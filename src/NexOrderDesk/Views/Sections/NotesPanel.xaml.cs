using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace NexOrderDesk.Views.Sections;

/// <summary>Special notes. No scroll bars: when the text gets long the font shrinks step by step so it all stays visible.</summary>
public partial class NotesPanel : UserControl
{
    private const double MaxFont = 13, MinFont = 7.5;
    private bool _queued;

    public NotesPanel() { InitializeComponent(); }

    private void OnTextChanged(object sender, TextChangedEventArgs e) => Queue();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Queue();

    // Fit after layout (also runs during the off-screen print / PDF render, which processes Loaded-priority work).
    private void Queue()
    {
        if (_queued) return;
        _queued = true;
        Dispatcher.BeginInvoke(new Action(Fit), DispatcherPriority.Loaded);
    }

    private void Fit()
    {
        _queued = false;
        if (Box.ActualHeight <= 0 || Box.ActualWidth <= 0) return;
        double fs = MaxFont;
        Box.FontSize = fs;
        Box.UpdateLayout();
        while (Box.ExtentHeight > Box.ViewportHeight + 0.5 && fs > MinFont)
        {
            fs -= 0.5;
            Box.FontSize = fs;
            Box.UpdateLayout();
        }
        if (!Box.IsKeyboardFocused) Box.ScrollToHome();
    }
}
