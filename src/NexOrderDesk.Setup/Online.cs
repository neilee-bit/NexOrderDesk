using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NexOrderDeskSetup
{
    [DataContract]
    public sealed class ReleaseFile
    {
        [DataMember(Name = "fileName")] public string FileName { get; set; }
        [DataMember(Name = "downloadUrl")] public string DownloadUrl { get; set; }
        [DataMember(Name = "sha256")] public string Sha256 { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
    }

    /// <summary>The part of update.json the setup needs (same file the installed app reads).</summary>
    [DataContract]
    public sealed class ReleaseManifest
    {
        [DataMember(Name = "version")] public string Version { get; set; }
        [DataMember(Name = "installerFileName")] public string InstallerFileName { get; set; }
        [DataMember(Name = "downloadUrl")] public string DownloadUrl { get; set; }
        [DataMember(Name = "sha256")] public string Sha256 { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "installers")] public Dictionary<string, ReleaseFile> Installers { get; set; }

        public ReleaseFile Pick(string variant)
        {
            if (Installers != null && Installers.TryGetValue(variant, out var f) && !string.IsNullOrEmpty(f?.DownloadUrl)) return f;
            return new ReleaseFile { FileName = InstallerFileName, DownloadUrl = DownloadUrl, Sha256 = Sha256, Size = Size };
        }
    }

    public sealed class SetupException : Exception
    {
        public SetupException(string message) : base(message) { }
    }

    /// <summary>Setup facts compiled into the exe.</summary>
    public static class SetupInfo
    {
        public static string Version
        {
            get
            {
                var v = typeof(SetupInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
                int plus = v.IndexOf('+');
                return plus >= 0 ? v.Substring(0, plus) : v;
            }
        }

        /// <summary>owner/repo of the GitHub releases, or "" when not configured.</summary>
        public static string Repository
        {
            get
            {
                var r = typeof(SetupInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "UpdateRepository")?.Value?.Trim() ?? "";
                if (r.Equals("OWNER/REPO", StringComparison.OrdinalIgnoreCase)) return "";
                var parts = r.Split('/');
                if (parts.Length != 2 || parts.Any(p => p.Length == 0 || p.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.'))))
                    return "";
                return r;
            }
        }

        public static string WorkFolder => Path.Combine(Path.GetTempPath(), "NexOrderDesk_Setup");
        public static string LogFile => Path.Combine(WorkFolder, "install.log");
    }

    /// <summary>Downloads the newest published release from GitHub and verifies it (HTTPS, own repository only, size, MSI signature, SHA256).</summary>
    public static class Online
    {
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var c = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 10 })
            { Timeout = TimeSpan.FromMinutes(30) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("NexOrderDesk-Setup/" + (SetupInfo.Version.Length > 0 ? SetupInfo.Version : "1.0"));
            return c;
        }

        public static async Task<ReleaseManifest> GetManifestAsync(CancellationToken ct)
        {
            var repo = SetupInfo.Repository;
            if (repo.Length == 0) throw new SetupException("This setup was built without a GitHub repository, so it cannot download NEX ORDER DESK. Use the offline setup instead.");
            var url = "https://github.com/" + repo + "/releases/latest/download/update.json";
            string json;
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(30));
                    using (var resp = await Http.GetAsync(url, cts.Token).ConfigureAwait(false))
                    {
                        if (resp.StatusCode == HttpStatusCode.NotFound)
                            throw new SetupException("No version of NEX ORDER DESK has been published on GitHub yet.");
                        if (!resp.IsSuccessStatusCode)
                            throw new SetupException("GitHub answered " + (int)resp.StatusCode + ". Please try again in a few minutes.");
                        json = (await resp.Content.ReadAsStringAsync().ConfigureAwait(false)).TrimStart('﻿');
                    }
                }
            }
            catch (SetupException) { throw; }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            { throw new SetupException("GitHub did not answer in time. Check the internet connection and try again."); }
            catch (HttpRequestException)
            { throw new SetupException("Could not connect to the internet. Check the connection and try again, or use the offline setup."); }

            try
            {
                var ser = new DataContractJsonSerializer(typeof(ReleaseManifest), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var m = (ReleaseManifest)ser.ReadObject(ms);
                    if (m == null || string.IsNullOrWhiteSpace(m.Version)) throw new SetupException("The release information on GitHub has no version number.");
                    return m;
                }
            }
            catch (SetupException) { throw; }
            catch (Exception) { throw new SetupException("The release information on GitHub (update.json) could not be read."); }
        }

        /// <summary>Downloads and verifies the installer; returns the path of the verified .msi.</summary>
        public static async Task<string> DownloadAsync(ReleaseFile f, IProgress<(long done, long total)> progress, CancellationToken ct)
        {
            var repo = SetupInfo.Repository;
            var expected = "https://github.com/" + repo + "/releases/download/";
            if (string.IsNullOrEmpty(f.DownloadUrl) || !Uri.TryCreate(f.DownloadUrl, UriKind.Absolute, out var u) ||
                u.Scheme != Uri.UriSchemeHttps || !f.DownloadUrl.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
                throw new SetupException("The installer address is not an official NEX ORDER DESK release - setup stopped.");
            var sha = (f.Sha256 ?? "").Trim();
            if (sha.Length != 64 || !sha.All(Uri.IsHexDigit))
                throw new SetupException("The release has no valid SHA256 checksum - setup stopped.");

            var name = Path.GetFileName(f.FileName ?? "");
            if (name.Length == 0 || !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                name = "NexOrderDesk_Setup.msi";

            Directory.CreateDirectory(SetupInfo.WorkFolder);
            var final = Path.Combine(SetupInfo.WorkFolder, name);
            var partial = final + ".partial";
            TryDelete(final); TryDelete(partial);

            try
            {
                using (var resp = await Http.GetAsync(f.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                        throw new SetupException("The installer could not be downloaded (GitHub answered " + (int)resp.StatusCode + ").");
                    var finalUri = resp.RequestMessage?.RequestUri;
                    if (finalUri != null && finalUri.Scheme != Uri.UriSchemeHttps)
                        throw new SetupException("The download was redirected to an insecure address and was stopped.");
                    long total = resp.Content.Headers.ContentLength ?? f.Size;
                    using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var dst = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        var buf = new byte[81920];
                        long done = 0; int n;
                        while ((n = await src.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            await dst.WriteAsync(buf, 0, n, ct).ConfigureAwait(false);
                            done += n;
                            progress?.Report((done, total));
                        }
                    }
                }

                var len = new FileInfo(partial).Length;
                if (f.Size > 0 && len != f.Size)
                    throw new SetupException("The download is incomplete. Please try again.");
                if (!Verify.IsMsi(partial))
                    throw new SetupException("The downloaded file is not a Windows Installer package.");
                if (!Verify.Sha256(partial).Equals(sha, StringComparison.OrdinalIgnoreCase))
                    throw new SetupException("The downloaded installer failed the SHA256 security check and was deleted. Please try again.");

                File.Move(partial, final);
                return final;
            }
            catch (OperationCanceledException) { TryDelete(partial); throw; }
            catch (SetupException) { TryDelete(partial); throw; }
            catch (HttpRequestException) { TryDelete(partial); throw new SetupException("The download was interrupted. Check the internet connection and try again."); }
            catch (IOException ex) { TryDelete(partial); throw new SetupException("The download could not be saved: " + ex.Message); }
        }

        public static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
    }

    public static class Verify
    {
        /// <summary>MSI files are OLE compound documents: D0 CF 11 E0 A1 B1 1A E1.</summary>
        public static bool IsMsi(string path)
        {
            var sig = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            using (var fs = File.OpenRead(path))
            {
                var head = new byte[8];
                return fs.Read(head, 0, 8) == 8 && head.SequenceEqual(sig);
            }
        }

        public static string Sha256(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(fs).Select(b => b.ToString("X2")));
        }
    }

    /// <summary>The offline setup carries the MSI as an embedded resource.</summary>
    public static class Payload
    {
        private const string Name = "payload.msi";

        public static bool HasEmbeddedMsi => typeof(Payload).Assembly.GetManifestResourceNames().Contains(Name);

        public static string Extract(IProgress<(long done, long total)> progress)
        {
            Directory.CreateDirectory(SetupInfo.WorkFolder);
            var path = Path.Combine(SetupInfo.WorkFolder, "NexOrderDesk_Setup.msi");
            Online.TryDelete(path);
            using (var src = typeof(Payload).Assembly.GetManifestResourceStream(Name))
            using (var dst = File.Create(path))
            {
                if (src == null) throw new SetupException("The installer inside this setup is missing.");
                var buf = new byte[1 << 20];
                long done = 0; int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    done += n;
                    progress?.Report((done, src.Length));
                }
            }
            if (!Verify.IsMsi(path)) throw new SetupException("The installer inside this setup is damaged. Please download the setup again.");
            return path;
        }
    }
}
