using NexOrderDesk.Models;
namespace NexOrderDesk.Data;

public class ColorDto { public string Name { get; set; } = ""; public string Hex { get; set; } = "#808080"; }

public class OrderTypeDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SheetTitle { get; set; } = "";
    public string AccentHex { get; set; } = "#FFC914";
    public string Layout { get; set; } = "Tops";
    public string Icon { get; set; } = "tshirt";
    /// <summary>Uploaded icon file (optional).</summary>
    public string? IconFile { get; set; }
}

public class SettingsDto
{
    public List<string>? Coordinators { get; set; }
    public List<string>? Materials { get; set; }
    public List<string>? NeckTypes { get; set; }
    public List<string>? SleeveTypes { get; set; }
    public List<string>? PrintTypes { get; set; }
    public List<ColorDto>? Colors { get; set; }
    // Bottoms sheet lists
    public List<string>? BottomTypes { get; set; }
    public List<string>? WaistTypes { get; set; }
    public List<string>? PocketTypes { get; set; }
    public List<string>? LegOpenings { get; set; }
    public List<string>? BottomExtras { get; set; }
    // Flags
    public List<string>? FlagMaterials { get; set; }
    // Tops: placket types, button counts, piping types
    public List<string>? PlacketTypes { get; set; }
    public List<string>? ButtonCounts { get; set; }
    public List<string>? PipingTypes { get; set; }
    // Jackets
    public List<string>? JacketTypes { get; set; }
    public List<string>? JacketCollars { get; set; }
    public List<string>? JacketSleeves { get; set; }
    public List<string>? JacketClosures { get; set; }
    public List<string>? JacketPockets { get; set; }
    public List<string>? JacketExtras { get; set; }
    // Order types (New Order menu) with their theme colours
    public List<OrderTypeDto>? OrderTypes { get; set; }
    /// <summary>"A4" or "A5" - last paper size used for Print / PDF.</summary>
    public string? PaperSize { get; set; }
    /// <summary>Print / PDF layout: Single, TwoUp, Duplex, A5, Half.</summary>
    public string? PrintLayout { get; set; }
    /// <summary>22 = settings already upgraded for 2.2 (FLAGS / DTF / Shorts types added once).</summary>
    public int SchemaVersion { get; set; }
    /// <summary>Check GitHub for a new version when the app starts (null = default on).</summary>
    public bool? AutoCheckUpdates { get; set; }
    /// <summary>A version the user chose to skip in the start-up reminder.</summary>
    public string? SkippedVersion { get; set; }
    public int LastOrderNumber { get; set; }
}

/// <summary>Factory defaults, and the fixed size lists. Option lists are user-editable at runtime (SettingsService).</summary>
public static class DefaultOptions
{
    // Defaults picked automatically on a new sheet
    public const string DefaultBottomType = "Bottom";
    public const string DefaultWaist = "Elastic Waist";
    public const string DefaultPocket = "Side Pockets";
    public const string DefaultLeg = "Open Hem — Straight";
    public const string DefaultPlacket = "3 Pcs Placket";
    public const string DefaultButtons = "2";
    /// <summary>Shown in "Placket Type &amp; Color" / "Button &amp; Button Color" until something is typed.</summary>
    public const string MatchText = "T-shirts match";

