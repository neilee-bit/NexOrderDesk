using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NexOrderDesk.Models;
using NexOrderDesk.Services;
using NexOrderDesk.ViewModels;
using NexOrderDesk.Views.Sections;
namespace NexOrderDesk.Core;

/// <summary>
/// Automatic check of the order sheet, used by the GitHub test build:  NexOrderDesk.exe --selftest result.txt
/// It builds real order sheets (the same views the app shows), fills them, submits them (which saves the PDF exactly like
/// a user's Submit) and checks what is on the sheet. Writes one line per check; exit code 0 = all passed.
/// Nothing is shown on screen and no window opens.
/// </summary>
public static class SelfTest
{
    private static readonly StringBuilder Log = new();
    private static int _failed, _passed;
    private static string _file = "";
    private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();

    private static void Step(string name) { Log.AppendLine($"--- {name}  ({Clock.Elapsed.TotalSeconds:0.0} s)"); Flush(); }
    private static void Flush() { try { File.WriteAllText(_file, Log.ToString()); } catch { } }

    public static int Run(string resultFile)
    {
        _file = resultFile;
        try
        {
            var s = SettingsService.Instance;
            Step("coordinator / T-shirt"); Coordinator(s, SheetLayout.Tops);
            Step("coordinator / jacket"); Coordinator(s, SheetLayout.Jacket);
            Step("print-pdf / T-shirt"); PrintPdfVisibility(s, SheetLayout.Tops);
            Step("print-pdf / jacket"); PrintPdfVisibility(s, SheetLayout.Jacket);
            Step("category and sleeve"); CategoryAndSleeve(s);
            Step("empty columns / T-shirt"); EmptyColumns(s, SheetLayout.Tops);
            Step("empty columns / jacket"); EmptyColumns(s, SheetLayout.Jacket);
            Step("bottoms strip"); BottomsStrip(s);
            Step("theme contrast"); ThemeContrast(s);
            Step("bold selections"); BoldSelections(s);
            Step("images and long names"); ImagesAndNames(s);
            Step("layout space"); LayoutSpace(s);
            Step("material colours"); Colours(s);
            Step("print and press"); PrintPress(s);
            Step("customers"); Customers(s);
            Step("customer box"); CustomerBoxUi(s);
            Step("done");
        }
        catch (Exception ex) { Fail("unexpected error: " + ex); }
        Log.Insert(0, $"NEX ORDER DESK {AppInfo.Version} self-test: {_passed} passed, {_failed} failed\n");
        try { File.WriteAllText(resultFile, Log.ToString()); } catch { }
        return _failed == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ checks
    private static void Coordinator(SettingsService s, SheetLayout layout)
    {
        var o = NewOrder(s, layout);
        var name = s.Coordinators.Count > 1 ? s.Coordinators[1] : s.Coordinators.FirstOrDefault() ?? "Dinithi";
        var live = Host(o);                                   // the sheet as shown on screen
        o.Coordinator = name;
        Pump();
        o.AdultShort.First(e => e.Label == "M").Quantity = 5;

        // the option list is reloaded (Import options / list edited) while the order is open
        var copy = s.Coordinators.ToList(); s.Coordinators.Clear(); foreach (var c in copy) s.Coordinators.Add(c);
        Pump();
        Check($"{layout}: coordinator kept when the coordinator list is reloaded", o.Coordinator == name, o.Coordinator);

        o.SubmitCommand.Execute(null);                        // locks the sheet and saves the PDF (draws the sheet again)
        Pump(); Pump();
        Check($"{layout}: coordinator kept after Submit (auto PDF)", o.Coordinator == name, $"'{o.Coordinator}'");
        Check($"{layout}: PDF saved on Submit", o.LastPdfPath != null && File.Exists(o.LastPdfPath), o.LastPdfPath ?? "none");

        var printed = Render(o);                              // the sheet exactly as it goes to print / PDF
        Check($"{layout}: coordinator printed on the submitted sheet", Texts(printed).Contains(name), string.Join(" | ", Texts(printed).Take(40)));
        Check($"{layout}: coordinator shown on screen after Submit", Texts(live).Contains(name), "");

        SheetOutput.RenderSides(o, 96);                       // Print / PDF button path
        Pump();
        Check($"{layout}: coordinator kept after Print/PDF", o.Coordinator == name, o.Coordinator);

        o.EditCommand.Execute(null); Pump();
        Check($"{layout}: coordinator kept when the order is edited again", o.Coordinator == name && ComboText(live, "CoordinatorCombo") == name,
              ComboText(live, "CoordinatorCombo"));
        Close(live, o);
    }

    private static void PrintPdfVisibility(SettingsService s, SheetLayout layout)
    {
        var o = NewOrder(s, layout);
        var live = Host(o);
        Check($"{layout}: Print and PDF hidden while creating the order",
              !IsShown(Find<Button>(live, "PrintButton")) && !IsShown(Find<Button>(live, "PdfButton")), "");
        Check($"{layout}: Print / PDF cannot run before Submit", !o.PrintCommand.CanExecute(null) && !o.PdfCommand.CanExecute(null), "");
        o.SubmitCommand.Execute(null); Pump(); Pump();
        Check($"{layout}: Print and PDF shown after Submit",
              IsShown(Find<Button>(live, "PrintButton")) && IsShown(Find<Button>(live, "PdfButton")), "");
        Check($"{layout}: Print / PDF available after Submit", o.PrintCommand.CanExecute(null) && o.PdfCommand.CanExecute(null), "");
        o.EditCommand.Execute(null); Pump();
        Check($"{layout}: Print and PDF hidden again while editing", !IsShown(Find<Button>(live, "PrintButton")), "");
        var printed = Render(o);
        Check($"{layout}: action bar never printed", !IsShown(Find<Button>(printed, "PdfButton")), "");
        Close(live, o);
    }

    private static void CategoryAndSleeve(SettingsService s)
    {
        var o = NewOrder(s, SheetLayout.Tops);
        var live = Host(o);
        Check("category/sleeve: defaults Adults (S/S) and Adults (L/S)",
              o.AdultShortColumn.Header == "Adults (S/S)" && o.AdultLongColumn.Header == "Adults (L/S)", o.AdultShortColumn.Header + ", " + o.AdultLongColumn.Header);
        o.AdultShortColumn.Category = "Gents"; o.AdultShortColumn.Sleeve = "L/S";
        o.AdultLongColumn.Category = "Lads";
        o.AdultShortColumn.Category = null!;                  // a drop-down sending "nothing" must not clear it
        Pump();
        Check("category/sleeve: chosen independently", o.AdultShortColumn.Header == "Gents (L/S)" && o.AdultLongColumn.Header == "Lads (L/S)",
              o.AdultShortColumn.Header + ", " + o.AdultLongColumn.Header);
        Check("category/sleeve: sizes tagged with the chosen sleeve", o.AdultShort.All(e => e.SleeveTag == "L/S"), "");
        o.AdultShort.First(e => e.Label == "L").Quantity = 12;
        o.AdultLong.First(e => e.Label == "XL").Quantity = 3;
        o.SubmitCommand.Execute(null); Pump(); Pump();
        var printed = Texts(Render(o));
        Check("category/sleeve: printed on the submitted sheet", printed.Contains("Gents (L/S)") && printed.Contains("Lads (L/S)"),
              string.Join(" | ", printed.Take(60)));
        Check("category/sleeve: kept after Submit", o.AdultShortColumn.Header == "Gents (L/S)" && o.AdultLongColumn.Header == "Lads (L/S)", "");
        o.EditCommand.Execute(null); Pump();
        Check("category/sleeve: kept when edited again", o.AdultShortColumn.Category == "Gents" && o.AdultShortColumn.Sleeve == "L/S", "");
        Close(live, o);
    }

    private static void EmptyColumns(SettingsService s, SheetLayout layout)
    {
        var o = NewOrder(s, layout);
        if (layout == SheetLayout.Jacket) o.IncludeKids = true;
        int allColumns = o.SizeColumnsView.Cast<object>().Count();
        o.AdultLong.First(e => e.Label == "M").Quantity = 7;
        o.AdultLong.First(e => e.Label == "2XL").Quantity = 4;
        o.SubmitCommand.Execute(null); Pump(); Pump();
        var shown = o.SizeColumnsView.Cast<SizeColumn>().ToList();
        Check($"{layout}: empty size columns removed after Submit", shown.Count == 1 && shown[0] == o.AdultLongColumn,
              $"{allColumns} columns while editing, {shown.Count} after Submit: " + string.Join(", ", shown.Select(c => c.Header)));
        Check($"{layout}: totals unchanged", o.TotalQuantity == 11, o.TotalQuantity.ToString());
        var printed = Texts(Render(o));
        Check($"{layout}: printed sheet has only the filled column", printed.Contains("M") && printed.Contains("2XL") && !printed.Contains("Kids (S/S)") && printed.Any(t => t.Contains("11")),
              string.Join(" | ", printed.Take(60)));
        o.EditCommand.Execute(null); Pump();
        Check($"{layout}: all columns and data back when edited", o.SizeColumnsView.Cast<object>().Count() == allColumns &&
              o.AdultLong.First(e => e.Label == "M").Quantity == 7, "");
        o.Detach();
    }

    private static void BottomsStrip(SettingsService s)
    {
        var o = NewOrder(s, SheetLayout.Bottoms);
        var live = Host(o);
        Check("bottoms: all adult sizes 4XS-5XL", string.Join(",", o.BottomSizes.Select(e => e.Label)) == "4XS,3XS,2XS,XS,S,M,L,XL,2XL,3XL,4XL,5XL",
              string.Join(",", o.BottomSizes.Select(e => e.Label)));
        Check("bottoms: the same size table as other sheets", FindAll<SizeTablePanel>(live).Any(IsShown), "");
        Check("bottoms: only the Adults column until Kids / PS are switched on",
              o.SizeColumnsView.Cast<SizeColumn>().Select(c => c.Header).SequenceEqual(new[] { "Adults" }), string.Join(", ", o.SizeColumns.Select(c => c.Header)));
        Check("bottoms: no '+ Other sizes' button", !FindAll<System.Windows.Controls.Primitives.ToggleButton>(live).Any(t => IsShown(t) && Equals(t.Content, o.ExtraSizesText)), o.ExtraSizesText);
        o.IncludeBottomPs = true; o.IncludeKids = true;
        var heads = o.SizeColumnsView.Cast<SizeColumn>().Select(c => c.Header).ToList();
        Check("bottoms: + Kids and + PS add columns in order", heads.SequenceEqual(new[] { "Adults", "Kids", "PS" }), string.Join(", ", heads));
        o.BottomAdultColumn.Category = "Gents";
        o.BottomAdultColumn.Sleeve = "L/S";                   // no sleeve on bottoms
        Check("bottoms: adult category without sleeve", o.BottomAdultColumn.Header == "Gents", o.BottomAdultColumn.Header);
        o.BottomSizes.First(e => e.Label == "M").Quantity = 10;
        o.BottomSizes.First(e => e.Label == "5XL").Quantity = 2;
        o.Kids.First(e => e.Label == "KM").Quantity = 6;
        o.Preschool.First(e => e.Label == "PS").Quantity = 4;
        Check("bottoms: total includes kids and PS", o.TotalQuantity == 22, o.TotalQuantity.ToString());
        o.SubmitCommand.Execute(null); Pump(); Pump();
        heads = o.SizeColumnsView.Cast<SizeColumn>().Select(c => c.Header).ToList();
        Check("bottoms: empty columns left off after Submit", heads.SequenceEqual(new[] { "Gents", "Kids", "PS" }), string.Join(", ", heads));
        var printed = Texts(Render(o));
        Check("bottoms: printed sizes, columns and total", new[] { "M", "5XL", "KM", "PS", "Kids", "Gents" }.All(printed.Contains) &&
              printed.Any(t => t.Contains("22")) && !printed.Contains("XS"), string.Join(" | ", printed.Take(60)));
        o.EditCommand.Execute(null); o.IncludeKids = false; Pump();
        Check("bottoms: switching Kids off removes its column and quantities", o.TotalQuantity == 16 && !o.SizeColumns.Any(c => c.Header == "Kids"), o.TotalQuantity.ToString());
        Close(live, o);
    }

    private static void ThemeContrast(SettingsService s)
    {
        var o = NewOrder(s, SheetLayout.Tops);
        foreach (var (hex, white) in new[] { ("#1B2A4A", true), ("#1E1E1E", true), ("#6A2C91", true), ("#FFC914", false), ("#6EC1F0", false) })
        {
            ThemeService.Apply(hex); Pump();
            var on = (Application.Current.Resources["OnAccentBrush"] as SolidColorBrush)?.Color ?? Colors.Transparent;
            Check($"theme {hex}: text on the theme colour is {(white ? "white" : "dark")}", (on == Colors.White) == white, on.ToString());
            var printed = Render(o);
            var num = FindAll<TextBox>(printed).FirstOrDefault(t => t.Text == o.OrderNumber);
            var fg = (num?.Foreground as SolidColorBrush)?.Color;
            Check($"theme {hex}: order number readable", fg.HasValue && NexOrderDesk.Models.ColorMath.Contrast(fg.Value, NexOrderDesk.Models.ColorMath.Parse(hex)) >= 3,
                  fg?.ToString() ?? "not found");
        }
        ThemeService.Apply(o.AccentHex);
        o.Detach();
    }

    private static void BoldSelections(SettingsService s)
    {
        var o = NewOrder(s, SheetLayout.Tops);
        var printed = Render(o);
        var neck = FindAll<ComboBox>(printed).FirstOrDefault(c => c.SelectedItem as string == o.NeckType);
        Check("bold: neck type shown in bold", neck != null && neck.FontWeight.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight(),
              neck?.FontWeight.ToString() ?? "not found");
        var combos = FindAll<ComboBox>(printed).Where(c => IsShown(c) && c.SelectedItem != null).ToList();
        Check("bold: every selected value on the sheet is bold", combos.Count > 3 && combos.All(c => c.FontWeight.ToOpenTypeWeight() >= 700),
              string.Join(", ", combos.Where(c => c.FontWeight.ToOpenTypeWeight() < 700).Select(c => c.SelectedItem)));
        o.Detach();
    }

    private static void ImagesAndNames(SettingsService s)
    {
        // one picture on an empty area fills it (front image card and the DTF workspace)
        foreach (var layout in new[] { SheetLayout.Tops, SheetLayout.Dtf })
        {
            var o = NewOrder(s, layout);
            var live = Host(o);
            var canvas = FindAll<FreeCanvasView>(live).First(c => IsShown(c));
            var bmp = BitmapSource.Create(400, 200, 96, 96, PixelFormats.Bgr32, null, new byte[400 * 200 * 4], 400 * 4);
            canvas.AddImages(new[] { bmp }); Pump();
            var doc = canvas.Doc!;
            var it = doc.Items.Single();
            var area = new Size(canvas.Surface.ActualWidth, canvas.Surface.ActualHeight);
            bool fills = it.W >= area.Width * 0.9 || it.H >= area.Height * 0.9;
            Check($"{layout}: a single picture fills the image area", fills && it.X >= 0 && it.Y >= 0,
                  $"picture {it.W:0}x{it.H:0} in area {area.Width:0}x{area.Height:0}");
            Close(live, o);
        }
        var n = NewOrder(s, SheetLayout.Tops);
        n.OrderName = "VERY LONG ORDER NAME FOR THE ANNUAL SPORTS MEET 2026 - HOUSE T-SHIRTS";
        var sheet = Render(n);
        var box = FindAll<TextBox>(sheet).FirstOrDefault(t => t.Text == n.OrderName);
        bool fits = false; string info = "not found";
        if (box != null)
        {
            var ft = new FormattedText(box.Text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(box.FontFamily, box.FontStyle, box.FontWeight, box.FontStretch), box.FontSize, Brushes.Black, 1);
            if (box.TextWrapping == TextWrapping.Wrap) ft.MaxTextWidth = box.ActualWidth - 6;
            fits = box.FontSize < 14 && box.FontSize >= 6 && ft.Width <= box.ActualWidth && ft.Height <= box.ActualHeight;
            info = $"font {box.FontSize}, {box.TextWrapping}, text {ft.Width:0}x{ft.Height:0} in box {box.ActualWidth:0}x{box.ActualHeight:0}";
        }
        Check("long order name fits its box (smaller / two lines)", fits, info);
        n.OrderName = "SPORTS MEET 2026 HOUSE T-SHIRTS";
        sheet = Render(n);
        box = FindAll<TextBox>(sheet).FirstOrDefault(t => t.Text == n.OrderName);
        bool medFits = false;
        if (box != null)
        {
            var ft = new FormattedText(box.Text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(box.FontFamily, box.FontStyle, box.FontWeight, box.FontStretch), box.FontSize, Brushes.Black, 1);
            if (box.TextWrapping == TextWrapping.Wrap) ft.MaxTextWidth = box.ActualWidth - 6;
            medFits = box.FontSize >= 9 && ft.Width <= box.ActualWidth && ft.Height <= box.ActualHeight;
        }
        Check("medium order name fits and stays readable", medFits, box == null ? "not found" : $"font {box.FontSize}, {box.TextWrapping}");
        n.Detach();
    }

    private static void LayoutSpace(SettingsService s)
    {
        var o = NewOrder(s, SheetLayout.Tops);
        o.MaterialColors[0].Note = "FRONT & BACK";
        o.AddMaterialCommand.Execute(null);
        Check("material: added rows say Material (not Extra)", o.MaterialColors.Count == 2 && o.MaterialColors.All(m => m.PillTitle == "Material"), "");
        var live = Host(o);
        var liveSizes = FindAll<SizeTablePanel>(live).First(IsShown).ActualHeight;
        var liveTexts = Texts(live);
        Check("material: note box between material and colour", liveTexts.Contains("FRONT & BACK") && liveTexts.Contains("e.g. front & back"), "");
        var pills = FindAll<System.Windows.Controls.Primitives.ToggleButton>(FindAll<PrintMethodPanel>(live).First(IsShown)).Where(IsShown).ToList();
        Check("print method: bigger buttons", pills.Count >= 3 && pills.All(p => p.ActualHeight >= 20), string.Join(", ", pills.Select(p => p.ActualHeight.ToString("0"))));
        var bar = FindAll<ActionBar>(live).First();
        Check("action bar: small buttons", bar.ActualHeight <= 26 && FindAll<Button>(bar).Where(IsShown).All(b => b.ActualHeight <= 24), bar.ActualHeight.ToString("0"));
        o.SubmitCommand.Execute(null); Pump(); Pump();
        var printed = Render(o);
        var printSizes = FindAll<SizeTablePanel>(printed).First(IsShown).ActualHeight;
        var pt = Texts(printed);
        Check("print: the button row is removed so the order uses the space", printSizes > liveSizes + 10, $"size table {liveSizes:0} on screen, {printSizes:0} printed");
        Check("material: note printed, empty note and hint left off", pt.Contains("FRONT & BACK") && !pt.Contains("e.g. front & back"), "");
        Close(live, o);
    }

    private static void Colours(SettingsService s)
    {
        var o = NewOrder(s, SheetLayout.Tops);
        var m = o.MaterialColors[0];
        Check("colour: default WHITE", m.ColorName == "WHITE" && m.ColorFound, m.ColorName);
        foreach (var (typed, hex) in new[] { ("BOTTLE GREEN", "#0B4D2C"), ("navy blue", "#1B2A4A"), ("MAROOM", "#6D1A2B"), ("ash melange", "#C3C5C7"), ("SKY", "#6EC1F0") })
        {
            m.ColorName = typed;
            var got = (m.ColorBrush as SolidColorBrush)?.Color.ToString().Replace("#FF", "#");
            Check($"colour: '{typed}' found automatically", m.ColorFound && got == hex, got ?? "");
        }
        m.ColorName = "QWERTY ZZZ";
        Check("colour: unknown name kept as typed", !m.ColorFound && m.ColorName == "QWERTY ZZZ", "");
        m.ColorName = "BOTTLE GREEN";
        var sug = ColorBox.Suggest("nav").Select(x => x.Name).ToList();
        Check("colour: suggestions while typing ('nav')", sug.Contains("NAVY BLUE"), string.Join(", ", sug.Take(8)));
        var printed = Texts(Render(o));
        Check("colour: printed on the sheet", printed.Contains("BOTTLE GREEN"), "");
        o.Detach();

        Check("colour: DAMRO (teal light blue) can be typed and picked", ColorNames.Find("damro")?.Hex == "#45B3C6" &&
              ColorBox.Suggest("dam").Any(x => x.Name == "DAMRO") && ColorFamilies.InFamily("Teals & Turquoise").Any(x => x.Name == "DAMRO"), "");
        Check("colour: no long names in the palette and suggestions", ColorFamilies.Library.All(x => x.Name.Length <= 13) && ColorBox.Suggest("").All(x => x.Name.Length <= 13 || s.Colors.Any(c => c.Name == x.Name)),
              string.Join(", ", ColorFamilies.Library.Where(x => x.Name.Length > 13).Select(x => x.Name)));

        // advanced colour picker
        Check("picker: every built-in colour is in a family", ColorFamilies.Library.All(sw => ColorFamilies.Names.Contains(ColorFamilies.FamilyOf(sw.Hex))) &&
              ColorFamilies.Names.All(f => ColorFamilies.InFamily(f).Count() >= 8), string.Join(", ", ColorFamilies.Names.Select(f => f + " " + ColorFamilies.InFamily(f).Count())));
        Check("picker: family sorting (navy, bottle green, maroon)", ColorFamilies.FamilyOf("#1B2A4A") == "Blues & Navy" && ColorFamilies.FamilyOf("#0B4D2C") == "Greens" &&
              ColorFamilies.FamilyOf("#6D1A2B") == "Reds & Maroons", "");
        var p = NewOrder(s, SheetLayout.Tops);
        var w = new Window { Width = 1100, Height = 820, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -3000, Top = -3000,
                             Content = new Border { Width = 840, Height = 592, Child = new OrderSheetView { DataContext = p } } };
        w.Show(); Pump();
        var cb = FindAll<ColorBox>(w).FirstOrDefault(IsShown);
        if (cb == null) { Fail("picker: colour field not found"); w.Close(); p.Detach(); return; }
        cb.OpenPicker(); Pump();
        Check("picker: opens on the current colour's family", cb.IsPickerOpen && cb.Picker.CurrentFamily == "Whites & Creams" && cb.Picker.VisibleSwatches.Count >= 10,
              $"{cb.IsPickerOpen} {cb.Picker.CurrentFamily} {cb.Picker.VisibleSwatches.Count}");
        cb.Picker.ShowFamily("Blues & Navy"); Pump();
        var navy = cb.Picker.VisibleSwatches.FirstOrDefault(x => x.Name == "NAVY BLUE");
        Check("picker: palette family shows its colours", navy != null, string.Join(", ", cb.Picker.VisibleSwatches.Take(10).Select(x => x.Name)));
        if (navy != null) cb.Picker.Use(navy.Name, navy.Hex);
        Pump();
        var pm = p.MaterialColors[0];
        Check("picker: a palette click fills name and colour and closes", pm.ColorName == "NAVY BLUE" && pm.PickedHex == "#1B2A4A" && !cb.IsPickerOpen,
              $"{pm.ColorName} {pm.PickedHex} {cb.IsPickerOpen}");
        cb.OpenPicker(); Pump();
        Check("picker: seven shades of the current colour", cb.Picker.ShadeSwatches.Count == 7 && cb.Picker.ShadeSwatches[5].Name == "DARK NAVY BLUE",
              string.Join(", ", cb.Picker.ShadeSwatches.Select(x => x.Name)));
        cb.Picker.SetCustom(140, 0.85, 0.45); Pump();
        var customHex = cb.Picker.CurrentHex; var customName = cb.Picker.CurrentName;
        Check("picker: custom colour gets the closest colour name", customName.Contains("GREEN"), customName + " " + customHex);
        cb.Picker.Use(customName, customHex); Pump();
        Check("picker: custom colour used exactly", pm.PickedHex == customHex && (pm.ColorBrush as SolidColorBrush)?.Color.ToString().Replace("#FF", "#") == customHex,
              pm.PickedHex ?? "none");
        pm.ColorName = "MAROON";
        Check("picker: typing a name afterwards looks the name up again", pm.PickedHex == null && pm.CurrentHex == "#6D1A2B", pm.CurrentHex ?? "none");
        w.Close(); p.Detach();
    }

    private static void PrintPress(SettingsService s)
    {
        var type = s.OrderTypes.FirstOrDefault(t => t.Id == "printpress");
        if (type == null) { Fail("print & press: order type missing"); return; }
        var o = new OrderViewModel(type, s, 9500) { OrderName = "SELFTEST PRESS" };
        var live = Texts(Host(o));
        Check("print & press: neck / collar / sleeve / placket type / button rows removed",
              !live.Any(t => t.StartsWith("Neck Type") || t.StartsWith("Collar Color") || t.StartsWith("Sleeve Type") || t.StartsWith("Button")), string.Join(" | ", live.Take(40)));
        Check("print & press: Collar Patch and Placket rows while editing", live.Contains("Collar Patch & Color:") && live.Contains("Placket & Color:"), "");
        o.HasPlacket = true; o.PlacketColor = "NAVY";
        o.AdultShort.First(e => e.Label == "M").Quantity = 3;
        o.SubmitCommand.Execute(null); Pump(); Pump();
        var printed = Texts(Render(o));
        Check("print & press: unticked Collar Patch not on the submitted sheet", !printed.Contains("Collar Patch & Color:"), "");
        Check("print & press: ticked Placket with its colour on the sheet", printed.Contains("Placket & Color:") && printed.Contains("NAVY"), string.Join(" | ", printed.Take(40)));
        o.EditCommand.Execute(null); o.HasCollarPatch = true; o.CollarPatchColor = "RED"; o.SubmitCommand.Execute(null); Pump(); Pump();
        printed = Texts(Render(o));
        Check("print & press: ticked Collar Patch appears with its colour", printed.Contains("Collar Patch & Color:") && printed.Contains("RED"), "");
        Check("submit: the save location is not shown", !(o.StatusText ?? "").Contains("Documents") && !(o.StatusText ?? "").Contains("\\"), o.StatusText ?? "");
        o.Detach();
    }

    private static void Customers(SettingsService s)
    {
        Check("customers: the 29 customers are suggested", new[] { "Urapola Dinesh", "Benet", "A&M", "Cap Rashmi Son", "Rajarata Clothing Ziyan" }
              .All(n => s.Customers.Contains(n)), s.Customers.Count.ToString());
        var m = NameSuggest.Match(s.Customers, "rash");
        Check("customers: partial match 'rash'", m.SequenceEqual(new[] { "Cap Rashmi", "Cap Rashmi Son" }), string.Join(", ", m));
        m = NameSuggest.Match(s.Customers, "DIN");
        Check("customers: case-insensitive 'DIN' finds Urapola Dinesh", m.Contains("Urapola Dinesh"), string.Join(", ", m));
        m = NameSuggest.Match(s.Customers, "ap");
        Check("customers: best matches first ('ap')", m.Count >= 3 && m.Take(2).OrderBy(x => x).SequenceEqual(new[] { "Linen Apparel", "Priyan Apparel" }), string.Join(", ", m));
        m = NameSuggest.Match(new[] { "Batti", "batti ", "BATTI", "Shiff" }, "");
        Check("customers: no duplicate suggestions", m.SequenceEqual(new[] { "Batti", "Shiff" }), string.Join(", ", m));

        var o = NewOrder(s, SheetLayout.Tops);
        o.Customer = "  New Test Customer 28  ";
        o.SubmitCommand.Execute(null); Pump(); Pump();
        Check("customers: a new name can be typed and is saved on the order", o.Customer == "New Test Customer 28", o.Customer);
        Check("customers: a new name is suggested next time", s.Customers.Count(c => c == "New Test Customer 28") == 1, "");
        o.EditCommand.Execute(null); o.SubmitCommand.Execute(null); Pump(); Pump();
        Check("customers: submitting again adds no duplicate", s.Customers.Count(c => c.Equals("New Test Customer 28", StringComparison.OrdinalIgnoreCase)) == 1, "");
        var printed = Texts(Render(o));
        Check("customers: customer printed on the sheet", printed.Contains("New Test Customer 28"), "");
        s.Customers.Remove("New Test Customer 28"); s.Save();
        o.Detach();
    }

    private static void CustomerBoxUi(SettingsService s)
    {
        var o = NewOrder(s, SheetLayout.Tops);
        var w = new Window { Width = 1100, Height = 820, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -3000, Top = -3000,
                             ShowActivated = true, Content = new Border { Width = 840, Height = 592, Child = new OrderSheetView { DataContext = o } } };
        w.Show(); Pump();
        var box = FindAll<CustomerBox>(w).FirstOrDefault();
        if (box == null) { Fail("customer box: not found on the sheet"); w.Close(); o.Detach(); return; }
        var tb = Find<TextBox>(box, "Box")!;
        w.Activate(); tb.Focus(); Pump();
        Check("customer box: clicking the field lists the customers", box.IsDropDownOpen && box.Suggestions.Count >= 29, $"{box.IsDropDownOpen} {box.Suggestions.Count}");
        Pump();
        var bar = box.DropPopup.Child == null ? null : FindAll<System.Windows.Controls.Primitives.ScrollBar>(box.DropPopup.Child)
                      .FirstOrDefault(b => b.Orientation == Orientation.Vertical && b.Visibility == Visibility.Visible);
        Check("customer box: slim scroll bar", bar != null && bar.ActualWidth <= 8, bar == null ? "no scroll bar" : bar.ActualWidth.ToString("0.0"));
        tb.Text = "kav"; Pump();
        Check("customer box: typing filters ('kav')", box.Suggestions.SequenceEqual(new[] { "Kavee Creations" }), string.Join(", ", box.Suggestions));
        tb.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(tb), 0, System.Windows.Input.Key.Down)
                      { RoutedEvent = UIElement.PreviewKeyDownEvent });
        tb.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(tb), 0, System.Windows.Input.Key.Enter)
                      { RoutedEvent = UIElement.PreviewKeyDownEvent });
        Pump();
        Check("customer box: keyboard Down + Enter fills the field", o.Customer == "Kavee Creations" && !box.IsDropDownOpen, o.Customer);
        tb.Text = "Someone Completely New"; Pump();
        Check("customer box: any other name can be typed", o.Customer == "Someone Completely New" && !box.IsDropDownOpen, o.Customer);
        box.Accept("Shiff"); Pump();
        Check("customer box: picking a suggestion fills the field", o.Customer == "Shiff", o.Customer);
        w.Close(); o.Detach();
    }

    // ------------------------------------------------------------------ helpers
    private static OrderViewModel NewOrder(SettingsService s, SheetLayout layout)
    {
        var type = s.OrderTypes.FirstOrDefault(t => t.Layout == layout) ?? throw new InvalidOperationException("no order type with layout " + layout);
        return new OrderViewModel(type, s, 9000 + _passed + _failed) { OrderName = "SELFTEST " + layout };
    }

    /// <summary>The sheet as the main window shows it (laid out, bindings live).</summary>
    private static FrameworkElement Host(OrderViewModel o)
    {
        var sheet = new OrderSheetView { DataContext = o };
        var root = new Border { Width = 840, Height = o.SheetHeight, Child = sheet };
        Layout(root);
        return root;
    }

    /// <summary>The sheet as SheetOutput draws it for Print / PDF (locked output state).</summary>
    private static FrameworkElement Render(OrderViewModel o) => o.RenderAs(false, () =>
    {
        var sheet = new OrderSheetView { DataContext = o };
        OutputMode.SetIsPrint(sheet, true);
        var root = new Border { Width = 840, Height = o.SheetHeight, Child = sheet };
        Layout(root);
        return (FrameworkElement)root;
    });

    private static void Layout(FrameworkElement root)
    {
        for (int i = 0; i < 2; i++)
        {
            root.Measure(new Size(root.Width, root.Height));
            root.Arrange(new Rect(0, 0, root.Width, root.Height));
            root.UpdateLayout();
            Pump();
        }
    }

    private static void Close(FrameworkElement live, OrderViewModel o) { if (live is Border b) b.Child = null; o.Detach(); }

    private static void Pump()
    {
        Log.AppendLine($"      . {Clock.Elapsed.TotalSeconds:0.0} s"); Flush();
        var d = Dispatcher.CurrentDispatcher;
        d.Invoke(() => { }, DispatcherPriority.ContextIdle);
        d.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static bool IsShown(UIElement? e)
    {
        if (e == null) return false;
        for (DependencyObject? d = e; d != null; d = VisualTreeHelper.GetParent(d))
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
        return true;
    }

    /// <summary>Every visible text on the sheet (text blocks, text boxes, drop-down selections).</summary>
    private static List<string> Texts(DependencyObject root)
    {
        var list = new List<string>();
        foreach (var fe in FindAll<FrameworkElement>(root))
        {
            if (!IsShown(fe)) continue;
            switch (fe)
            {
                case TextBlock t:
                    var text = t.Inlines.Count > 0 ? string.Concat(t.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text)) : t.Text;
                    if (string.IsNullOrWhiteSpace(text)) text = t.Text;
                    if (!string.IsNullOrWhiteSpace(text)) list.Add(text.Trim());
                    break;
                case TextBox b when !string.IsNullOrWhiteSpace(b.Text): list.Add(b.Text.Trim()); break;
            }
        }
        return list;
    }

    private static string ComboText(DependencyObject root, string name) => Find<ComboBox>(root, name)?.SelectedItem as string ?? "";

    private static T? Find<T>(DependencyObject root, string name) where T : FrameworkElement =>
        FindAll<T>(root).FirstOrDefault(x => x.Name == name);

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        var stack = new Stack<DependencyObject>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is T t) yield return t;
            int n = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
            for (int i = n - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
    }

    private static void Check(string what, bool ok, string detail)
    {
        if (ok) { _passed++; Log.AppendLine("PASS  " + what); }
        else { _failed++; Log.AppendLine("FAIL  " + what + (detail.Length > 0 ? "   [" + detail + "]" : "")); }
        Flush();
    }

    private static void Fail(string what) { _failed++; Log.AppendLine("FAIL  " + what); }
}
