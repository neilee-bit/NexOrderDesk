using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using NexOrderDesk.Core;
using NexOrderDesk.Data;
using NexOrderDesk.Models;
namespace NexOrderDesk.Services;

/// <summary>Persistent option lists, order types + last order number (JSON in %LocalAppData%\NexOrderDesk).</summary>
public sealed class SettingsService : ObservableObject
{
    public static SettingsService Instance { get; } = new();
    public ObservableCollection<string> Coordinators { get; } = new();
    public ObservableCollection<string> Materials { get; } = new();
    public ObservableCollection<string> NeckTypes { get; } = new();
    public ObservableCollection<string> SleeveTypes { get; } = new();
    public ObservableCollection<string> PrintTypes { get; } = new();
    public ObservableCollection<ColorOption> Colors { get; } = new();
    public ObservableCollection<string> BottomTypes { get; } = new();
    public ObservableCollection<string> WaistTypes { get; } = new();
    public ObservableCollection<string> PocketTypes { get; } = new();
    public ObservableCollection<string> LegOpenings { get; } = new();
    public ObservableCollection<string> BottomExtras { get; } = new();
    public ObservableCollection<string> FlagMaterials { get; } = new();
    public ObservableCollection<string> PlacketTypes { get; } = new();
    public ObservableCollection<string> ButtonCounts { get; } = new();
    public ObservableCollection<string> PipingTypes { get; } = new();
    public ObservableCollection<string> JacketTypes { get; } = new();
    public ObservableCollection<string> JacketCollars { get; } = new();
    public ObservableCollection<string> JacketSleeves { get; } = new();
    public ObservableCollection<string> JacketClosures { get; } = new();
    public ObservableCollection<string> JacketPockets { get; } = new();
    public ObservableCollection<string> JacketExtras { get; } = new();

    /// <summary>Folder for uploaded order-type icons.</summary>
    public static string IconDir => Path.Combine(Dir, "icons");
    public ObservableCollection<OrderTypeDef> OrderTypes { get; } = new();

    private int _last;
    public int LastOrderNumber { get => _last; set => Set(ref _last, value); }
    private string _paper = "A5";
    /// <summary>"A4" or "A5".</summary>
    public string PaperSize { get => _paper; set => Set(ref _paper, value == "A4" ? "A4" : "A5"); }
    private string _layout = "Single";
    /// <summary>Last Print / PDF layout: Single, TwoUp, Duplex, A5, Half.</summary>
    public string PrintLayout { get => _layout; set => Set(ref _layout, string.IsNullOrWhiteSpace(value) ? "Single" : value); }
    private int _schema;
    private bool _autoCheck = true;
    /// <summary>Check for updates when the app starts.</summary>
    public bool AutoCheckUpdates { get => _autoCheck; set => Set(ref _autoCheck, value); }
    /// <summary>Version not to remind about at start-up ("Skip this version"). Check for Updates still shows it.</summary>
    public string? SkippedVersion { get; set; }

    private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexOrderDesk");
    private static string FilePath => Path.Combine(Dir, "settings.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private SettingsService()
    {
        Apply(DefaultOptions.Create());
        _schema = 0;
        bool hadFile = false;
        try { if (File.Exists(FilePath)) { hadFile = true; Apply(JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(FilePath))); } } catch { /* corrupt file: keep defaults */ }
        if (!hadFile) _schema = 25;
        Upgrade();
    }

