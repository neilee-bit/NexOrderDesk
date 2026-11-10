using System.IO;
using System.Windows;
using System.Windows.Threading;
namespace NexOrderDesk;

public partial class App : Application
{
    public App()
    {
        // Safety net: an unexpected error shows a message (and is logged) instead of closing the app and losing the order.
        DispatcherUnhandledException += OnUnhandled;
    }

    private static bool _selfTest;

    protected override void OnStartup(StartupEventArgs e)
    {
        // NexOrderDesk.exe --selftest <result file>   (used by the GitHub test build; no window opens)
        int i = Array.FindIndex(e.Args, a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase));
        if (i >= 0)
        {
            StartupUri = null;
            _selfTest = true;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            var file = i + 1 < e.Args.Length ? e.Args[i + 1] : Path.Combine(Path.GetTempPath(), "NexOrderDesk_selftest.txt");
            try { File.WriteAllText(file, "started\n"); } catch { }
            Dispatcher.BeginInvoke(new Action(() => Shutdown(NexOrderDesk.Core.SelfTest.Run(file))), DispatcherPriority.Loaded);
            return;
        }
        base.OnStartup(e);
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexOrderDesk");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error_log.txt"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n\n");
        }
        catch { }
        if (_selfTest) { e.Handled = true; return; }
        MessageBox.Show("Something went wrong, but your open orders are safe.\n\n" + e.Exception.Message +
                        "\n\n(Details saved to %LocalAppData%\\NexOrderDesk\\error_log.txt)", "NEX ORDER DESK",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
