using System.Text;
using System.Windows.Media;
using NexOrderDesk.Models;
namespace NexOrderDesk.Core;

/// <summary>
/// Colour by name (2.9): type "navy blue", "bottle green", "light pink", "maroom" … and the colour is found automatically.
/// Built-in library of ~300 garment, textile and web colour names; works offline. Understands light / dark / pale /
/// deep / bright / neon in front of a colour, "grey" = "gray", "colour" / "color" words, and small typing mistakes.
/// </summary>
public static class ColorNames
{
    public sealed record Match(string Name, string Hex, bool Exact);

    // garment & textile names first (they win over web names with the same spelling)
    private static readonly (string Name, string Hex)[] Library =
    {
        ("White","#FFFFFF"),("Optic White","#FBFCFE"),("Off White","#F4F1E8"),("Milk White","#FAF8F2"),("Ivory","#FFFFF0"),("Cream","#F6EFD6"),
        ("Ecru","#E8DFC8"),("Pearl","#EAE6DA"),("Bone","#E3DAC9"),("Black","#1E1E1E"),("Jet Black","#0E0E10"),("Charcoal","#36454F"),
        ("Charcoal Grey","#3E4247"),("Grey","#8D939B"),("Light Grey","#C8CCD1"),("Dark Grey","#555A60"),("Ash","#B2B6B9"),("Ash Grey","#B2B6B9"),
        ("Ash Melange","#C3C5C7"),("Grey Melange","#A7AAAD"),("Heather Grey","#9EA3A6"),("Melange","#B7B9BB"),("Anthra Melange","#4B4E52"),
        ("Silver","#C0C0C0"),("Steel Grey","#71797E"),("Slate Grey","#708090"),("Smoke Grey","#848884"),("Graphite","#41424C"),
        ("Navy","#1B2A4A"),("Navy Blue","#1B2A4A"),("Dark Navy","#141E33"),("Midnight Blue","#191970"),("Royal Blue","#2457C5"),
        ("Blue","#1F5BD8"),("Light Blue","#9CC8EE"),("Dark Blue","#13306E"),("Sky Blue","#6EC1F0"),("Baby Blue","#A9D3F2"),
        ("Powder Blue","#B0E0E6"),("Ice Blue","#D6ECF7"),("Cobalt Blue","#0047AB"),("Cobalt","#0047AB"),("Electric Blue","#1F7BFF"),
        ("Denim","#3B5E8C"),("Denim Blue","#3B5E8C"),("Indigo","#3F3C8F"),("Petrol Blue","#1E5E6E"),("Petrol","#1E5E6E"),
        ("Peacock Blue","#0D6C85"),("Teal","#008080"),("Teal Blue","#167C8C"),("Turquoise","#30C6C6"),("Aqua","#3FD5D5"),
        ("Cyan","#00B7EB"),("Air Force Blue","#5D8AA8"),("Steel Blue","#4682B4"),("Ocean Blue","#1F6FA3"),("Sapphire","#0F52BA"),
        ("Azure","#2F8FEA"),("Cerulean","#2A7AB0"),("Prussian Blue","#003153"),("Oxford Blue","#14213D"),("Space Blue","#1D2951"),
        ("Red","#D62828"),("Bright Red","#EE1C25"),("Dark Red","#8E1B1B"),("Scarlet","#E0282E"),("Crimson","#C0182F"),
        ("Cherry Red","#C1121F"),("Cherry","#B11226"),("Blood Red","#8A0303"),("Brick Red","#A23B2B"),("Rust","#B7410E"),
        ("Maroon","#6D1A2B"),("Wine","#5E1A2F"),("Wine Red","#6B1E2E"),("Burgundy","#6B1F33"),("Oxblood","#4A1316"),
        ("Pink","#F28CB1"),("Light Pink","#F8BBD0"),("Baby Pink","#F6C6D6"),("Hot Pink","#F0428C"),("Dark Pink","#C7366F"),
        ("Rose","#E8A0B4"),("Rose Pink","#F08CAE"),("Dusty Pink","#D8A1A4"),("Blush","#F1C6C1"),("Fuchsia","#D6247F"),
        ("Magenta","#C3177C"),("Salmon","#F28C78"),("Coral","#F26F5B"),("Peach","#F8C3A0"),("Apricot","#F7B98B"),
        ("Orange","#F77F00"),("Light Orange","#FBAE5C"),("Dark Orange","#D9620B"),("Neon Orange","#FF6E1A"),("Burnt Orange","#C1571C"),
        ("Tangerine","#F28500"),("Amber","#FFB000"),("Yellow","#FFD60A"),("Light Yellow","#FFF1A6"),("Lemon","#FFF44F"),
        ("Lemon Yellow","#FFF44F"),("Golden Yellow","#FFC20E"),("Mustard","#D8A31A"),("Mustard Yellow","#D8A31A"),("Neon Yellow","#E9FF1F"),
        ("Butter","#F7E7A1"),("Gold","#C9A227"),("Golden","#D4A72C"),("Old Gold","#B59410"),("Khaki","#B8A77A"),("Beige","#D9C8A9"),
        ("Sand","#D4BF94"),("Camel","#C19A6B"),("Tan","#C8A27A"),("Nude","#E3C3A8"),("Skin","#EBC4A4"),("Taupe","#8B7D6B"),
        ("Brown","#7A4A2A"),("Light Brown","#A9744F"),("Dark Brown","#4A2C1D"),("Chocolate","#5C3317"),("Chocolate Brown","#5C3317"),
        ("Coffee","#6F4E37"),("Coffee Brown","#6F4E37"),("Mocha","#7B5B4A"),("Walnut","#5D432C"),("Copper","#B87333"),("Bronze","#A97142"),
        ("Green","#2E9E4F"),("Light Green","#9AD69A"),("Dark Green","#1E5631"),("Bottle Green","#0B4D2C"),("Forest Green","#228B22"),
        ("Emerald","#1F9D63"),("Emerald Green","#1F9D63"),("Jade","#00A86B"),("Kelly Green","#3BB143"),("Parrot Green","#3EB548"),
        ("Apple Green","#8DB600"),("Lime","#A6D608"),("Lime Green","#7FD03A"),("Neon Green","#39FF14"),("Mint","#A8E6CF"),("Mint Green","#98E2C6"),
        ("Sea Green","#2E8B57"),("Olive","#6B6B2B"),("Olive Green","#5F6B2F"),("Army Green","#4B5320"),("Military Green","#4D5A2C"),
        ("Moss Green","#6D7B3C"),("Sage","#9CAF88"),("Sage Green","#9CAF88"),("Pistachio","#A8C686"),("Leaf Green","#4CAF50"),
        ("Grass Green","#5DA130"),("Hunter Green","#355E3B"),("Pine Green","#01796F"),("Purple","#6A2C91"),("Light Purple","#B58AD6"),
        ("Dark Purple","#3E1D5C"),("Violet","#7F3FBF"),("Lavender","#C8B6E2"),("Lilac","#C8A2C8"),("Mauve","#B784A7"),("Plum","#6E2A5F"),
        ("Grape","#5B2C6F"),("Orchid","#C060C0"),("Royal Purple","#5B2A86"),("Amethyst","#9966CC"),("Multi Colour","#9AA0A9"),
        // web colour names (CSS)
        ("Alice Blue","#F0F8FF"),("Antique White","#FAEBD7"),("Aquamarine","#7FFFD4"),("Bisque","#FFE4C4"),("Blanched Almond","#FFEBCD"),
        ("Blue Violet","#8A2BE2"),("Burly Wood","#DEB887"),("Cadet Blue","#5F9EA0"),("Chartreuse","#7FFF00"),("Cornflower Blue","#6495ED"),
        ("Cornsilk","#FFF8DC"),("Dark Cyan","#008B8B"),("Dark Goldenrod","#B8860B"),("Dark Khaki","#BDB76B"),("Dark Magenta","#8B008B"),
        ("Dark Olive Green","#556B2F"),("Dark Orchid","#9932CC"),("Dark Salmon","#E9967A"),("Dark Sea Green","#8FBC8F"),
        ("Dark Slate Blue","#483D8B"),("Dark Slate Grey","#2F4F4F"),("Dark Turquoise","#00CED1"),("Dark Violet","#9400D3"),
        ("Deep Pink","#FF1493"),("Deep Sky Blue","#00BFFF"),("Dim Grey","#696969"),("Dodger Blue","#1E90FF"),("Fire Brick","#B22222"),
        ("Floral White","#FFFAF0"),("Gainsboro","#DCDCDC"),("Ghost White","#F8F8FF"),("Goldenrod","#DAA520"),("Green Yellow","#ADFF2F"),
        ("Honeydew","#F0FFF0"),("Indian Red","#CD5C5C"),("Lawn Green","#7CFC00"),("Lemon Chiffon","#FFFACD"),("Light Coral","#F08080"),
        ("Light Cyan","#E0FFFF"),("Light Salmon","#FFA07A"),("Light Sea Green","#20B2AA"),("Light Sky Blue","#87CEFA"),
        ("Light Slate Grey","#778899"),("Light Steel Blue","#B0C4DE"),("Linen","#FAF0E6"),("Medium Aquamarine","#66CDAA"),
        ("Medium Blue","#0000CD"),("Medium Orchid","#BA55D3"),("Medium Purple","#9370DB"),("Medium Sea Green","#3CB371"),
        ("Medium Slate Blue","#7B68EE"),("Medium Spring Green","#00FA9A"),("Medium Turquoise","#48D1CC"),("Medium Violet Red","#C71585"),
        ("Mint Cream","#F5FFFA"),("Misty Rose","#FFE4E1"),("Moccasin","#FFE4B5"),("Navajo White","#FFDEAD"),("Old Lace","#FDF5E6"),
        ("Olive Drab","#6B8E23"),("Orange Red","#FF4500"),("Pale Goldenrod","#EEE8AA"),("Pale Green","#98FB98"),("Pale Turquoise","#AFEEEE"),
        ("Pale Violet Red","#DB7093"),("Papaya Whip","#FFEFD5"),("Peach Puff","#FFDAB9"),("Peru","#CD853F"),("Rosy Brown","#BC8F8F"),
        ("Saddle Brown","#8B4513"),("Sandy Brown","#F4A460"),("Seashell","#FFF5EE"),("Sienna","#A0522D"),("Slate Blue","#6A5ACD"),
        ("Snow","#FFFAFA"),("Spring Green","#00FF7F"),("Thistle","#D8BFD8"),("Tomato","#FF6347"),("Wheat","#F5DEB3"),("White Smoke","#F5F5F5"),
        ("Yellow Green","#9ACD32"),("Rebecca Purple","#663399"),("Lemon Lime","#C8E62F"),("Neon Pink","#FF2E93"),("Fluorescent Green","#3DFF2E"),
        ("Fluorescent Yellow","#E6FF00"),("Fluorescent Orange","#FF7A00"),("Fluorescent Pink","#FF3EA5"),("Camouflage","#78866B"),("Camo","#78866B"),
    };

