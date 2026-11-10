using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NexOrderDeskSetup
{
    /// <summary>
    /// One window, no wizard pages: get the installer (download or unpack), install it with a progress bar,
    /// start NEX ORDER DESK and close - like the Chrome installer.
    /// </summary>
    public partial class SetupWindow : Window
    {
        private enum Phase { Working, Installing, Finished, Failed }

        private readonly bool _auto, _launch;
        private Phase _phase = Phase.Working;
        private CancellationTokenSource _cts;
        private bool _allowClose;

        public SetupWindow(bool auto, bool launch)
        {
            _auto = auto; _launch = launch;
            InitializeComponent();
            VersionText.Text = "Setup " + SetupInfo.Version + (Payload.HasEmbeddedMsi ? "  (offline)" : "");
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => Start();

        private async void Start()
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _phase = Phase.Working;
            ActionPanel.Visibility = Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Visible;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            Bar.Visibility = Visibility.Visible;
            try { await RunAsync(_cts.Token); }
            catch (OperationCanceledException) { Finish(2); }
            catch (SetupException ex) { ShowError(ex.Message); }
            catch (Exception ex) { ShowError("Setup stopped unexpectedly: " + ex.Message); }
        }

        private async Task RunAsync(CancellationToken ct)
        {
            // 1. the program file can only be replaced while the app is closed
            if (MsiRunner.AppRunning())
            {
                if (_auto) throw new SetupException("NEX ORDER DESK is open. Close it and run the setup again.");
                Working("Please close NEX ORDER DESK to continue", "Setup continues by itself as soon as it is closed.");
                while (MsiRunner.AppRunning()) await Task.Delay(1000, ct);
            }

            // 2. get a verified installer
            string msi;
            if (Payload.HasEmbeddedMsi)
            {
                Working("Preparing...", "");
                var p = new Progress<(long done, long total)>(v => ShowBytes(v.done, v.total, false));
                msi = await Task.Run(() => Payload.Extract(p));
            }
            else
            {
                Working("Connecting...", "");
                var m = await Online.GetManifestAsync(ct);
                // small installer when the .NET 8 Desktop Runtime is already on this PC, otherwise the complete one
                var file = m.Pick(MsiRunner.HasDesktopRuntime8() ? "light" : "full");
                Working("Downloading NEX ORDER DESK " + m.Version + "...", "");
                var p = new Progress<(long done, long total)>(v => ShowBytes(v.done, v.total, true));
                msi = await Online.DownloadAsync(file, p, ct);
            }
            ct.ThrowIfCancellationRequested();

            // 3. only one Windows Installer setup can run at a time
            if (MsiRunner.InstallerBusy())
            {
                Working("Waiting for another installation to finish...", "");
                for (int i = 0; MsiRunner.InstallerBusy(); i++)
                {
                    if (_auto && i > 150) throw new SetupException(MsiRunner.Describe(1618));
                    await Task.Delay(2000, ct);
                }
            }

            // 4. install (cannot be cancelled half-way)
            _phase = Phase.Installing;
            CloseButton.Visibility = Visibility.Collapsed;
            Working("Installing...", "");
            uint code = await Task.Run(() => MsiRunner.Install(msi, (frac, action) =>
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (frac > 0) { Bar.IsIndeterminate = false; Bar.Value = frac; }
                    if (!string.IsNullOrEmpty(action)) DetailText.Text = action;
                }))));
            Online.TryDelete(msi);
            if (code != 0 && code != 3010) throw new SetupException(MsiRunner.Describe(code));

            // 5. done - start the app like Chrome does
            _phase = Phase.Finished;
            Bar.IsIndeterminate = false; Bar.Value = 1;
            StatusText.Text = "NEX ORDER DESK is installed";
            DetailText.Text = _launch ? "Starting NEX ORDER DESK..." : (code == 3010 ? "Restart the PC when convenient." : "");
            if (_launch) MsiRunner.LaunchApp(MsiRunner.InstalledExe());
            await Task.Delay(_auto ? 300 : 1800);
            Finish(0);
        }

        // ------------------------------------------------------------------ UI helpers
        private void Working(string status, string detail)
        {
            StatusText.Text = status;
            DetailText.Text = detail;
            Bar.IsIndeterminate = true;
        }

        private void ShowBytes(long done, long total, bool download)
        {
            if (total > 0)
            {
                Bar.IsIndeterminate = false;
                Bar.Value = Math.Min(1.0, (double)done / total);
                DetailText.Text = download ? $"{done / 1048576.0:0.0} MB of {total / 1048576.0:0.0} MB" : $"{100.0 * done / total:0} %";
            }
        }

        private void ShowError(string message)
        {
            _phase = Phase.Failed;
            try
            {
                Directory.CreateDirectory(SetupInfo.WorkFolder);
                File.WriteAllText(Path.Combine(SetupInfo.WorkFolder, "setup-error.txt"), DateTime.Now.ToString("s") + "  " + message);
            }
            catch { }
            if (_auto) { Finish(1); return; }

            StatusText.Text = "Setup could not finish";
            StatusText.Foreground = (Brush)FindResource("Brand");
            DetailText.Text = message;
            Bar.Visibility = Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Visible;
            LogButton.Visibility = File.Exists(SetupInfo.LogFile) ? Visibility.Visible : Visibility.Collapsed;
            ActionPanel.Visibility = Visibility.Visible;
        }

        private void Finish(int exitCode)
        {
            _allowClose = true;
            try { _cts?.Dispose(); } catch { }
            Application.Current.Shutdown(exitCode);
        }

        private bool ConfirmCancel()
        {
            if (_auto) return true;
            return MessageBox.Show(this, "Stop installing NEX ORDER DESK?", "NEX ORDER DESK",
                       MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        // ------------------------------------------------------------------ events
        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
        }

        private void OnMinimise(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_allowClose) return;
            e.Cancel = true;
            switch (_phase)
            {
                case Phase.Installing: return;                       // Windows Installer must finish (or roll back) by itself
                case Phase.Failed: case Phase.Finished:
                    int code = _phase == Phase.Finished ? 0 : 1;
                    Dispatcher.BeginInvoke(new Action(() => Finish(code)));   // not inside the Closing event
                    return;
                default:
                    if (ConfirmCancel())
                    {
                        try { _cts?.Cancel(); } catch { }
                        // if the current step does not react to cancelling (unpacking), it stops before installing
                    }
                    return;
            }
        }

        private void OnRetry(object sender, RoutedEventArgs e) => Start();

        private void OnExit(object sender, RoutedEventArgs e) => Finish(1);

        private void OnShowLog(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("notepad.exe", "\"" + SetupInfo.LogFile + "\"") { UseShellExecute = true }); } catch { }
        }
    }
}
