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

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexOrderDesk");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error_log.txt"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n\n");
        }
        catch { }
        MessageBox.Show("Something went wrong, but your open orders are safe.\n\n" + e.Exception.Message +
                        "\n\n(Details saved to %LocalAppData%\\NexOrderDesk\\error_log.txt)", "NEX ORDER DESK",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
