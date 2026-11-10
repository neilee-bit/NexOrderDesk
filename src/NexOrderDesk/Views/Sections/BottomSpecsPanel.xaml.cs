using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NexOrderDesk.Services;
namespace NexOrderDesk.Views.Sections;

public partial class BottomSpecsPanel : UserControl
{
    public BottomSpecsPanel() { InitializeComponent(); }

    private void OnManagePockets(object sender, RoutedEventArgs e)
    {
        PocketToggle.IsChecked = false;
        Dispatcher.BeginInvoke(new Action(() => OptionsManager.Show("pockets")), DispatcherPriority.Background);
    }

    private void OnManageExtras(object sender, RoutedEventArgs e) => OptionsManager.Show("extras");
}
