using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    public static int Run(string resultFile)
    {
        try
        {
            var s = SettingsService.Instance;
            Coordinator(s, SheetLayout.Tops);
            Coordinator(s, SheetLayout.Jacket);
            PrintPdfVisibility(s, SheetLayout.Tops);
            PrintPdfVisibility(s, SheetLayout.Jacket);
            CategoryAndSleeve(s);
            EmptyColumns(s, SheetLayout.Tops);
            EmptyColumns(s, SheetLayout.Jacket);
            BottomsStrip(s);
            Customers(s);
            CustomerBoxUi(s);
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
        Check("category/sleeve: printed on the submitted sheet", printed.Contains("Gents") && printed.Contains("Lads") && printed.Contains("L/S"),
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
        o.BottomSizes.First(e => e.Label == "M").Quantity = 10;
        o.BottomSizes.First(e => e.Label == "L").Quantity = 6;
        Check("bottoms: full-width strip while editing", !o.BottomStripCompact && double.IsNaN(o.BottomStripWidth), "");
        o.SubmitCommand.Execute(null); Pump(); Pump();
        Check("bottoms: only filled sizes, next to the total", o.BottomStripCompact && o.BottomStripWidth == 128 &&
              o.BottomStripView.Cast<object>().Count() == 2 && o.TotalQuantity == 16, $"{o.BottomStripWidth} / {o.TotalQuantity}");
        var printed = Texts(Render(o));
        Check("bottoms: printed sizes and total", printed.Contains("M") && printed.Contains("L") && printed.Contains("16") && !printed.Contains("XS"),
              string.Join(" | ", printed.Take(50)));
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
    }

    private static void Fail(string what) { _failed++; Log.AppendLine("FAIL  " + what); }
}
