using System.Windows;
using System.Windows.Controls;
using NexOrderDesk.Services;
namespace NexOrderDesk.Views.Sections;

public partial class PrintMethodPanel : UserControl
{
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(PrintMethodPanel), new PropertyMetadata(false));
    /// <summary>Smaller, less dominant version (bottoms / shorts sheets).</summary>
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    public PrintMethodPanel() { InitializeComponent(); }
    private void OnManage(object sender, RoutedEventArgs e) => OptionsManager.Show("printtypes");
}
