using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace NexOrderDeskSetup
{
    /// <summary>
    /// Runs the MSI through Windows Installer with no Windows Installer windows of its own; progress is reported to our
    /// window instead (the same technique branded installers use). A full verbose log is written to %TEMP%\NexOrderDesk_Setup.
    /// </summary>
    public static class MsiRunner
    {
        private delegate int InstallUiHandler(IntPtr context, uint messageType, [MarshalAs(UnmanagedType.LPWStr)] string message);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern int MsiSetInternalUI(int uiLevel, IntPtr window);
        [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern InstallUiHandler MsiSetExternalUI(InstallUiHandler handler, uint filter, IntPtr context);
        [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiEnableLog(uint logMode, string logFile, uint attributes);
        [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiInstallProduct(string packagePath, string commandLine);

        private const int INSTALLUILEVEL_NONE = 2;
        private const uint MT_FATALEXIT = 0x00000000, MT_ERROR = 0x01000000, MT_ACTIONSTART = 0x08000000,
                           MT_ACTIONDATA = 0x09000000, MT_PROGRESS = 0x0A000000;
        private const uint LOG_FATALEXIT = 0x1, LOG_ERROR = 0x2, LOG_ACTIONSTART = 0x100, LOG_ACTIONDATA = 0x200, LOG_PROGRESS = 0x400;
        private const uint LOG_VERBOSE_ALL = 0x1FDF;   // same as msiexec /l*v

        // progress state (Windows Installer "ProgressMessage" protocol)
        private static long _total, _done, _step;
        private static bool _forward = true, _actionData, _generating;
        private static double _shown;
        private static Action<double, string> _report;
        private static InstallUiHandler _keepAlive;   // the delegate must not be garbage-collected during the install
        public static string LastError { get; private set; } = "";

        /// <summary>Installs the package. progress(fraction 0..1 or -1 when unknown, action text). Returns the Windows Installer result code.</summary>
        public static uint Install(string msiPath, Action<double, string> progress)
        {
            _report = progress; _total = _done = _step = 0; _forward = true; _actionData = _generating = false; _shown = 0; LastError = "";
            Directory.CreateDirectory(SetupInfo.WorkFolder);
            MsiEnableLog(LOG_VERBOSE_ALL, SetupInfo.LogFile, 0);
            MsiSetInternalUI(INSTALLUILEVEL_NONE, IntPtr.Zero);
            _keepAlive = Handler;
            var previous = MsiSetExternalUI(_keepAlive, LOG_FATALEXIT | LOG_ERROR | LOG_ACTIONSTART | LOG_ACTIONDATA | LOG_PROGRESS, IntPtr.Zero);
            try
            {
                // REBOOT=ReallySuppress: never restart the PC by itself; the app is closed beforehand so no reboot is needed
                return MsiInstallProduct(msiPath, "REBOOT=ReallySuppress");
            }
            finally
            {
                MsiSetExternalUI(previous, 0, IntPtr.Zero);
                GC.KeepAlive(_keepAlive);
            }
        }

        private static int Handler(IntPtr ctx, uint type, string message)
        {
            try
            {
                switch (type & 0xFF000000)
                {
                    case MT_PROGRESS: OnProgress(message); break;
                    case MT_ACTIONDATA:
                        if (_actionData && _total > 0) { _done += _forward ? _step : -_step; Report(null); }
                        break;
                    case MT_ACTIONSTART: Report(ActionText(message)); break;
                    case MT_ERROR:
                    case MT_FATALEXIT:
                        if (!string.IsNullOrWhiteSpace(message)) LastError = message.Trim();
                        break;
                }
            }
            catch { }
            return 0;   // let Windows Installer use its default answer
        }

        private static void OnProgress(string message)
        {
            var f = Fields(message);
            if (f.Length < 2) return;
            switch (f[0])
            {
                case 0:   // reset: field2 = total ticks, field3 = direction, field4 = 1 while the install script is prepared
                    _total = f[1];
                    _forward = f.Length < 3 || f[2] == 0;
                    _done = _forward ? 0 : _total;
                    _actionData = false;
                    _generating = f.Length >= 4 && f[3] == 1;
                    break;
                case 1:   // action info: ticks per ACTIONDATA message
                    if (f.Length >= 3 && f[2] == 1) { _actionData = true; _step = f[1]; } else _actionData = false;
                    break;
                case 2:   // progress report
                    if (_total > 0) _done += _forward ? f[1] : -f[1];
                    break;
                case 3:   // more ticks
                    _total += f[1];
                    break;
            }
            Report(null);
        }

        private static void Report(string action)
        {
            // preparing = first 10 %, applying the changes = 10..100 %; the bar never moves backwards
            if (_total > 0)
            {
                double p = Math.Max(0, Math.Min(1, (double)_done / _total));
                double frac = _generating ? 0.10 * p : 0.10 + 0.90 * p;
                if (frac > _shown) _shown = frac;
            }
            _report?.Invoke(_shown, action);
        }

        // "1: 0 2: 1234 3: 0 4: 1 " -> [0, 1234, 0, 1]
        private static long[] Fields(string message)
        {
            var list = new List<long>();
            if (string.IsNullOrEmpty(message)) return list.ToArray();
            var parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                if (!parts[i].EndsWith(":")) { i--; continue; }
                long.TryParse(parts[i + 1], out var v);
                list.Add(v);
            }
            return list.ToArray();
        }

        // "Action 12:34:56: InstallFiles. Copying new files" -> "Copying new files"
        private static string ActionText(string message)
        {
            if (string.IsNullOrEmpty(message)) return null;
            int dot = message.IndexOf(". ", StringComparison.Ordinal);
            var t = dot >= 0 ? message.Substring(dot + 2).Trim() : "";
            return t.Length == 0 ? null : t;
        }

        public static string Describe(uint code)
        {
            switch (code)
            {
                case 1602: return "The installation was cancelled. Nothing was changed.";
                case 1618: return "Another program is being installed right now. Wait until it has finished, then press Try again.";
                case 1625: case 1643: return "Windows blocked the installation because of a security policy on this PC. Ask your IT administrator.";
                case 1638: return "Another version of NEX ORDER DESK is installed that cannot be replaced automatically. Remove it in Settings > Apps, then run this setup again.";
                case 1612: case 1620: return "The installer file could not be opened. Download the setup again.";
                case 112: case 1601: return "Not enough free disk space or Windows Installer is not available.";
                default:
                    var detail = string.IsNullOrWhiteSpace(LastError) ? "" : "\n\n" + Shorten(LastError, 300);
                    return "The installation did not finish (code " + code + "). Your previous version and your data are unchanged." + detail;
            }
        }

        private static string Shorten(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";

        // ------------------------------------------------------------------ helpers around the install

        /// <summary>True when the .NET 8 Desktop Runtime (x64) is installed, so the small installer is enough.</summary>
        public static bool HasDesktopRuntime8()
        {
            try
            {
                var pf = Environment.GetEnvironmentVariable("ProgramW6432");
                if (string.IsNullOrEmpty(pf)) pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var dir = Path.Combine(pf, "dotnet", "shared", "Microsoft.WindowsDesktop.App");
                return Directory.Exists(dir) && Directory.GetDirectories(dir, "8.*").Length > 0;
            }
            catch { return false; }
        }

        /// <summary>Running NEX ORDER DESK windows (they must be closed so the program file can be replaced).</summary>
        public static bool AppRunning()
        {
            var ps = Process.GetProcessesByName("NexOrderDesk");
            foreach (var p in ps) p.Dispose();
            return ps.Length > 0;
        }

        public static bool InstallerBusy()
        {
            try { if (Mutex.TryOpenExisting(@"Global\_MSIExecute", out var m)) { m.Dispose(); return true; } }
            catch (UnauthorizedAccessException) { return true; }
            catch { }
            return false;
        }

        /// <summary>Version of the installed NEX ORDER DESK, or null when it is not installed.</summary>
        public static Version InstalledVersion()
        {
            try
            {
                var exe = InstalledExe();
                if (exe == null) return null;
                var v = FileVersionInfo.GetVersionInfo(exe).ProductVersion ?? "";
                int plus = v.IndexOf('+'); if (plus >= 0) v = v.Substring(0, plus);
                return Version.TryParse(v, out var r) ? new Version(r.Major, r.Minor, Math.Max(0, r.Build)) : null;
            }
            catch { return null; }
        }

        /// <summary>Where the installed NexOrderDesk.exe is (the MSI records the folder it used).</summary>
        public static string InstalledExe()
        {
            try
            {
                using (var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var k = hk.OpenSubKey(@"Software\Promotees\NexOrderDesk"))
                {
                    var dir = k?.GetValue("InstallFolder") as string;
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        var exe = Path.Combine(dir.Trim(), "NexOrderDesk.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }
            }
            catch { }
            var pf = Environment.GetEnvironmentVariable("ProgramW6432");
            if (string.IsNullOrEmpty(pf)) pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var def = Path.Combine(pf, "NEX ORDER DESK", "NexOrderDesk.exe");
            return File.Exists(def) ? def : null;
        }

        /// <summary>
        /// Starts the installed app as the normal signed-in user (not as administrator), the same way Explorer would.
        /// </summary>
        public static void LaunchApp(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return;
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + exe + "\"") { UseShellExecute = false }); }
            catch { try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true }); } catch { } }
        }
    }
}
