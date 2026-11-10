using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NexOrderDesk.Core;
using NexOrderDesk.Models;
using NexOrderDesk.Services;
using NexOrderDesk.Views;
namespace NexOrderDesk.ViewModels;

public class MainViewModel : ObservableObject
{
    private OrderViewModel? _selected;

    public MainViewModel()
    {
        Settings = SettingsService.Instance;
        Tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoTabs));
        NewOrderCommand = new RelayCommand(p => { if (p is OrderTypeDef t) NewOrder(t); });
        CloseTabCommand = new RelayCommand(p => { if (p is OrderViewModel o) CloseTab(o); });
        ExportCommand = new RelayCommand(_ => Export());
        ImportCommand = new RelayCommand(_ => Import());
        ManageTypesCommand = new RelayCommand(_ => ManageTypes());
        if (Settings.OrderTypes.Count > 0) NewOrder(Settings.OrderTypes[0]);
    }

    public SettingsService Settings { get; }
    public ObservableCollection<OrderViewModel> Tabs { get; } = new();
    /// <summary>Order types offered in the New Order menu (user-editable, each with its own theme colour).</summary>
    public ObservableCollection<OrderTypeDef> OrderTypes => Settings.OrderTypes;
    public bool HasNoTabs => Tabs.Count == 0;
    public ICommand NewOrderCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ManageTypesCommand { get; }

    public OrderViewModel? SelectedTab
    {
        get => _selected;
        set
        {
            var old = _selected;
            if (!Set(ref _selected, value)) return;
            if (old != null) old.PropertyChanged -= OnSelectedChanged;
            if (value != null) { value.PropertyChanged += OnSelectedChanged; ThemeService.Apply(value.AccentHex); }
        }
    }

    // The theme follows the selected order's colour live (per-order colour, or the type colour being edited).
    private void OnSelectedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrderViewModel.AccentHex) && sender is OrderViewModel o && o == _selected) ThemeService.Apply(o.AccentHex);
    }

    private void NewOrder(OrderTypeDef t)
    {
        // Suggest last submitted number + 1, skipping numbers already used by open tabs.
        int max = Settings.LastOrderNumber;
        foreach (var tab in Tabs) if (int.TryParse(tab.OrderNumber.Trim(), out var n) && n > max) max = n;
        var order = new OrderViewModel(t, Settings, max + 1);
        Tabs.Add(order); SelectedTab = order;
    }

    private void CloseTab(OrderViewModel o)
    {
        var r = MessageBox.Show($"Close order #{o.OrderNumber}?\nUnsaved changes will be lost.",
            "Close order", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (r != MessageBoxResult.OK) return;
        int i = Tabs.IndexOf(o); Tabs.Remove(o); o.Detach();
        SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(i, Tabs.Count - 1)];
    }

    private void ManageTypes()
    {
        try
        {
            new OrderTypesWindow { Owner = Application.Current.MainWindow }.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show("Could not open the order types editor: " + ex.Message, "Order types"); }
        Settings.Save();
        if (_selected != null) ThemeService.Apply(_selected.AccentHex);
    }

    private void Export()
    {
        var d = new SaveFileDialog { Filter = "JSON backup|*.json", FileName = "nex_order_desk_settings.json" };
        if (d.ShowDialog() == true) { try { Settings.ExportTo(d.FileName); } catch (Exception ex) { MessageBox.Show(ex.Message, "Export failed"); } }
    }
    private void Import()
    {
        var d = new OpenFileDialog { Filter = "JSON backup|*.json" };
        if (d.ShowDialog() == true) { try { Settings.ImportFrom(d.FileName); } catch (Exception ex) { MessageBox.Show(ex.Message, "Import failed"); } }
    }
}
