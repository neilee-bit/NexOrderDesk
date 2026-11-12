using System.Globalization;
using System.Windows;
using System.Windows.Data;
namespace NexOrderDesk.Core;

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is bool b ? !b : true;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is bool b ? !b : false;
}

/// <summary>True when the bound number is at least <see cref="Min"/> (e.g. 3 or more material rows).</summary>
public sealed class AtLeastConverter : IValueConverter
{
    public int Min { get; set; }
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is int n && n >= Min;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => Binding.DoNothing;
}