    public static SettingsDto Create() => new()
    {
        Coordinators = new() { "Dinithi", "Mahindi", "Kaavee", "Senuri" },
        Materials = new() { "Wetlook", "Close Hole", "Honeycomb", "Tricot" },
        NeckTypes = new() { "Full Collar", "Round Neck", "Crew Neck", "Round Cross Neck" },
        SleeveTypes = new() { "Hem Sleeve", "Cuff Sleeve", "Wrangler Sleeve" },
        PrintTypes = new() { "SUBLIMATION (full)", "EMBROIDERY", "SCREEN PRINT", "DTF", "SUBLIMATION (part)" },
        Colors = new()
        {
            new() { Name = "WHITE", Hex = "#FFFFFF" }, new() { Name = "BLACK", Hex = "#1E1E1E" },
            new() { Name = "NAVY BLUE", Hex = "#1B2A4A" }, new() { Name = "RED", Hex = "#D62828" },
            new() { Name = "ROYAL BLUE", Hex = "#2457C5" }, new() { Name = "SKY BLUE", Hex = "#6EC1F0" },
            new() { Name = "YELLOW", Hex = "#FFD60A" }, new() { Name = "ORANGE", Hex = "#F77F00" },
            new() { Name = "GREEN", Hex = "#2E9E4F" }, new() { Name = "MAROON", Hex = "#6D1A2B" },
            new() { Name = "GREY", Hex = "#8D939B" }, new() { Name = "PINK", Hex = "#F28CB1" },
        },
        BottomTypes = new() { DefaultBottomType, "Full-Length Pants", "Track Pants (Military / Sport)" },
        WaistTypes = new() { DefaultWaist, "Drawstring", "Elastic + Drawstring", "Fitted Waistband (Belt Loops)", "Other" },
        PocketTypes = new()
        {
            DefaultPocket, "Back Pockets (No Flap)", "Back Pockets (With Flap)", "Cargo Pockets", "Zip Pockets",
            "Coin Pocket", "Elastic / Mesh Pocket (Sports)", "No Pockets"
        },
        LegOpenings = new()
        {
            DefaultLeg, "Open Hem — Tapered", "Open Hem — Bootcut", "Elastic Cuff", "Ribbed Cuff", "Drawcord Cuff", "Zipper Cuff"
        },
        BottomExtras = new()
        {
            "Piping", "Contrast Panels", "Contrast Stitching", "Reflective Trim", "Knee Reinforcement Panels", "Belt Loops",
            "Side Stripe / Taping"
        },
        JacketTypes = new() { "Track Jacket", "Bomber Jacket", "Windbreaker", "Varsity Jacket", "Coach Jacket", "Zip-Up Hoodie", "Other" },
        JacketCollars = new() { "Stand-Up Collar", "Hooded", "Rib-Knit Collar", "Shirt-Style Collar", "Zip Mock Neck", "Other" },
        JacketSleeves = new() { "Long Sleeve — Ribbed Cuff", "Long Sleeve — Elastic Cuff", "Long Sleeve — Zipper Cuff", "Raglan Sleeve", "Set-In Sleeve", "Other" },
        JacketClosures = new() { "Full Zip", "Half Zip", "Button Placket", "Zip + Button", "Snap Buttons", "Other" },
        JacketPockets = new() { "Side Pockets", "Zip Pockets", "Kangaroo Pocket", "Chest Pocket", "Inner Pocket", "None" },
        JacketExtras = new()
        {
            "Sleeve Pockets", "Contrast Panels", "Contrast Stitching", "Reflective Trim", "Inner Lining (Mesh)", "Inner Lining (Fleece)",
            "Elastic Hem", "Drawcord Hem", "Woven Label"
        },
        PlacketTypes = new() { DefaultPlacket, "2 Pcs Placket" },
        ButtonCounts = new() { DefaultButtons, "3" },
        PipingTypes = new() { "Single Piping", "Double Piping", "Contrast Piping" },
        FlagMaterials = new() { "Satin", "Silk", "Polyester Silk", "Chiffon Silk", "Imitation Silk" },
        OrderTypes = DefaultOrderTypes(),
        PaperSize = "A5",
        PrintLayout = "Single",
        SchemaVersion = 25
    };

    public static List<OrderTypeDto> DefaultOrderTypes() => new()
    {
        new() { Id = "tshirt", Name = "T-Shirt Order", SheetTitle = "T-SHIRT ORDER SPECIFICATION SHEET", AccentHex = "#FFC914", Layout = "Tops", Icon = "tshirt" },
        new() { Id = "jacket", Name = "Jacket Order", SheetTitle = "JACKET ORDER SPECIFICATION SHEET", AccentHex = "#FF9A1F", Layout = "Jacket", Icon = "jacket" },
        new() { Id = "cutprint", Name = "Cut and Print", SheetTitle = "CUT AND PRINT ORDER SPECIFICATION SHEET", AccentHex = "#43B649", Layout = "Tops", Icon = "cut" },
        new() { Id = "printpress", Name = "Print and Press", SheetTitle = "PRINT AND PRESS ORDER SPECIFICATION SHEET", AccentHex = "#A678E8", Layout = "Tops", Icon = "press" },
        new() { Id = "bottoms", Name = "Bottoms Order", SheetTitle = "BOTTOMS ORDER SPECIFICATION SHEET", AccentHex = "#3B8BEB", Layout = "Bottoms", Icon = "bottoms" },
        Shorts(), Flags(), Dtf()
    };

    public static OrderTypeDto Shorts() => new() { Id = "shorts", Name = "Shorts Order", SheetTitle = "SHORTS ORDER SPECIFICATION SHEET", AccentHex = "#2457C5", Layout = "Bottoms", Icon = "shorts" };
    public static OrderTypeDto Flags() => new() { Id = "flags", Name = "FLAGS", SheetTitle = "FLAG ORDER SPECIFICATION SHEET", AccentHex = "#E63946", Layout = "Flags", Icon = "flag" };
    public static OrderTypeDto Dtf() => new() { Id = "dtf", Name = "DTF", SheetTitle = "DTF ORDER SHEET", AccentHex = "#00A6A6", Layout = "Dtf", Icon = "dtf" };

    /// <summary>Default flag size rows on a new FLAGS sheet.</summary>
    public const int FlagRowsStart = 3, FlagRowsMax = 10;

    public static string[] AdultSizes => new[] { "4XS","3XS","2XS","XS","S","M","L","XL","2XL","3XL","4XL","5XL" };
    public static string[] KidsSizes => new[] { "K3XS","K2XS","KXS","KS","KM","KL","KXL","K2XL","K3XL" };
    public static string[] PreschoolSizes => new[] { "P4XS","P3XS","P2XS","PXS","PS","PM","PL","PXL" };   // 4th column "PS (S/S)"
    /// <summary>Bottoms size breakdown.</summary>
    public static string[] BottomSizes => new[] { "XS","S","M","L","XL","2XL","3XL","4XL" };
    /// <summary>Blank optional-size rows that appear with "+ Other sizes" on the bottoms sheet.</summary>
    public const int OptionalRowsStart = 4, OptionalRowsMax = 12;
}
