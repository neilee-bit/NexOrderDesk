using System.Windows;
using System.Windows.Controls;
using NexOrderDesk.Models;
using NexOrderDesk.Services;
using NexOrderDesk.ViewModels;
namespace NexOrderDesk.Views.Sections;

/// <summary>Sheet banner. The palette button changes the theme colour of this one order.</summary>
public partial class TitleBanner : UserControl
{
    public TitleBanner()
    {
        InitializeComponent();
        Palette.ItemsSource = ColorMath.Presets;
    }

    private OrderViewModel? Vm => DataContext as OrderViewModel;

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (Vm is { } o) HexBox.Text = o.AccentHex;
    }

    private void OnSwatch(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string hex && Vm is { } o) { o.SetAccent(hex); HexBox.Text = hex; }
    }

    private void OnApplyHex(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } o) return;
        if (ColorMath.NormalizeHex(HexBox.Text) is { } hex) o.SetAccent(hex);
        else MessageBox.Show("Type a colour like #1B2A4A.", "Theme colour");
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } o) return;
        o.ResetAccent(); HexBox.Text = o.AccentHex;
    }

    private void OnIconClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm is not { IsEditable: true } o) return;
        if (IconUpload.Choose(o.Type, Window.GetWindow(this))) SettingsService.Instance.Save();
    }

    private void OnSaveDefault(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } o) return;
        o.SaveAccentAsTypeDefault();
        ColorToggle.IsChecked = false;
    }
}
