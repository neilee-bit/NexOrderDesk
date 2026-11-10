using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace NexOrderDeskSetup
{
    /// <summary>
    /// Command line (all optional):
    ///   /auto      no questions: on an error the setup closes with exit code 1 instead of waiting (for IT / tests)
    ///   /nolaunch  do not start NEX ORDER DESK at the end
    /// Exit code: 0 = installed, 1 = failed, 2 = cancelled.
    /// </summary>
    public partial class App : Application
    {
        private Mutex _single;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = e.Args.Select(a => a.Trim().TrimStart('/', '-').ToLowerInvariant()).ToArray();

            _single = new Mutex(true, @"Local\NexOrderDesk_Setup_Running", out bool first);
            if (!first)
            {
                if (!args.Contains("auto"))
                    MessageBox.Show("NEX ORDER DESK setup is already running.", "NEX ORDER DESK", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(2);
                return;
            }

            var w = new SetupWindow(auto: args.Contains("auto"), launch: !args.Contains("nolaunch"));
            MainWindow = w;
            w.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _single?.ReleaseMutex(); } catch { }
            _single?.Dispose();
            base.OnExit(e);
        }
    }
}
