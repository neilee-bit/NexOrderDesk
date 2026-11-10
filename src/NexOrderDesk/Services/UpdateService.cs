using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NexOrderDesk.Core;
namespace NexOrderDesk.Services;

/// <summary>One installer in the update manifest.</summary>
public sealed class UpdateInstaller
{
    [JsonPropertyName("fileName")] public string FileName { get; set; } = "";
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
}

/// <summary>update.json - published with every GitHub release (made by tools/build-release.ps1).</summary>
public sealed class UpdateManifest
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("tag")] public string Tag { get; set; } = "";
    [JsonPropertyName("releaseDate")] public string ReleaseDate { get; set; } = "";
    [JsonPropertyName("minimumSupportedVersion")] public string MinimumSupportedVersion { get; set; } = "";
    [JsonPropertyName("installerFileName")] public string InstallerFileName { get; set; } = "";
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("releaseNotes")] public string ReleaseNotes { get; set; } = "";
    [JsonPropertyName("releasePage")] public string ReleasePage { get; set; } = "";
    [JsonPropertyName("installers")] public Dictionary<string, UpdateInstaller>? Installers { get; set; }

    /// <summary>The installer of the same kind as this app (light / full), falling back to the main one.</summary>
    public UpdateInstaller PickInstaller(string distribution)
    {
        if (Installers != null && Installers.TryGetValue(distribution, out var i) && i.DownloadUrl.Length > 0) return i;
        return new UpdateInstaller { FileName = InstallerFileName, DownloadUrl = DownloadUrl, Sha256 = Sha256, Size = Size };
    }
}

public enum UpdateState { UpToDate, Available, TooOld, NotConfigured }

public sealed record UpdateCheckResult(UpdateState State, UpdateManifest? Manifest, string Message);

