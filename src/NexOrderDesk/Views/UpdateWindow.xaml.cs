using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using NexOrderDesk.Core;
using NexOrderDesk.Services;
using NexOrderDesk.ViewModels;
namespace NexOrderDesk.Views;

/// <summary>Check for Updates: shows the result, downloads + verifies the MSI with progress, then hands over to Windows Installer.</summary>
public partial class UpdateWindow : Window
{
    private UpdateCheckResult? _result;
    private string? _verifiedMsi;
    private CancellationTokenSource? _cts;
    private bool _busy, _loading = true;

    private UpdateWindow(UpdateCheckResult? preloaded)
    {
        InitializeComponent();
        _result = preloaded;
        HeaderText.Text = $"Installed: version {AppInfo.Version}" + (AppInfo.UpdatesConfigured ? $"   ·   updates from github.com/{AppInfo.UpdateRepository}" : "");
        VersionFoot.Text = $"NEX ORDER DESK {AppInfo.Version} ({AppInfo.Distribution})";
        AutoCheck.IsChecked = SettingsService.Instance.AutoCheckUpdates;
        _loading = false;
        Loaded += async (_, _) => { if (_result != null) ShowResult(_result); else await CheckAsync(); };
        Closing += (_, e) =>
        {
            if (_busy && MessageBox.Show(this, "Stop the download?", "Software update", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                e.Cancel = true;
            else _cts?.Cancel();
        };
    }

    /// <summary>Opens the window; with a result from the start-up check it shows it directly, otherwise it checks now.</summary>
    public static void Open(UpdateCheckResult? preloaded = null)
    {
        var w = new UpdateWindow(preloaded);
        if (Application.Current.MainWindow is { IsLoaded: true } mw) w.Owner = mw;
        w.ShowDialog();
    }

    // ---------------------------------------------------------------- states
    private void Panels(UIElement visible)
    {
        foreach (var p in new UIElement[] { PanelChecking, PanelMessage, PanelAvailable, PanelProgress })
            p.Visibility = p == visible ? Visibility.Visible : Visibility.Collapsed;
        foreach (var b in new UIElement[] { SkipBtn, PageBtn, RetryBtn, DownloadBtn, InstallBtn }) b.Visibility = Visibility.Collapsed;
        CloseBtn.Content = "Close";
    }

    private void Message(string icon, Brush iconBrush, string title, string text)
    {
        Panels(PanelMessage);
        MessageIcon.Text = icon; MessageIcon.Foreground = iconBrush;
        MessageTitle.Text = title; MessageText.Text = text;
    }

    private void Error(string text)
    {
        Message("⚠", Brushes.DarkOrange, "Update check failed", text + "\n\nNEX ORDER DESK keeps working normally without the internet.");
        RetryBtn.Visibility = Visibility.Visible;
    }

    private async Task CheckAsync()
    {
        Panels(PanelChecking);
        try { ShowResult(await UpdateService.CheckAsync()); }
        catch (UpdateException ex) { Error(ex.Message); }
        catch (Exception ex) { Error("Unexpected problem: " + ex.Message); }
    }

    private void ShowResult(UpdateCheckResult r)
    {
        _result = r;
        switch (r.State)
        {
            case UpdateState.UpToDate:
                Message("✔", new SolidColorBrush(Color.FromRgb(0x0F, 0x7B, 0x3F)), "You are using the latest version.",
                        $"NEX ORDER DESK {AppInfo.Version}" + (r.Manifest != null && r.Manifest.ReleaseDate.Length > 0 ? $"  (latest release: {r.Manifest.Version}, {r.Manifest.ReleaseDate})" : ""));
                break;
            case UpdateState.NotConfigured:
                Message("ℹ", Brushes.SteelBlue, "Online updates are not set up", r.Message);
                break;
            case UpdateState.TooOld:
                Message("⚠", Brushes.DarkOrange, $"Version {r.Manifest?.Version} needs a manual install", r.Message);
                PageBtn.Visibility = Visibility.Visible;
                break;
            case UpdateState.Available when r.Manifest != null:
                Panels(PanelAvailable);
                CurrentText.Text = AppInfo.Version;
                NewText.Text = r.Manifest.Version;
                DateText.Text = r.Manifest.ReleaseDate.Length > 0 ? r.Manifest.ReleaseDate : "—";
                NotesBox.Text = CleanNotes(r.Manifest.ReleaseNotes);
                DownloadBtn.Visibility = Visibility.Visible;
                SkipBtn.Visibility = Visibility.Visible;
                PageBtn.Visibility = Visibility.Visible;
                CloseBtn.Content = "Later";
                break;
        }
    }

    /// <summary>Release notes are Markdown; show them as clean plain text.</summary>
    private static string CleanNotes(string md)
    {
        if (string.IsNullOrWhiteSpace(md)) return "No release notes.";
        var lines = md.Replace("\r\n", "\n").Split('\n').Select(l =>
        {
            var t = l.TrimEnd();
            t = System.Text.RegularExpressions.Regex.Replace(t, @"^#{1,6}\s*", "");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"^\s*[-*]\s+", "  • ");
            return t.Replace("**", "").Replace("__", "").Replace("`", "");
        });
        return string.Join(Environment.NewLine, lines).Trim();
    }

    // ---------------------------------------------------------------- download
    private async void OnDownload(object sender, RoutedEventArgs e)
    {
        if (_result?.Manifest is not { } m || _busy) return;
        _busy = true;
        _cts = new CancellationTokenSource();
        Panels(PanelProgress);
        ProgressTitle.Text = $"Downloading version {m.Version}…";
        ReadyText.Visibility = Visibility.Collapsed;
        Bar.IsIndeterminate = false; Bar.Value = 0;
        CloseBtn.Content = "Cancel";
        var progress = new Progress<(long done, long total)>(p =>
        {
            if (p.total > 0)
            {
                Bar.Value = Math.Min(100, p.done * 100.0 / p.total);
                ProgressText.Text = $"{p.done / 1048576.0:0.0} of {p.total / 1048576.0:0.0} MB  ({Bar.Value:0}%)";
            }
            else { Bar.IsIndeterminate = true; ProgressText.Text = $"{p.done / 1048576.0:0.0} MB"; }
        });
        try
        {
            var path = await UpdateService.DownloadAsync(m, progress, _cts.Token);
            _verifiedMsi = path;
            Bar.IsIndeterminate = false; Bar.Value = 100;
            ProgressTitle.Text = $"✔  Version {m.Version} is downloaded and verified";
            ProgressText.Text = "SHA256 checksum and installer file checked - safe to install.";
            ReadyText.Visibility = Visibility.Visible;
            InstallBtn.Visibility = Visibility.Visible;
            CloseBtn.Content = "Later";
        }
        catch (OperationCanceledException) { ShowResult(_result!); }
        catch (UpdateException ex) { Error(ex.Message); }
        catch (Exception ex) { Error("The download failed: " + ex.Message); }
        finally { _busy = false; }
    }

    // ---------------------------------------------------------------- install
    private void OnInstall(object sender, RoutedEventArgs e)
    {
        if (_verifiedMsi == null) return;
        if (UpdateService.InstallerBusy())
        {
            MessageBox.Show(this, "Another installation is running on this PC. Wait until it has finished, then click Install again.", "Software update");
            return;
        }
        int others = UpdateService.OtherInstances();
        if (others > 0)
        {
            MessageBox.Show(this, $"NEX ORDER DESK is open {others} more time(s). Close the other window(s) first, then click Install again.", "Software update");
            return;
        }
        int open = (Application.Current.MainWindow?.DataContext as MainViewModel)?.Tabs.Count(t => t.IsEditable) ?? 0;
        var warn = open > 0 ? $"\n\n{open} open order(s) are not submitted and will be closed. Click No to submit them first." : "";
        if (MessageBox.Show(this, "NEX ORDER DESK will close and the update will be installed. It starts again by itself when the installation is finished." + warn,
                "Install update", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            SettingsService.Instance.Save();                // settings are on disk before the app closes
            UpdateService.StartInstall(_verifiedMsi);
        }
        catch (Exception ex) { Error(ex.Message); return; }
        Application.Current.Shutdown();
    }

    // ---------------------------------------------------------------- other buttons
    private async void OnRetry(object sender, RoutedEventArgs e) => await CheckAsync();

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        if (_result?.Manifest is { } m) { SettingsService.Instance.SkippedVersion = m.Version; SettingsService.Instance.Save(); }
        Close();
    }

    private void OnReleasePage(object sender, RoutedEventArgs e)
    {
        var url = _result?.Manifest?.ReleasePage is { Length: > 0 } p && p.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
            ? p : UpdateService.ReleasesPage;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private void OnAutoCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SettingsService.Instance.AutoCheckUpdates = AutoCheck.IsChecked == true;
        SettingsService.Instance.Save();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
