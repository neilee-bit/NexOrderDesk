using System.Reflection;
namespace NexOrderDesk.Core;

/// <summary>Version and release information compiled into the app (from Directory.Build.props / the release build).</summary>
public static class AppInfo
{
    private static readonly Assembly Asm = Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;

    /// <summary>Installed version, e.g. "2.7.0" (MAJOR.MINOR.PATCH).</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>"owner/repo" of the GitHub repository that hosts releases, or "" when online updates are not configured.</summary>
    public static string UpdateRepository { get; } = ReadRepo();

    /// <summary>"light" (needs .NET 8 Desktop Runtime) or "full" (self-contained) - the updater installs the same kind.</summary>
    public static string Distribution { get; } = Meta("Distribution") is { Length: > 0 } d ? d.ToLowerInvariant() : "light";

    public static bool UpdatesConfigured => UpdateRepository.Length > 0;

    public static string Title => $"NEX ORDER DESK {Version}";

    private static string? Meta(string key) =>
        Asm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;

    private static string ReadVersion()
    {
        var info = Asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info)) return info.Split('+')[0].Trim();
        var v = Asm.GetName().Version;
        return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
    }

    private static string ReadRepo()
    {
        var r = (Meta("UpdateRepository") ?? "").Trim().Trim('/');
        // must look like owner/repo and not be the placeholder
        if (r.Equals("OWNER/REPO", StringComparison.OrdinalIgnoreCase)) return "";
        var parts = r.Split('/');
        return parts.Length == 2 && parts.All(p => p.Length > 0 && p.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')) ? r : "";
    }

    /// <summary>Parses "2.7.1" / "v2.7.1" into a comparable version (null if invalid).</summary>
    public static System.Version? Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().TrimStart('v', 'V').Split('+', '-')[0];
        var p = s.Split('.');
        if (p.Length < 2 || p.Length > 4) return null;
        var n = new int[3];
        for (int i = 0; i < 3; i++)
            if (i < p.Length) { if (!int.TryParse(p[i], out n[i]) || n[i] < 0) return null; }
        return new System.Version(n[0], n[1], n[2]);
    }
}
