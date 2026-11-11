using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using NexOrderDesk.Core;
using NexOrderDesk.Data;
using NexOrderDesk.Models;
using NexOrderDesk.Services;
namespace NexOrderDesk.ViewModels;

/// <summary>All editable data of one open order sheet (one tab).</summary>
public class OrderViewModel : ObservableObject
{
    private readonly SettingsService _s;
    private bool _busy; private int _seq;
    private string _orderNumber, _orderName = "", _customer = "", _coordinator = "", _notes = "",
        _neck, _sleeve, _collarColor = "", _pipingType, _collarSizeLabel = "23 x 6",
        _bottomType, _waist, _leg;
    private DateTime _delivery = DateTime.Today;
    private ColorOption? _pipingColor;
    private bool _hasPiping;
    private string _backPaper = "A5";
    private bool _isEditable = true, _includeExtra;
    private string _accentHex; private bool _customAccent; private Brush _accentBrush;
    private string _flagUnit = "ft", _status = "";
    private int _statusSeq;

    public OrderViewModel(OrderTypeDef type, SettingsService settings, int suggestedNumber)
    {
        _s = settings; Type = type; Layout = type.Layout; _orderNumber = suggestedNumber.ToString();
        _accentHex = type.AccentHex; _accentBrush = ColorMath.Brush(_accentHex);
        Type.PropertyChanged += OnTypeChanged;

        // ---- tops defaults ----
        _neck = Pick(_s.NeckTypes, "Full Collar");
        _sleeve = Pick(_s.SleeveTypes, "Hem Sleeve");
        _pipingType = Pick(_s.PipingTypes, "");
        _placketType = Pick(_s.PlacketTypes, DefaultOptions.DefaultPlacket);
        _buttonCount = Pick(_s.ButtonCounts, DefaultOptions.DefaultButtons);
        // ---- jacket defaults ----
        _jacketType = Pick(_s.JacketTypes, "Track Jacket");
        _jacketCollar = Pick(_s.JacketCollars, "Stand-Up Collar");
        _jacketSleeve = Pick(_s.JacketSleeves, "Long Sleeve — Ribbed Cuff");
        _jacketClosure = Pick(_s.JacketClosures, "Full Zip");
        // ---- bottoms defaults ----
        _bottomType = Pick(_s.BottomTypes, DefaultOptions.DefaultBottomType);
        _waist = Pick(_s.WaistTypes, DefaultOptions.DefaultWaist);
        _leg = Pick(_s.LegOpenings, DefaultOptions.DefaultLeg);

        MaterialColors = new ObservableCollection<MaterialColorEntry> { NewMaterial(true) };
        AddMaterialCommand = new RelayCommand(_ => MaterialColors.Add(NewMaterial(false)), _ => IsEditable && MaterialColors.Count < (IsFlags ? 4 : 3));
        RemoveMaterialCommand = new RelayCommand(p => { if (p is MaterialColorEntry e && !e.IsPrimary) MaterialColors.Remove(e); });

        PrintMethods = new ObservableCollection<PrintMethodOption>();
        PrintMethodsView = CollectionViewSource.GetDefaultView(PrintMethods);
        PrintMethodsView.Filter = o => IsEditable || (o is PrintMethodOption m && m.IsSelected);
        RebuildPrintMethods(true);
        _s.PrintTypes.CollectionChanged += (_, _) => RebuildPrintMethods(false);
        _s.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(SettingsService.LastOrderNumber)) OnPropertyChanged(nameof(LastOrderText)); };
        // the coordinator list reloaded (Import options / list edited): show the saved coordinator again in the drop-down
        _s.Coordinators.CollectionChanged += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(
            new Action(() => OnPropertyChanged(nameof(Coordinator))), System.Windows.Threading.DispatcherPriority.Background);

        // ---- bottoms tick-box lists ----
        Pockets = new ObservableCollection<CheckOption>();
        Extras = new ObservableCollection<CheckOption>();
        ExtrasView = new ListCollectionView(Extras) { Filter = o => IsEditable || (o is CheckOption c && c.IsSelected) };
        RebuildChecks(Pockets, _s.PocketTypes, new[] { DefaultOptions.DefaultPocket }, OnPocketChanged, true);
        RebuildChecks(Extras, _s.BottomExtras, Array.Empty<string>(), OnExtraChanged, true);
        _s.PocketTypes.CollectionChanged += (_, _) => RebuildChecks(Pockets, _s.PocketTypes, null, OnPocketChanged, false);
        _s.BottomExtras.CollectionChanged += (_, _) => RebuildChecks(Extras, _s.BottomExtras, null, OnExtraChanged, false);
        JacketPockets = new ObservableCollection<CheckOption>();
        JacketExtras = new ObservableCollection<CheckOption>();
        JacketExtrasView = new ListCollectionView(JacketExtras) { Filter = o => IsEditable || (o is CheckOption c && c.IsSelected) };
        RebuildChecks(JacketPockets, _s.JacketPockets, new[] { "Side Pockets" }, OnJacketPocketChanged, true);
        RebuildChecks(JacketExtras, _s.JacketExtras, Array.Empty<string>(), OnJacketExtraChanged, true);
        _s.JacketPockets.CollectionChanged += (_, _) => RebuildChecks(JacketPockets, _s.JacketPockets, null, OnJacketPocketChanged, false);
        _s.JacketExtras.CollectionChanged += (_, _) => RebuildChecks(JacketExtras, _s.JacketExtras, null, OnJacketExtraChanged, false);

        // ---- sizes ----
        AdultShort = new(DefaultOptions.AdultSizes.Select(s => new SizeEntry(s, "S/S")));
        AdultLong = new(DefaultOptions.AdultSizes.Select(s => new SizeEntry(s, "L/S")));
        // jackets: the Kids column is optional (like PS) - hidden until "+ Kids" is switched on
        Kids = new(DefaultOptions.KidsSizes.Select(s => new SizeEntry(s, IsBottoms ? "" : "S/S", Layout is SheetLayout.Jacket or SheetLayout.Bottoms)));
        Preschool = new(DefaultOptions.PreschoolSizes.Select(s => new SizeEntry(s, IsBottoms ? "" : "S/S", true)));
        BottomSizes = new(DefaultOptions.BottomSizes.Select(s => new SizeEntry(s, "")));
        OptionalSizes = new(Enumerable.Range(0, DefaultOptions.OptionalRowsStart).Select(_ => new SizeEntry("", "", true, true)));
        foreach (var e in AllSizes) Watch(e);
        OptionalSizes.CollectionChanged += (_, a) =>
        {
            if (a.NewItems != null) foreach (SizeEntry e in a.NewItems) Watch(e);
            OnPropertyChanged(nameof(TotalQuantity)); OnPropertyChanged(nameof(SizeRowCount));
        };

        AdultShortView = MakeSizeView(AdultShort); AdultLongView = MakeSizeView(AdultLong); KidsView = MakeSizeView(Kids);
        PreschoolView = MakeSizeView(Preschool); BottomSizesView = MakeSizeView(BottomSizes); OptionalSizesView = MakeSizeView(OptionalSizes);
        AddOptionalSizeCommand = new RelayCommand(_ =>
        {
            if (OptionalSizes.Count < DefaultOptions.OptionalRowsMax) OptionalSizes.Add(new SizeEntry("", "", true, true) { IsActive = true });
        }, _ => IsEditable && OptionalSizes.Count < DefaultOptions.OptionalRowsMax);

        // bottoms: one horizontal strip XS..4XL (+ optional sizes), total beside it
        BottomStrip = new ObservableCollection<SizeEntry>(BottomSizes.Concat(OptionalSizes));
        OptionalSizes.CollectionChanged += (_, a) => { if (a.NewItems != null) foreach (SizeEntry e in a.NewItems) BottomStrip.Add(e); };
        BottomStripView = MakeSizeView(BottomStrip);
        // 2.9: Adults line (+ other sizes), then Kids and PS lines when switched on
        BottomLines = new ObservableCollection<StripLine>
        {
            new("ADULTS", BottomStrip, () => IsEditable, () => true),
            new("KIDS", Kids, () => IsEditable, () => _includeKids),
            new("PS", Preschool, () => IsEditable, () => _includeBottomPs),
        };

        // flags
        FlagSizes = new(Enumerable.Range(0, DefaultOptions.FlagRowsStart).Select(_ => NewFlagRow()));
        FlagSizes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(FlagTotal));
        FlagSizesView = new ListCollectionView(FlagSizes) { Filter = o => IsEditable || (o is FlagSizeEntry f && f.HasContent) };
        AddFlagSizeCommand = new RelayCommand(_ => FlagSizes.Add(NewFlagRow()), _ => IsEditable && FlagSizes.Count < DefaultOptions.FlagRowsMax);
        RemoveFlagSizeCommand = new RelayCommand(p => { if (p is FlagSizeEntry f && FlagSizes.Count > 1) FlagSizes.Remove(f); }, _ => IsEditable);
        SetUnitCommand = new RelayCommand(p => { if (p is string u) FlagUnit = u; }, _ => IsEditable);

        // adult columns: category (Adults / Unisex / Gents / Lads) and sleeve (S/S / L/S) are chosen in their headers
        AdultShortColumn = SizeColumn.Adult("Adults", "S/S", AdultShortView);
        AdultLongColumn = SizeColumn.Adult("Adults", "L/S", AdultLongView);
        SizeColumns = IsBottoms
            ? new ObservableCollection<SizeColumn> { new("Size Breakdown", BottomSizesView) }
            : IsJacket
                ? new ObservableCollection<SizeColumn> { AdultShortColumn, AdultLongColumn }
                : new ObservableCollection<SizeColumn> { AdultShortColumn, AdultLongColumn, new("Kids (S/S)", KidsView) };
        // submitted sheet / print / PDF: size columns without any quantity are left out; the others share the width
        SizeColumnsView = new ListCollectionView(SizeColumns) { Filter = o => IsEditable || o is not SizeColumn c || c.HasData || !SizeColumns.Any(x => x.HasData) };

        FlipCommand = new RelayCommand(_ => IsBackSide = !IsBackSide);
        // Print / PDF only for submitted orders (the buttons are hidden while the order is being created or edited)
        PrintCommand = new RelayCommand(_ => { if (CanOutput()) Views.PrintPreviewWindow.Open(this, false); }, _ => !IsEditable);
        PdfCommand = new RelayCommand(_ => { if (CanOutput()) Views.PrintPreviewWindow.Open(this, true); }, _ => !IsEditable);
        OpenDocumentsCommand = new RelayCommand(_ => SheetOutput.OpenFolder(LastPdfPath));

        SubmitCommand = new RelayCommand(_ => Submit(), _ => IsEditable);
        EditCommand = new RelayCommand(_ => IsEditable = true, _ => !IsEditable);
    }

    private bool CanOutput()
    {
        if (!IsEditable) return true;
        ShowStatus("Submit the order first - Print and PDF are available for submitted orders", 5);
        return false;
    }

    private static string Pick(IList<string> list, string preferred) =>
        list.FirstOrDefault(n => n.Equals(preferred, StringComparison.OrdinalIgnoreCase)) ?? list.FirstOrDefault() ?? "";

    private void Watch(SizeEntry e) => e.PropertyChanged += (_, a) =>
    {
        if (a.PropertyName is nameof(SizeEntry.Quantity) or nameof(SizeEntry.IsActive)) OnPropertyChanged(nameof(TotalQuantity));
    };

    public SettingsService Settings => _s;

    // ---- order type / theme colour ----
    public OrderTypeDef Type { get; }
    /// <summary>Fixed when the order is opened (changing a type's layout later does not rebuild open sheets).</summary>
    public SheetLayout Layout { get; }
    public bool IsBottoms => Layout == SheetLayout.Bottoms;
    public bool IsTops => Layout == SheetLayout.Tops;
    public bool IsJacket => Layout == SheetLayout.Jacket;
    /// <summary>T-shirt style or jacket: vertical size table (Adults / Kids / PS).</summary>
    public bool IsTopLike => IsTops || IsJacket;
    public bool IsFlags => Layout == SheetLayout.Flags;
    public bool IsDtf => Layout == SheetLayout.Dtf;
    /// <summary>Garment sheets (tops / bottoms) have notes, sizes, print methods and the two image cards.</summary>
    public bool IsGarment => IsTops || IsBottoms || IsJacket;
    /// <summary>Bottoms / shorts / flags mostly use coloured material: bigger, easier colour picker.</summary>
    public bool ProminentColor => IsBottoms || IsFlags;
    public string SheetTitle => Type.SheetTitle;
    public Geometry Icon => Type.Icon;
    /// <summary>Uploaded icon of the order type (null = built-in icon).</summary>
    public ImageSource? IconImage => Type.IconImage;
    public bool HasIconImage => Type.HasIconImage;
    public bool NoIconImage => Type.NoIconImage;
    public Brush AccentBrush => _accentBrush;
    /// <summary>Theme colour of THIS order. Starts as the type colour and follows it, until changed for this order only.</summary>
    public string AccentHex
    {
        get => _accentHex;
        private set
        {
            var hex = ColorMath.NormalizeHex(value); if (hex == null) return;
            if (!Set(ref _accentHex, hex)) return;
            _accentBrush = ColorMath.Brush(hex);
            OnPropertyChanged(nameof(AccentBrush));
        }
    }
    public bool HasCustomAccent => _customAccent;
    public void SetAccent(string hex) { if (ColorMath.NormalizeHex(hex) == null) return; _customAccent = true; AccentHex = hex; OnPropertyChanged(nameof(HasCustomAccent)); }
    public void ResetAccent() { _customAccent = false; AccentHex = Type.AccentHex; OnPropertyChanged(nameof(HasCustomAccent)); }
    /// <summary>Makes this order's colour the default colour of its order type (all new orders of this type).</summary>
    public void SaveAccentAsTypeDefault() { Type.AccentHex = AccentHex; _customAccent = false; OnPropertyChanged(nameof(HasCustomAccent)); _s.Save(); }

    private void OnTypeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrderTypeDef.AccentHex) && !_customAccent) AccentHex = Type.AccentHex;
        if (e.PropertyName == nameof(OrderTypeDef.SheetTitle)) OnPropertyChanged(nameof(SheetTitle));
        if (e.PropertyName == nameof(OrderTypeDef.IconKey)) OnPropertyChanged(nameof(Icon));
        if (e.PropertyName == nameof(OrderTypeDef.IconFile))
        { OnPropertyChanged(nameof(IconImage)); OnPropertyChanged(nameof(HasIconImage)); OnPropertyChanged(nameof(NoIconImage)); }
    }

    /// <summary>Called when the tab is closed, so the order type no longer holds on to it.</summary>
    public void Detach() => Type.PropertyChanged -= OnTypeChanged;

    // ---- order info ----
    public string OrderNumber { get => _orderNumber; set => Set(ref _orderNumber, value ?? ""); }
    public string LastOrderText => _s.LastOrderNumber > 0 ? $"Last order: {_s.LastOrderNumber}" : "Last order: –";
    public DateTime DeliveryDate
    {
        get => _delivery;
        set { if (Set(ref _delivery, value.Date)) { OnPropertyChanged(nameof(DeliveryText)); OnPropertyChanged(nameof(DeliveryDayText)); } }
    }
    public string DeliveryText => _delivery.ToString("dd-MMM-yy");
    public string DeliveryDayText => _delivery.ToString("dddd");
    public string OrderName { get => _orderName; set => Set(ref _orderName, value ?? ""); }
    public string Customer
    {
        get => _customer;
        set { if (Set(ref _customer, value ?? "")) OnPropertyChanged(nameof(ShowCollarLabel)); }
    }
    /// <summary>
    /// Selected coordinator. A drop-down sends "nothing" (null) when its list is not ready yet - this happened every time the
    /// sheet was drawn for Submit / Print / PDF, and it erased the coordinator. Null is now ignored (like every other
    /// drop-down on the sheet); the value only changes when a coordinator is actually picked.
    /// </summary>
    public string Coordinator { get => _coordinator; set { if (value != null) Set(ref _coordinator, value); } }
    public string Notes { get => _notes; set => Set(ref _notes, value ?? ""); }

    /// <summary>Pictures in the front-sheet image cards (movable / resizable, several per card).</summary>
    public CanvasDoc FrontImages { get; } = new();
    public CanvasDoc BackImages { get; } = new();

    // ---- material / colour ----
    public ObservableCollection<MaterialColorEntry> MaterialColors { get; }
    public ICommand AddMaterialCommand { get; }
    public ICommand RemoveMaterialCommand { get; }
    /// <summary>Material choices for this sheet: flag materials on FLAGS, garment materials otherwise.</summary>
    public ObservableCollection<string> MaterialOptions => IsFlags ? _s.FlagMaterials : _s.Materials;
    public string MaterialManageKey => IsFlags ? "flagmaterials" : "materials";
    private MaterialColorEntry NewMaterial(bool primary) => new(
        IsFlags ? Pick(_s.FlagMaterials, "Satin") : Pick(_s.Materials, "Wetlook"),
        _s.Colors.FirstOrDefault(c => c.Name.Equals("WHITE", StringComparison.OrdinalIgnoreCase)) ?? _s.Colors.FirstOrDefault(), primary);

    // ---- garment specs (tops) ----
    public string NeckType { get => _neck; set { if (value != null) Set(ref _neck, value); } }
    public string CollarColor { get => _collarColor; set => Set(ref _collarColor, value ?? ""); }
    public string SleeveType { get => _sleeve; set { if (value != null) Set(ref _sleeve, value); } }
    // Placket Type & Color / Button & Button Color: free text, starting as "T-shirts match" (replaced when the user types,
    // can be edited or cleared; what is in the box is exactly what prints).
    private string _placketText = DefaultOptions.MatchText, _buttonText = DefaultOptions.MatchText;
    public string PlacketText
    {
        get => _placketText;
        set { if (Set(ref _placketText, value ?? "")) OnPropertyChanged(nameof(PlacketIsDefault)); }
    }
    public bool PlacketIsDefault => _placketText.Trim().Length == 0;
    public string ButtonText
    {
        get => _buttonText;
        set { if (Set(ref _buttonText, value ?? "")) OnPropertyChanged(nameof(ButtonIsDefault)); }
    }
    public bool ButtonIsDefault => _buttonText.Trim().Length == 0;
    public string MatchText => DefaultOptions.MatchText;
    // middle drop-downs: placket type (default 3 Pcs Placket) and button count (default 2) - both lists add/editable
    private string _placketType, _buttonCount;
    public string PlacketType { get => _placketType; set { if (value != null) Set(ref _placketType, value); } }
    public string ButtonCount { get => _buttonCount; set { if (value != null) Set(ref _buttonCount, value); } }
    // Piping is optional: off by default, then type + colour are not needed
    public bool HasPiping
    {
        get => _hasPiping;
        set { if (Set(ref _hasPiping, value)) { OnPropertyChanged(nameof(NoPiping)); OnPropertyChanged(nameof(ShowPipingHint)); } }
    }
    public bool NoPiping => !_hasPiping;

    // ---------------- Print & Press (2.9): short spec list - Collar Patch, Placket, Piping
    /// <summary>Print and Press orders show only Collar Patch, Placket and Piping (no neck / collar / sleeve / button rows).</summary>
    public bool IsPrintPress => IsTops && Type.Id == "printpress";
    public bool IsStandardTops => !IsPrintPress;
    private bool _hasCollarPatch, _hasPlacket;
    private string _collarPatchColor = "", _placketColor = "";
    /// <summary>Ticked = the order has a collar patch; only ticked rows appear on the submitted sheet.</summary>
    public bool HasCollarPatch { get => _hasCollarPatch; set { if (Set(ref _hasCollarPatch, value)) OnPropertyChanged(nameof(ShowCollarPatchHint)); } }
    public string CollarPatchColor { get => _collarPatchColor; set { if (Set(ref _collarPatchColor, value ?? "")) OnPropertyChanged(nameof(ShowCollarPatchHint)); } }
    public bool ShowCollarPatchHint => _hasCollarPatch && _collarPatchColor.Length == 0;
    public bool HasPlacket { get => _hasPlacket; set { if (Set(ref _hasPlacket, value)) OnPropertyChanged(nameof(ShowPlacketHint)); } }
    public string PlacketColor { get => _placketColor; set { if (Set(ref _placketColor, value ?? "")) OnPropertyChanged(nameof(ShowPlacketHint)); } }
    public bool ShowPlacketHint => _hasPlacket && _placketColor.Length == 0;
    /// <summary>Piping details / colour, typed freely (only used when piping is ticked).</summary>
    private string _pipingText = "";
    public string PipingText
    {
        get => _pipingText;
        set { if (Set(ref _pipingText, value ?? "")) OnPropertyChanged(nameof(ShowPipingHint)); }
    }
    public bool ShowPipingHint => _hasPiping && _pipingText.Length == 0;
    public string PipingType { get => _pipingType; set { if (value != null) Set(ref _pipingType, value); } }
    public ColorOption? PipingColor { get => _pipingColor; set => Set(ref _pipingColor, value); }

    // ---- back side paper size (A5 landscape = default, or A4 portrait) ----
    public string BackPaper
    {
        get => _backPaper;
        set
        {
            if (!Set(ref _backPaper, value == "A4" ? "A4" : "A5")) return;
            OnPropertyChanged(nameof(IsBackA4)); OnPropertyChanged(nameof(IsBackA5)); OnPropertyChanged(nameof(SheetHeight));
        }
    }
    public bool IsBackA4 { get => _backPaper == "A4"; set { if (value) BackPaper = "A4"; else OnPropertyChanged(nameof(IsBackA4)); } }
    public bool IsBackA5 { get => _backPaper == "A5"; set { if (value) BackPaper = "A5"; else OnPropertyChanged(nameof(IsBackA5)); } }
    /// <summary>Height of the sheet design canvas (width is always 840): A5 landscape 592, A4 portrait back side 1188.</summary>
    public double SheetHeight => _isBack && _backPaper == "A4" ? SheetOutput.SheetA4H : SheetOutput.SheetH;
    public bool ShowCollarLabel => IsTops && _customer.Trim().Equals("A&M", StringComparison.OrdinalIgnoreCase);
    public string CollarSizeLabel { get => _collarSizeLabel; set => Set(ref _collarSizeLabel, value ?? ""); }

    // ---- garment specs (bottoms) ----
    public string BottomType { get => _bottomType; set { if (value != null) Set(ref _bottomType, value); } }
    public string WaistType { get => _waist; set { if (value != null) Set(ref _waist, value); } }
    public string LegOpening { get => _leg; set { if (value != null) Set(ref _leg, value); } }
    /// <summary>Pocket types: several can be ticked (e.g. Side Pockets + Back Pockets).</summary>
    public ObservableCollection<CheckOption> Pockets { get; }
    public string PocketSummary
    {
        get
        {
            var sel = Pockets.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            return sel.Count == 0 ? "—" : string.Join(" + ", sel);
        }
    }
    /// <summary>Additional custom features. While editing all show; after Submit only the ticked ones.</summary>
    public ObservableCollection<CheckOption> Extras { get; }
    public ICollectionView ExtrasView { get; }
    public bool HasExtras => Extras.Any(x => x.IsSelected);
    public bool ShowNoExtras => !IsEditable && !HasExtras;

    private void RebuildChecks(ObservableCollection<CheckOption> target, IEnumerable<string> names, IEnumerable<string>? initial,
                               PropertyChangedEventHandler handler, bool first)
    {
        var keep = first ? new HashSet<string>(initial ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                         : target.Where(x => x.IsSelected).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var x in target) x.PropertyChanged -= handler;
        target.Clear();
        foreach (var n in names)
        {
            var c = new CheckOption(n, keep.Contains(n));
            c.PropertyChanged += handler;
            target.Add(c);
        }
        OnPropertyChanged(nameof(PocketSummary)); OnPropertyChanged(nameof(HasExtras)); OnPropertyChanged(nameof(ShowNoExtras));
        OnPropertyChanged(nameof(JacketPocketSummary)); OnPropertyChanged(nameof(HasJacketExtras)); OnPropertyChanged(nameof(ShowNoJacketExtras));
    }

    // ---- garment specs (jackets) ----
    private string _jacketType, _jacketCollar, _jacketSleeve, _jacketClosure;
    public string JacketType { get => _jacketType; set { if (value != null) Set(ref _jacketType, value); } }
    public string JacketCollar
    {
        get => _jacketCollar;
        set { if (value != null && Set(ref _jacketCollar, value)) OnPropertyChanged(nameof(IsHooded)); }
    }
    /// <summary>"Hooded" collar is highlighted on the sheet so production never misses the hood.</summary>
    public bool IsHooded => _jacketCollar?.Contains("hood", StringComparison.OrdinalIgnoreCase) == true;
    public string JacketSleeve { get => _jacketSleeve; set { if (value != null) Set(ref _jacketSleeve, value); } }
    public string JacketClosure { get => _jacketClosure; set { if (value != null) Set(ref _jacketClosure, value); } }
    public ObservableCollection<CheckOption> JacketPockets { get; }
    public string JacketPocketSummary
    {
        get
        {
            var sel = JacketPockets.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            return sel.Count == 0 ? "—" : string.Join(" + ", sel);
        }
    }
    public ObservableCollection<CheckOption> JacketExtras { get; }
    public ICollectionView JacketExtrasView { get; }
    public bool HasJacketExtras => JacketExtras.Any(x => x.IsSelected);
    public bool ShowNoJacketExtras => !IsEditable && !HasJacketExtras;

    private static bool IsNone(CheckOption c) => c.Name.StartsWith("None", StringComparison.OrdinalIgnoreCase) || c.Name.StartsWith("No pocket", StringComparison.OrdinalIgnoreCase);
    private void OnJacketPocketChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CheckOption.IsSelected) || _busy || sender is not CheckOption o) return;
        if (o.IsSelected)
        {
            _busy = true;
            foreach (var q in JacketPockets.Where(q => q != o && q.IsSelected && (IsNone(o) || IsNone(q))).ToList()) q.IsSelected = false;
            _busy = false;
        }
        OnPropertyChanged(nameof(JacketPocketSummary));
    }
    private void OnJacketExtraChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CheckOption.IsSelected)) return;
        OnPropertyChanged(nameof(HasJacketExtras)); OnPropertyChanged(nameof(ShowNoJacketExtras));
    }

    private static bool IsNoPockets(CheckOption c) => c.Name.StartsWith("No pocket", StringComparison.OrdinalIgnoreCase);

    private void OnPocketChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CheckOption.IsSelected) || _busy || sender is not CheckOption o) return;
        if (o.IsSelected)
        {
            // "No Pockets" excludes every other pocket type, and the other way round.
            _busy = true;
            var clash = Pockets.Where(q => q != o && q.IsSelected && (IsNoPockets(o) || IsNoPockets(q))).ToList();
            foreach (var q in clash) q.IsSelected = false;
            _busy = false;
        }
        OnPropertyChanged(nameof(PocketSummary));
    }

    private void OnExtraChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CheckOption.IsSelected)) return;
        OnPropertyChanged(nameof(HasExtras)); OnPropertyChanged(nameof(ShowNoExtras));
    }

    // ---- printing methods (max 2) ----
    public ObservableCollection<PrintMethodOption> PrintMethods { get; }
    public ICollectionView PrintMethodsView { get; }
    public string PrintSummary
    {
        get
        {
            var sel = PrintMethods.Where(m => m.IsSelected).Select(m => m.BannerName).ToList();
            return sel.Count == 0 ? "SELECT PRINTING METHOD" : string.Join("  +  ", sel);
        }
    }

    private void RebuildPrintMethods(bool initial)
    {
        var keep = PrintMethods.Where(m => m.IsSelected).Select(m => m.Name).ToHashSet();
        foreach (var m in PrintMethods) m.PropertyChanged -= OnPrintMethodChanged;
        PrintMethods.Clear();
        foreach (var name in _s.PrintTypes)
        {
            var o = new PrintMethodOption(name);
            o.PropertyChanged += OnPrintMethodChanged;
            PrintMethods.Add(o);
            if (initial ? name.Equals("SUBLIMATION (full)", StringComparison.OrdinalIgnoreCase) : keep.Contains(name)) o.IsSelected = true;
        }
        if (initial && !PrintMethods.Any(m => m.IsSelected) && PrintMethods.Count > 0) PrintMethods[0].IsSelected = true;
        OnPropertyChanged(nameof(PrintSummary));
    }

    private void OnPrintMethodChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PrintMethodOption.IsSelected) || _busy || sender is not PrintMethodOption o) return;
        if (o.IsSelected)
        {
            o.SelectionOrder = ++_seq;
            var sel = PrintMethods.Where(m => m.IsSelected).ToList();
            if (sel.Count > 2)
            {
                _busy = true;
                sel.Where(m => m != o).OrderBy(m => m.SelectionOrder).First().IsSelected = false;   // drop the oldest pick
                _busy = false;
            }
        }
        OnPropertyChanged(nameof(PrintSummary));
    }

    // ---- sizes ----
    public ObservableCollection<SizeEntry> AdultShort { get; }
    public ObservableCollection<SizeEntry> AdultLong { get; }
    public ObservableCollection<SizeEntry> Kids { get; }
    public ObservableCollection<SizeEntry> Preschool { get; }
    public ObservableCollection<SizeEntry> BottomSizes { get; }
    public ObservableCollection<SizeEntry> OptionalSizes { get; }
    public ICollectionView AdultShortView { get; }
    public ICollectionView AdultLongView { get; }
    public ICollectionView KidsView { get; }
    public ICollectionView PreschoolView { get; }
    public ICollectionView BottomSizesView { get; }
    public ICollectionView OptionalSizesView { get; }
    public ICommand AddOptionalSizeCommand { get; }
    public SizeColumn AdultShortColumn { get; }
    public SizeColumn AdultLongColumn { get; }
    /// <summary>What the size table shows: every column while editing; on the submitted sheet only columns with quantities.</summary>
    public ICollectionView SizeColumnsView { get; }
    /// <summary>Columns shown in the size table. Tops: Adults S/S, Adults L/S, Kids S/S (+ PS). Bottoms: Size Breakdown (+ Optional sizes).</summary>
    public ObservableCollection<SizeColumn> SizeColumns { get; }

    private IEnumerable<SizeEntry> AllSizes => !IsGarment ? Enumerable.Empty<SizeEntry>() : IsBottoms
        ? BottomSizes.Concat(OptionalSizes).Concat(Kids).Concat(Preschool)
        : AdultShort.Concat(AdultLong).Concat(Kids).Concat(Preschool);

    private IEnumerable<ObservableCollection<SizeEntry>> SizeLists => !IsGarment ? Array.Empty<ObservableCollection<SizeEntry>>() : IsBottoms
        ? new[] { BottomSizes, OptionalSizes, Kids, Preschool }
        : new[] { AdultShort, AdultLong, Kids, Preschool };

    /// <summary>Rows each size column is divided into: full list while editing, tighter once submitted (only filled sizes show).</summary>
    public int SizeRowCount
    {
        get
        {
            if (IsEditable)
                return IsBottoms ? Math.Max(BottomSizes.Count, _includeExtra ? OptionalSizes.Count : 0) : 12;
            return Math.Max(6, SizeLists.Select(l => l.Count(x => x.IsActive && x.HasQuantity)).DefaultIfEmpty(0).Max());
        }
    }

    private ICollectionView MakeSizeView(ObservableCollection<SizeEntry> src)
    {
        // A separate view per list (not the shared default view), so several open orders never share filters.
        var v = new ListCollectionView(src);
        // Editing: show every row (keeps the columns aligned). Submitted: only sizes that have a quantity.
        v.Filter = o => IsEditable ? (o is SizeEntry e0 && (!e0.IsCustom || e0.IsActive)) : (o is SizeEntry e && e.IsActive && e.HasQuantity);
        return v;
    }

    /// <summary>Tops: "+ PS sizes" (pre-school column). Bottoms: "+ Other sizes" (optional sizes typed by the user).</summary>
    public bool IncludeExtraSizes
    {
        get => _includeExtra;
        set
        {
            if (!Set(ref _includeExtra, value)) return;
            var list = IsBottoms ? OptionalSizes : Preschool;
            var view = IsBottoms ? OptionalSizesView : PreschoolView;
            foreach (var e in list) { e.IsActive = value; if (!value) { e.Quantity = null; if (e.IsCustom) e.Label = ""; } }
            var col = SizeColumns.FirstOrDefault(c => c.View == view);
            if (value && col == null)
                SizeColumns.Add(IsBottoms ? new SizeColumn("Optional sizes", view, AddOptionalSizeCommand) : new SizeColumn("PS (S/S)", view));
            else if (!value && col != null) SizeColumns.Remove(col);
            view.Refresh(); BottomStripView.Refresh(); RefreshBottomLines();
            OnPropertyChanged(nameof(TotalQuantity)); OnPropertyChanged(nameof(SizeRowCount));
        }
    }
    public string ExtraSizesText => IsBottoms ? "+ Other sizes" : "+ PS sizes";

    private bool _includeKids;
    private bool _includeBottomPs;
    /// <summary>Bottoms / shorts: "+ PS" adds the pre-school sizes line (P4XS to PXL).</summary>
    public bool IncludeBottomPs
    {
        get => _includeBottomPs;
        set
        {
            if (!IsBottoms || !Set(ref _includeBottomPs, value)) return;
            foreach (var e in Preschool) { e.IsActive = value; if (!value) e.Quantity = null; }
            PreschoolView.Refresh(); RefreshBottomLines();
            OnPropertyChanged(nameof(TotalQuantity)); OnPropertyChanged(nameof(SizeRowCount));
        }
    }

    /// <summary>Bottoms / shorts size lines: Adults (+ other sizes), Kids, PS.</summary>
    public ObservableCollection<StripLine> BottomLines { get; }
    private void RefreshBottomLines() { foreach (var l in BottomLines) l.Refresh(); }

    /// <summary>Jackets: "+ Kids" adds the Kids (S/S) column. Bottoms: "+ Kids" adds the Kids sizes line.</summary>
    public bool IncludeKids
    {
        get => _includeKids;
        set
        {
            if (!(IsJacket || IsBottoms) || !Set(ref _includeKids, value)) return;
            foreach (var e in Kids) { e.IsActive = value; if (!value) e.Quantity = null; }
            if (IsJacket)
            {
                var col = SizeColumns.FirstOrDefault(c => c.View == KidsView);
                if (value && col == null) SizeColumns.Insert(Math.Min(2, SizeColumns.Count), new SizeColumn("Kids (S/S)", KidsView));
                else if (!value && col != null) SizeColumns.Remove(col);
            }
            KidsView.Refresh(); RefreshBottomLines();
            OnPropertyChanged(nameof(TotalQuantity)); OnPropertyChanged(nameof(SizeRowCount));
        }
    }
    public string ExtraSizesTip => IsBottoms
        ? "Add an 'Optional sizes' column: type any other size (5XL, 28, 30, kids…) and its quantity"
        : "Add the pre-school column PS (S/S): P4XS to PXL";

    public int TotalQuantity => AllSizes.Where(x => x.IsActive).Sum(x => x.Quantity ?? 0);

    /// <summary>Bottoms size strip (XS..4XL then the optional sizes), shown left-to-right with the total beside it.</summary>
    public ObservableCollection<SizeEntry> BottomStrip { get; }
    public ICollectionView BottomStripView { get; }


    // ---- flags ----
    public ObservableCollection<FlagSizeEntry> FlagSizes { get; }
    public ICollectionView FlagSizesView { get; }
    public ICommand AddFlagSizeCommand { get; }
    public ICommand RemoveFlagSizeCommand { get; }
    public ICommand SetUnitCommand { get; }
    /// <summary>"ft" or "in" - applies to every size line (numbers are not converted when switching).</summary>
    public string FlagUnit
    {
        get => _flagUnit;
        set { if (Set(ref _flagUnit, value == "in" ? "in" : "ft")) { OnPropertyChanged(nameof(IsFeet)); OnPropertyChanged(nameof(IsInches)); OnPropertyChanged(nameof(FlagUnitText)); } }
    }
    // clicking the unit that is already on keeps it on (re-raise so the toggle stays pressed)
    public bool IsFeet { get => _flagUnit == "ft"; set { if (value) FlagUnit = "ft"; else OnPropertyChanged(nameof(IsFeet)); } }
    public bool IsInches { get => _flagUnit == "in"; set { if (value) FlagUnit = "in"; else OnPropertyChanged(nameof(IsInches)); } }
    public string FlagUnitText => _flagUnit == "in" ? "inches (in)" : "feet (ft)";
    public int FlagTotal => FlagSizes.Sum(f => f.Quantity ?? 0);
    private FlagSizeEntry NewFlagRow()
    {
        var f = new FlagSizeEntry();
        f.PropertyChanged += (_, a) => { if (a.PropertyName == nameof(FlagSizeEntry.Quantity)) OnPropertyChanged(nameof(FlagTotal)); };
        return f;
    }

    // ---- free workspaces ----
    /// <summary>DTF sheet / flag artwork workspace (front side).</summary>
    public CanvasDoc Workspace { get; } = new();

    // ---- status line (submit result) ----
    public string StatusText { get => _status; private set { if (Set(ref _status, value)) OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => _status.Length > 0;
    public string? LastPdfPath { get; private set; }
    public ICommand OpenDocumentsCommand { get; }
    public void ShowStatus(string text, int seconds = 8)
    {
        StatusText = text;
        int my = ++_statusSeq;
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        t.Tick += (_, _) => { t.Stop(); if (my == _statusSeq) StatusText = ""; };
        t.Start();
    }

    // ---- submit / edit ----
    public bool IsEditable
    {
        get => _isEditable;
        private set
        {
            if (!Set(ref _isEditable, value)) return;
            AdultShortView.Refresh(); AdultLongView.Refresh(); KidsView.Refresh(); PreschoolView.Refresh();
            BottomSizesView.Refresh(); OptionalSizesView.Refresh(); PrintMethodsView.Refresh(); ExtrasView.Refresh();
            BottomStripView.Refresh(); FlagSizesView.Refresh(); JacketExtrasView.Refresh(); SizeColumnsView.Refresh();
            RefreshBottomLines();
            OnPropertyChanged(nameof(SizeRowCount)); OnPropertyChanged(nameof(ShowNoExtras)); OnPropertyChanged(nameof(ShowNoJacketExtras));
        }
    }

    // ---- flip (front / back side) ----
    private bool _isBack;
    /// <summary>Back side workspace: text boxes and pictures.</summary>
    public CanvasDoc Back { get; } = new();
    public bool IsBackSide
    {
        get => _isBack;
        set { if (Set(ref _isBack, value)) { OnPropertyChanged(nameof(FlipText)); OnPropertyChanged(nameof(SheetHeight)); } }
    }
    public string FlipText => _isBack ? "⇄  Flip to Front" : "⇄  Flip to Back";
    public ICommand FlipCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand PdfCommand { get; }

    /// <summary>Runs <paramref name="render"/> with the sheet in its locked "output" state on the chosen side, then restores the live state.</summary>
    public T RenderAs<T>(bool backSide, Func<T> render)
    {
        bool editable = _isEditable, back = _isBack;
        IsEditable = false; IsBackSide = backSide;
        try { return render(); }
        finally { IsBackSide = back; IsEditable = editable; }
    }
    public ICommand SubmitCommand { get; }
    public ICommand EditCommand { get; }

    /// <summary>
    /// Submit: lock the sheet, then save its PDF automatically to Documents\NexOrderDesk Orders (no dialogs, no printing).
    /// The same order number always overwrites its own file, so re-submitting never makes duplicates.
    /// </summary>
    private void Submit()
    {
        if (int.TryParse(OrderNumber.Trim(), out var n)) _s.LastOrderNumber = n;
        Customer = Customer.Trim();
        _s.RememberCustomer(Customer);          // new customers are suggested next time
        _s.Save();
        IsEditable = false;
        ShowStatus($"\u2714  Order #{OrderNumber} submitted", 6);
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            var path = SheetOutput.AutoSavePdf(this);
            if (path != null) { LastPdfPath = path; ShowStatus($"\u2714  Order #{OrderNumber} submitted", 6); }
            else ShowStatus($"\u2714  Order #{OrderNumber} submitted  \u2022  PDF could not be saved automatically (use PDF)", 10);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
}
