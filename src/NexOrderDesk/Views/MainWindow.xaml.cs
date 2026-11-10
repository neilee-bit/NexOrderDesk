using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NexOrderDesk.Core;
using NexOrderDesk.Services;
using NexOrderDesk.ViewModels;
using NexOrderDesk.Views.Sections;
namespace NexOrderDesk.Views;

public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); DataContext = new MainViewModel(); }

    private void OnNewType(object sender, RoutedEventArgs e) => AddToggle.IsChecked = false;

    // ---------------------------------------------------------------- online updates
    private UpdateCheckResult? _startupResult;

    /// <summary>Start-up check (if switched on): quiet - only a small "Update available" button appears. Offline = nothing happens.</summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!AppInfo.UpdatesConfigured || !SettingsService.Instance.AutoCheckUpdates) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3));            // let the app finish starting first
            var r = await UpdateService.CheckAsync();
            if (r.State != UpdateState.Available || r.Manifest == null) return;
            if (string.Equals(SettingsService.Instance.SkippedVersion, r.Manifest.Version, StringComparison.OrdinalIgnoreCase)) return;
            _startupResult = r;
            UpdatePill.Content = $"\u2B06  Update {r.Manifest.Version} available";
            UpdatePill.Visibility = Visibility.Visible;
        }
        catch { /* no internet / GitHub unavailable: keep working silently */ }
    }

    private void OnUpdatePill(object sender, RoutedEventArgs e) => UpdateWindow.Open(_startupResult);

    private void OnCheckUpdates(object sender, RoutedEventArgs e) => UpdateWindow.Open();

    private void OnManageTypes(object sender, RoutedEventArgs e)
    {
        AddToggle.IsChecked = false;
        // open after the pop-up has closed
        Dispatcher.BeginInvoke(new Action(() => (DataContext as MainViewModel)?.ManageTypesCommand.Execute(null)),
                               System.Windows.Threading.DispatcherPriority.Background);
    }

    // Ctrl+P opens Print / PDF. Ctrl+V:
    //  - typing in any text field (incl. a workspace text box) with text on the clipboard -> normal paste into that field
    //  - a free workspace on screen (back side, DTF, flag artwork) -> pictures / text become movable objects there
    //  - otherwise a picture goes to the selected image card (or the first empty one) on the front side
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tab = (DataContext as MainViewModel)?.SelectedTab;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (e.Key == Key.P && ctrl && tab != null) { tab.PrintCommand.Execute(null); e.Handled = true; return; }
        // Ctrl+C on a selected picture (front cards, back side, DTF, flags) copies it; in a text field it copies text as usual
        if (e.Key == Key.C && ctrl && tab?.IsEditable == true)
        {
            if (Keyboard.FocusedElement is TextBox) return;
            if (FreeCanvasView.ActiveVisible()?.CopySelected() == true) e.Handled = true;
            return;
        }
        if (e.Key != Key.V || !ctrl || tab?.IsEditable != true) return;
        try
        {
            bool hasImage = Services.ImageLoader.ClipboardHasImage();
            bool inTextField = Keyboard.FocusedElement is TextBox;
            if (inTextField && !hasImage) return;                          // ordinary text paste

            var canvas = FreeCanvasView.ActiveVisible();
            if (canvas != null && (!inTextField || FreeCanvasView.IsCanvasTextBox(Keyboard.FocusedElement) || !tab.IsGarment || tab.IsBackSide))
            {
                if (canvas.Paste()) e.Handled = true;
                return;
            }
            if (hasImage && !tab.IsBackSide && ImageUploadPanel.PasteFromClipboard()) e.Handled = true;
        }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard busy */ }
    }
}
