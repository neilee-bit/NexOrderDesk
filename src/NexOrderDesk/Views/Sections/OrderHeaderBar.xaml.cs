using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using NexOrderDesk.ViewModels;
namespace NexOrderDesk.Views.Sections;

/// <summary>
/// Order information row. Delivery date fix: the calendar is no longer two-way bound (that binding could be lost when the
/// pop-up closed during the click, so the sheet kept the old date). The picked date is written straight into the order,
/// the pop-up closes after the click has finished, and the calendar's mouse capture is released.
/// </summary>
public partial class OrderHeaderBar : UserControl
{
    private bool _syncing;

    public OrderHeaderBar() { InitializeComponent(); }

    private OrderViewModel? Vm => DataContext as OrderViewModel;

    private void OnDatePopupOpened(object? sender, EventArgs e)
    {
        if (Vm is not { } o) return;
        _syncing = true;
        Cal.SelectedDate = o.DeliveryDate;
        Cal.DisplayDate = o.DeliveryDate;
        _syncing = false;
        PickedText.Text = o.DeliveryDate.ToString("dddd, dd MMMM yyyy");
    }

    private void OnDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || Vm is not { } o || Cal.SelectedDate is not DateTime d) return;
        SetDate(o, d);
    }

    private void OnQuickDate(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } o || sender is not FrameworkElement fe || !int.TryParse(fe.Tag?.ToString(), out var days)) return;
        SetDate(o, DateTime.Today.AddDays(days));
    }

    private void SetDate(OrderViewModel o, DateTime d)
    {
        o.DeliveryDate = d.Date;
        DateText.Text = o.DeliveryText;                    // belt and braces: show it even if a binding was dropped
        PickedText.Text = d.ToString("dddd, dd MMMM yyyy");
        // close after the current mouse click is fully processed
        Dispatcher.BeginInvoke(new Action(() =>
        {
            DateToggle.IsChecked = false;
            if (Mouse.Captured is CalendarItem or Calendar or CalendarDayButton) Mouse.Capture(null);
            DateText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(OrderViewModel.DeliveryText)) { Mode = System.Windows.Data.BindingMode.OneWay });
        }), DispatcherPriority.Input);
    }

    // The WPF Calendar keeps the mouse captured after a click, which swallows the next click on the sheet.
    private void OnCalendarMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured is CalendarItem or Calendar or CalendarDayButton) Mouse.Capture(null);
    }
}