    private static readonly Dictionary<string, (string Name, string Hex)> ByKey = Build();

    private static Dictionary<string, (string, string)> Build()
    {
        var d = new Dictionary<string, (string, string)>();
        foreach (var (n, h) in Library)
        {
            var k = Key(n);
            if (!d.ContainsKey(k)) d[k] = (n, h);
        }
        return d;
    }

    /// <summary>"Navy-Blue colour", "navy  blue", "NAVYBLUE" → "navyblue"; grey and gray are the same.</summary>
    public static string Key(string? s)
    {
        var words = Words(s);
        return string.Concat(words);
    }

    private static List<string> Words(string? s)
    {
        var sb = new StringBuilder();
        foreach (var ch in (s ?? "").ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                 .Select(w => w switch { "gray" => "grey", "colour" or "color" or "colours" or "colors" or "clr" => "", "multicolour" or "multicolor" => "multi", _ => w })
                 .Where(w => w.Length > 0).ToList();
    }

    /// <summary>All library names (for suggestions).</summary>
    public static IEnumerable<string> AllNames => Library.Select(l => l.Name).Distinct(StringComparer.OrdinalIgnoreCase);

    public static string? HexOf(string name) => ByKey.TryGetValue(Key(name), out var v) ? v.Hex : null;

    /// <summary>Finds the colour for a typed name, or null when nothing sensible matches.</summary>
    public static Match? Find(string? typed)
    {
        var words = Words(typed);
        if (words.Count == 0) return null;
        var key = string.Concat(words);
        if (ByKey.TryGetValue(key, out var exact)) return new Match(exact.Name, exact.Hex, true);

        // modifier in front: light / dark / pale / deep / bright / neon …
        double lighten = 0, darken = 0;
        var core = new List<string>(words);
        while (core.Count > 1)
        {
            var m = core[0];
            if (m is "light" or "lite" or "pale" or "baby" or "soft" or "pastel") lighten += 0.35;
            else if (m is "dark" or "deep" or "dk") darken += 0.3;
            else if (m is "bright" or "vivid" or "neon" or "fluorescent" or "fluo") { }
            else break;
            core.RemoveAt(0);
        }
        // longest run of words that is a known colour ("cotton navy blue" → navy blue, "black/white" → black)
        for (int len = core.Count; len >= 1; len--)
            for (int start = 0; start + len <= core.Count; start++)
            {
                var k = string.Concat(core.Skip(start).Take(len));
                if (ByKey.TryGetValue(k, out var hit)) return new Match(hit.Name, Adjust(hit.Hex, lighten, darken), false);
            }
        // one word that starts a colour name: "sky" → Sky Blue, "royal" → Royal Blue
        foreach (var w in core.Where(w => w.Length >= 3))
        {
            var first = Library.FirstOrDefault(l => Words(l.Name).FirstOrDefault() == w);
            if (first.Name != null) return new Match(first.Name, Adjust(first.Hex, lighten, darken), false);
        }
        // small typing mistakes
        for (int len = core.Count; len >= 1; len--)
            for (int start = 0; start + len <= core.Count; start++)
            {
                var k = string.Concat(core.Skip(start).Take(len));
                if (k.Length >= 4 && TryTypo(k, out var hit)) return new Match(hit.Name, Adjust(hit.Hex, lighten, darken), false);
            }
        return null;
    }

    private static string Adjust(string hex, double lighten, double darken)
    {
        if (lighten <= 0 && darken <= 0) return hex;
        var c = ColorMath.Parse(hex);
        if (lighten > 0) c = ColorMath.Lighten(c, Math.Min(0.7, lighten));
        if (darken > 0) c = ColorMath.Darken(c, Math.Min(0.6, darken));
        return ColorMath.ToHex(c);
    }

    /// <summary>Small typing mistakes: "maroom", "turqoise", "lavendar" (1 mistake per 5 letters, max 2).</summary>
    private static bool TryTypo(string k, out (string Name, string Hex) hit)
    {
        hit = default;
        int allowed = Math.Min(2, k.Length / 5);
        if (allowed == 0) return false;
        int best = int.MaxValue;
        foreach (var (key, v) in ByKey)
        {
            if (Math.Abs(key.Length - k.Length) > allowed) continue;
            int d = Distance(k, key, allowed);
            if (d < best) { best = d; hit = v; }
        }
        return best <= allowed;
    }

    private static int Distance(string a, string b, int limit)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i; int rowMin = cur[0];
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                rowMin = Math.Min(rowMin, cur[j]);
            }
            if (rowMin > limit) return limit + 1;
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    public static SolidColorBrush BrushOf(string hex) => ColorMath.Brush(hex);
}