    /// <summary>Title + live list behind every "Add / edit list" key. Colours are handled separately (key "colors").</summary>
    public (string Title, ObservableCollection<string>? List) GetList(string key) => key switch
    {
        "coordinators" => ("Coordinators", Coordinators),
        "materials" => ("Material types", Materials),
        "necks" => ("Neck types", NeckTypes),
        "sleeves" => ("Sleeve types", SleeveTypes),
        "printtypes" => ("Print types", PrintTypes),
        "bottomtypes" => ("Bottom types", BottomTypes),
        "waists" => ("Waist types", WaistTypes),
        "pockets" => ("Pocket types", PocketTypes),
        "legs" => ("Leg openings", LegOpenings),
        "extras" => ("Additional custom options", BottomExtras),
        "flagmaterials" => ("Flag materials", FlagMaterials),
        "plackets" => ("Placket types", PlacketTypes),
        "buttons" => ("Button counts", ButtonCounts),
        "pipings" => ("Piping types", PipingTypes),
        "jackettypes" => ("Jacket types", JacketTypes),
        "jacketcollars" => ("Collars", JacketCollars),
        "jacketsleeves" => ("Sleeve & cuff types", JacketSleeves),
        "jacketclosures" => ("Closures", JacketClosures),
        "jacketpockets" => ("Jacket pocket types", JacketPockets),
        "jacketextras" => ("Additional customisation", JacketExtras),
        _ => ("Colours", null)
    };

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T>? src)
    { if (src == null) return; target.Clear(); foreach (var x in src) target.Add(x); }

    private void Apply(SettingsDto? d)
    {
        if (d == null) return;
        Fill(Coordinators, d.Coordinators); Fill(Materials, d.Materials); Fill(NeckTypes, d.NeckTypes);
        Fill(SleeveTypes, d.SleeveTypes); Fill(PrintTypes, d.PrintTypes);
        Fill(BottomTypes, d.BottomTypes); Fill(WaistTypes, d.WaistTypes); Fill(PocketTypes, d.PocketTypes);
        Fill(LegOpenings, d.LegOpenings); Fill(BottomExtras, d.BottomExtras); Fill(FlagMaterials, d.FlagMaterials);
        Fill(PlacketTypes, d.PlacketTypes); Fill(ButtonCounts, d.ButtonCounts); Fill(PipingTypes, d.PipingTypes);
        Fill(JacketTypes, d.JacketTypes); Fill(JacketCollars, d.JacketCollars); Fill(JacketSleeves, d.JacketSleeves);
        Fill(JacketClosures, d.JacketClosures); Fill(JacketPockets, d.JacketPockets); Fill(JacketExtras, d.JacketExtras);
        if (d.Colors != null) { Colors.Clear(); foreach (var c in d.Colors) Colors.Add(new ColorOption(c.Name, c.Hex)); }
        if (d.OrderTypes != null && d.OrderTypes.Count > 0)
        {
            OrderTypes.Clear();
            foreach (var t in d.OrderTypes)
            {
                var id = string.IsNullOrWhiteSpace(t.Id) ? Guid.NewGuid().ToString("N") : t.Id;
                var layout = Enum.TryParse<SheetLayout>(t.Layout, true, out var l) ? l : SheetLayout.Tops;
                OrderTypes.Add(new OrderTypeDef(id, t.Name ?? "Order", t.SheetTitle ?? "ORDER SPECIFICATION SHEET", t.AccentHex ?? "#FFC914", layout, OrderIcons.Normalize(t.Icon))
                { IconFile = t.IconFile });
            }
        }
        if (d.PaperSize != null) PaperSize = d.PaperSize;
        if (d.PrintLayout != null) PrintLayout = d.PrintLayout;
        if (d.AutoCheckUpdates.HasValue) AutoCheckUpdates = d.AutoCheckUpdates.Value;
        if (d.SkippedVersion != null) SkippedVersion = d.SkippedVersion;
        LastOrderNumber = d.LastOrderNumber;
        _schema = Math.Max(_schema, d.SchemaVersion);
    }

    /// <summary>One-time upgrade of an older settings file: add the new SHORTS / FLAGS / DTF order types (never re-added if deleted later).</summary>
    private void Upgrade()
    {
        if (_schema < 22) Upgrade22();
        if (_schema < 25)
        {
            // 2.5: the built-in Jacket Order gets the new jacket sheet layout
            foreach (var t in OrderTypes.Where(t => t.Id == "jacket" && t.Layout == SheetLayout.Tops)) t.Layout = SheetLayout.Jacket;
            _schema = 25;
            Save();
        }
    }

    private void Upgrade22()
    {
        void Ensure(OrderTypeDto t, Func<OrderTypeDef, bool> exists)
        {
            if (OrderTypes.Any(exists)) return;
            var layout = Enum.TryParse<SheetLayout>(t.Layout, true, out var l) ? l : SheetLayout.Tops;
            OrderTypes.Add(new OrderTypeDef(t.Id, t.Name, t.SheetTitle, t.AccentHex, layout, t.Icon));
        }
        Ensure(DefaultOptions.Shorts(), o => o.Id == "shorts" || o.Name.Contains("short", StringComparison.OrdinalIgnoreCase));
        Ensure(DefaultOptions.Flags(), o => o.Id == "flags" || o.Layout == SheetLayout.Flags);
        Ensure(DefaultOptions.Dtf(), o => o.Id == "dtf" || o.Layout == SheetLayout.Dtf);
        _schema = 22;
    }

    private SettingsDto ToDto() => new()
    {
        Coordinators = Coordinators.ToList(), Materials = Materials.ToList(), NeckTypes = NeckTypes.ToList(),
        SleeveTypes = SleeveTypes.ToList(), PrintTypes = PrintTypes.ToList(),
        Colors = Colors.Select(c => new ColorDto { Name = c.Name, Hex = c.Hex }).ToList(),
        BottomTypes = BottomTypes.ToList(), WaistTypes = WaistTypes.ToList(), PocketTypes = PocketTypes.ToList(),
        LegOpenings = LegOpenings.ToList(), BottomExtras = BottomExtras.ToList(), FlagMaterials = FlagMaterials.ToList(),
        PlacketTypes = PlacketTypes.ToList(), ButtonCounts = ButtonCounts.ToList(), PipingTypes = PipingTypes.ToList(),
        JacketTypes = JacketTypes.ToList(), JacketCollars = JacketCollars.ToList(), JacketSleeves = JacketSleeves.ToList(),
        JacketClosures = JacketClosures.ToList(), JacketPockets = JacketPockets.ToList(), JacketExtras = JacketExtras.ToList(),
        OrderTypes = OrderTypes.Select(t => new OrderTypeDto
        {
            Id = t.Id, Name = t.Name, SheetTitle = t.SheetTitle, AccentHex = t.AccentHex, Layout = t.Layout.ToString(), Icon = t.IconKey,
            IconFile = t.IconFile
        }).ToList(),
        PaperSize = PaperSize,
        PrintLayout = PrintLayout,
        AutoCheckUpdates = AutoCheckUpdates,
        SkippedVersion = SkippedVersion,
        SchemaVersion = _schema,
        LastOrderNumber = LastOrderNumber
    };

    public void Save() { try { Directory.CreateDirectory(Dir); File.WriteAllText(FilePath, JsonSerializer.Serialize(ToDto(), Json)); } catch { } }
    public void ExportTo(string path) => File.WriteAllText(path, JsonSerializer.Serialize(ToDto(), Json));
    public void ImportFrom(string path) { Apply(JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(path))); Upgrade(); Save(); }
}
