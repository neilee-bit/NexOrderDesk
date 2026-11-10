using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NexOrderDesk.Services;
using NexOrderDesk.ViewModels;
namespace NexOrderDesk.Views;

/// <summary>Minimal Print / PDF dialog: pick a layout, see the exact A4 / A5 pages, then print or save the PDF.</summary>
public partial class PrintPreviewWindow : Window
{
    private readonly OrderViewModel _o;
    private BitmapSource? _front, _back;
    private bool _ready;

    private PrintPreviewWindow(OrderViewModel o, bool pdfFirst)
    {
        InitializeComponent();
        _o = o;
        OrderText.Text = $"Order #{o.OrderNumber}" + (string.IsNullOrWhiteSpace(o.OrderName) ? "" : $"  ·  {o.OrderName}");
        var layout = SheetOutput.ParseLayout(SettingsService.Instance.PrintLayout);
        foreach (var rb in new[] { OptSingle, OptTwoUp, OptDuplex, OptA5, OptHalf })
            rb.IsChecked = (string)rb.Tag == layout.ToString();
        IncludeBack.IsChecked = o.Back.HasContent;
        IncludeBack.IsEnabled = o.Back.HasContent;
        if (!o.Back.HasContent) IncludeBack.Content = "Include the back side (empty)";
        if (pdfFirst) { PdfBtn.Background = (Brush)FindResource("AccentBrush"); PrintBtn.Background = Brushes.White; }
        Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(RenderPreview), DispatcherPriority.Background);
    }

    public static void Open(OrderViewModel o, bool pdfFirst)
    {
        try
        {
            var w = new PrintPreviewWindow(o, pdfFirst);
            if (Application.Current.MainWindow is { IsLoaded: true } mw) w.Owner = mw;
            w.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show("Could not open Print / PDF: " + ex.Message, "Print / PDF"); }
    }

    private PrintLayout Layout =>
        new[] { OptSingle, OptTwoUp, OptDuplex, OptA5, OptHalf }.FirstOrDefault(r => r.IsChecked == true) is { } rb
            ? SheetOutput.ParseLayout((string)rb.Tag) : PrintLayout.Single;

    private bool WithBack => IncludeBack.IsChecked == true;

    private void RenderPreview()
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            (_front, _back) = SheetOutput.RenderSides(_o, 110);
        }
        finally { Mouse.OverrideCursor = null; }
        _ready = true;
        Busy.Visibility = Visibility.Collapsed;
        Refresh();
    }

    private void Refresh()
    {
        if (!_ready || _front == null) return;
        Pages.Items.Clear();
        var layout = Layout;
        var pages = SheetOutput.Compose(layout, _front, _back, WithBack);
        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            // portrait pages ~470 high, landscape pages ~620 wide
            double scale = p.Height >= p.Width ? 470 / p.Height : 620 / p.Width;
            var page = new Border
            {
                Width = p.Width * scale, Height = p.Height * scale, Background = Brushes.White,
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.25 },
                Child = new Viewbox { Stretch = Stretch.Uniform, Child = SheetOutput.BuildPageVisual(p) }
            };
            string label = layout == PrintLayout.Duplex ? (i % 2 == 0 ? $"Paper {i / 2 + 1}  ·  side 1 (front)" : $"Paper {i / 2 + 1}  ·  side 2 (back)")
                                                        : $"Page {i + 1} of {pages.Count}";
            var stack = new StackPanel { Margin = new Thickness(12) };
            stack.Children.Add(page);
            stack.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Brushes.DimGray, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
            Pages.Items.Add(stack);
        }
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SettingsService.Instance.PrintLayout = Layout.ToString();
        SettingsService.Instance.Save();
        Refresh();
    }

    private void OnPrint(object sender, RoutedEventArgs e)
    {
        SettingsService.Instance.PrintLayout = Layout.ToString(); SettingsService.Instance.Save();
        SheetOutput.Print(_o, Layout, WithBack);
    }

    private void OnPdf(object sender, RoutedEventArgs e)
    {
        SettingsService.Instance.PrintLayout = Layout.ToString(); SettingsService.Instance.Save();
        var dlg = new SaveFileDialog
        {
            Filter = "PDF document|*.pdf", FileName = $"Order_{SheetOutput.SafeName(_o)}.pdf",
            InitialDirectory = System.IO.Directory.Exists(SheetOutput.AutoFolder) ? SheetOutput.AutoFolder
                               : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var (front, back) = SheetOutput.RenderSides(_o, 300);
            SheetOutput.WritePdf(dlg.FileName, SheetOutput.Compose(Layout, front, back, WithBack));
            Mouse.OverrideCursor = null;
            _o.ShowStatus($"PDF saved: {System.IO.Path.GetFileName(dlg.FileName)}");
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); } catch { }
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            MessageBox.Show(this, "Could not create the PDF: " + ex.Message, "PDF");
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
