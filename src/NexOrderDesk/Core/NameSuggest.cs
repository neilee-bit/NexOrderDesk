namespace NexOrderDesk.Core;

/// <summary>Customer name suggestions: partial, case-insensitive matching without duplicates.</summary>
public static class NameSuggest
{
    /// <summary>
    /// Names matching <paramref name="typed"/> (empty = all names). Best matches first:
    /// name starts with the text, then a word in the name starts with it, then the text appears anywhere.
    /// Duplicates (same name in different case / spacing) are shown once.
    /// </summary>
    public static List<string> Match(IEnumerable<string> names, string? typed, int max = 50)
    {
        var t = (typed ?? "").Trim();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hits = new List<(int Rank, int Index, string Name)>();
        int i = 0;
        foreach (var raw in names)
        {
            var n = (raw ?? "").Trim();
            i++;
            if (n.Length == 0 || !seen.Add(Normalize(n))) continue;
            int rank;
            if (t.Length == 0) rank = 0;
            else if (n.StartsWith(t, StringComparison.OrdinalIgnoreCase)) rank = 0;
            else if (n.Split(' ', '&', '-', '.', '/').Any(w => w.StartsWith(t, StringComparison.OrdinalIgnoreCase))) rank = 1;
            else if (n.Contains(t, StringComparison.OrdinalIgnoreCase)) rank = 2;
            else continue;
            hits.Add((rank, i, n));
        }
        return hits.OrderBy(h => h.Rank).ThenBy(h => t.Length == 0 ? h.Index : 0).ThenBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                   .Take(max).Select(h => h.Name).ToList();
    }

    /// <summary>"cap  rashmi" and "Cap Rashmi" are the same name.</summary>
    public static string Normalize(string s) => string.Join(" ", s.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