/// <summary>
/// Online updates through GitHub Releases.
///  - reads https://github.com/OWNER/REPO/releases/latest/download/update.json (the newest PUBLISHED release only)
///  - downloads the MSI over HTTPS from that repository's release URL only
///  - verifies size, SHA256 and the MSI file signature before anything is run
///  - installs with Windows Installer (msiexec) after the app has closed, keeping the install folder; user data
///    (%LocalAppData%\NexOrderDesk, Documents\NexOrderDesk Orders) is never touched
/// No tokens or credentials are stored in the app: releases must be downloadable anonymously (public releases).
/// </summary>
public static class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 10 };
        var c = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"NexOrderDesk-Updater/{AppInfo.Version}");
        return c;
    }

    public static string UpdatesFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexOrderDesk", "updates");

    public static string ManifestUrl => $"https://github.com/{AppInfo.UpdateRepository}/releases/latest/download/update.json";
    public static string ReleasesPage => $"https://github.com/{AppInfo.UpdateRepository}/releases";

    // ------------------------------------------------------------------ check
    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        if (!AppInfo.UpdatesConfigured)
            return new(UpdateState.NotConfigured, null, "Online updates are not set up in this build (no GitHub repository configured).");

        UpdateManifest? m;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(20));
            using var resp = await Http.GetAsync(ManifestUrl, cts.Token).ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new UpdateException("No release has been published on GitHub yet (or this repository is private).");
            if (!resp.IsSuccessStatusCode)
                throw new UpdateException($"GitHub answered {(int)resp.StatusCode} {resp.ReasonPhrase}. Please try again later.");
            var json = (await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false)).TrimStart('﻿');
            m = JsonSerializer.Deserialize<UpdateManifest>(json);
        }
        catch (UpdateException) { throw; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new UpdateException("GitHub did not answer in time. Check the internet connection and try again."); }
        catch (HttpRequestException)
        { throw new UpdateException("Could not reach GitHub. Check the internet connection and try again."); }
        catch (JsonException)
        { throw new UpdateException("The update information on GitHub is damaged (update.json). Please contact the administrator."); }

        var latest = AppInfo.Parse(m?.Version);
        var current = AppInfo.Parse(AppInfo.Version) ?? new Version(0, 0, 0);
        if (m == null || latest == null) throw new UpdateException("The update information on GitHub has no valid version number.");

        if (latest <= current) return new(UpdateState.UpToDate, m, "You are using the latest version.");
        var min = AppInfo.Parse(m.MinimumSupportedVersion);
        if (min != null && current < min)
            return new(UpdateState.TooOld, m,
                $"Version {m.Version} cannot be installed automatically over {AppInfo.Version} (needs {m.MinimumSupportedVersion} or newer). " +
                "Download the installer from the release page and run it.");
        return new(UpdateState.Available, m, $"Version {m.Version} is available.");
    }

    // ------------------------------------------------------------------ download + verify
    /// <summary>Downloads the installer for this app's distribution and verifies it. Returns the verified MSI path.</summary>
    public static async Task<string> DownloadAsync(UpdateManifest m, IProgress<(long done, long total)> progress, CancellationToken ct)
    {
        var inst = m.PickInstaller(AppInfo.Distribution);
        ValidateUrl(inst.DownloadUrl);
        var fileName = SafeFileName(inst.FileName.Length > 0 ? inst.FileName : $"NexOrderDesk_{m.Version}_Setup.msi");
        if (!fileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) throw new UpdateException("The release does not point to an .msi installer.");
        if (!IsSha256(inst.Sha256)) throw new UpdateException("The release has no valid SHA256 checksum - the download will not be used.");

        Directory.CreateDirectory(UpdatesFolder);
        foreach (var old in Directory.GetFiles(UpdatesFolder)) { try { File.Delete(old); } catch { } }   // earlier/partial downloads
        var final = Path.Combine(UpdatesFolder, fileName);
        var partial = final + ".partial";

        try
        {
            using (var resp = await Http.GetAsync(inst.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                if (!resp.IsSuccessStatusCode)
                    throw new UpdateException($"The installer could not be downloaded (GitHub answered {(int)resp.StatusCode}).");
                if (resp.RequestMessage?.RequestUri is { } finalUri && finalUri.Scheme != Uri.UriSchemeHttps)
                    throw new UpdateException("The download was redirected to an insecure address and was stopped.");
                long total = resp.Content.Headers.ContentLength ?? inst.Size;
                await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var dst = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                var buf = new byte[81920];
                long done = 0; int n;
                while ((n = await src.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                    done += n;
                    progress.Report((done, total));
                }
            }

            // ---- verification: nothing is run unless all checks pass ----
            var info = new FileInfo(partial);
            if (inst.Size > 0 && info.Length != inst.Size)
                throw new UpdateException($"The download is incomplete ({info.Length:N0} of {inst.Size:N0} bytes). Please try again.");
            if (!HasMsiSignature(partial))
                throw new UpdateException("The downloaded file is not a valid Windows Installer package.");
            var hash = await Sha256Async(partial, ct).ConfigureAwait(false);
            if (!hash.Equals(inst.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new UpdateException("The downloaded installer failed the SHA256 security check and was deleted. Please try again.");

            File.Move(partial, final, true);
            return final;
        }
        catch (OperationCanceledException) { TryDelete(partial); throw; }
        catch (UpdateException) { TryDelete(partial); throw; }
        catch (HttpRequestException) { TryDelete(partial); throw new UpdateException("The download was interrupted. Check the internet connection and try again."); }
        catch (IOException ex) { TryDelete(partial); throw new UpdateException("The download could not be saved: " + ex.Message); }
    }

    /// <summary>Only HTTPS release-asset URLs of the configured repository are accepted.</summary>
    private static void ValidateUrl(string url)
    {
        var expected = $"https://github.com/{AppInfo.UpdateRepository}/releases/download/";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps ||
            !url.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("The installer address is not an official release of this application - update stopped.");
    }

    private static bool IsSha256(string s) => s.Trim().Length == 64 && s.Trim().All(Uri.IsHexDigit);

    private static string SafeFileName(string name) =>
        string.Concat(Path.GetFileName(name).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>MSI files are OLE compound documents: D0 CF 11 E0 A1 B1 1A E1.</summary>
    private static bool HasMsiSignature(string path)
    {
        var sig = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        using var fs = File.OpenRead(path);
        var head = new byte[8];
        return fs.Read(head, 0, 8) == 8 && head.SequenceEqual(sig);
    }

    public static async Task<string> Sha256Async(string path, CancellationToken ct = default)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }

    // ------------------------------------------------------------------ install
    /// <summary>True while another Windows Installer setup is running (only one may run at a time).</summary>
    public static bool InstallerBusy()
    {
        try { if (Mutex.TryOpenExisting(@"Global\_MSIExecute", out var m)) { m.Dispose(); return true; } }
        catch (UnauthorizedAccessException) { return true; }
        catch { }
        return false;
    }

    /// <summary>Other running NexOrderDesk windows (they must be closed so the files can be replaced).</summary>
    public static int OtherInstances()
    {
        using var me = Process.GetCurrentProcess();
        return Process.GetProcessesByName(me.ProcessName).Count(p => { using (p) return p.Id != me.Id; });
    }

    /// <summary>
    /// Starts the update and returns; the caller must then close the app. A small script waits until this app has
    /// exited, runs Windows Installer with a progress window (UAC asks for permission as usual - nothing is bypassed),
    /// keeps the current install folder, writes a log, and starts the updated app again when the install succeeded.
    /// </summary>
    public static void StartInstall(string msiPath)
    {
        if (!File.Exists(msiPath) || !HasMsiSignature(msiPath)) throw new UpdateException("The verified installer is missing. Please download it again.");
        var exe = Environment.ProcessPath ?? throw new UpdateException("Cannot find the running program.");
        var dir = Path.GetDirectoryName(exe)!;
        bool devBuild = dir.Contains(@"\bin\Debug", StringComparison.OrdinalIgnoreCase) || dir.Contains(@"\bin\Release", StringComparison.OrdinalIgnoreCase);
        // installed copy: keep the same folder; developer build: install to the default folder
        // (no trailing backslash before the closing quote - Windows Installer adds it itself)
        string installArg = devBuild ? "" : $" INSTALLFOLDER=\"{dir.TrimEnd('\\')}\"";
        string relaunch = devBuild
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NEX ORDER DESK", "NexOrderDesk.exe")
            : exe;
        var log = Path.Combine(UpdatesFolder, "install.log");
        int pid = Environment.ProcessId;

        var cmd = new StringBuilder();
        cmd.AppendLine("@echo off");
        cmd.AppendLine("title NEX ORDER DESK update");
        cmd.AppendLine("echo Waiting for NEX ORDER DESK to close...");
        cmd.AppendLine(":wait");
        cmd.AppendLine($"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul && (timeout /t 1 /nobreak >nul & goto wait)");
        cmd.AppendLine("echo Installing the update - please allow the Windows permission prompt...");
        cmd.AppendLine($"start \"\" /wait msiexec.exe /i \"{msiPath}\" /passive /norestart /l*v \"{log}\"{installArg}");
        cmd.AppendLine("set RC=%ERRORLEVEL%");
        cmd.AppendLine($"if \"%RC%\"==\"0\" start \"\" \"{relaunch}\"");
        cmd.AppendLine($"if \"%RC%\"==\"3010\" start \"\" \"{relaunch}\"");
        cmd.AppendLine("if not \"%RC%\"==\"0\" if not \"%RC%\"==\"3010\" (");
        cmd.AppendLine("  echo.");
        cmd.AppendLine("  echo The update was not installed ^(code %RC%^). Your previous version and all your data are unchanged.");
        cmd.AppendLine($"  echo Details: {log}");
        cmd.AppendLine("  pause");
        cmd.AppendLine($"  start \"\" \"{exe}\"");
        cmd.AppendLine(")");
        var script = Path.Combine(UpdatesFolder, "run-update.cmd");
        File.WriteAllText(script, cmd.ToString(), new UTF8Encoding(false));

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"\"{script}\"\"")
        {
            UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized, WorkingDirectory = UpdatesFolder
        });
    }
}

public sealed class UpdateException : Exception
{
    public UpdateException(string message) : base(message) { }
}
