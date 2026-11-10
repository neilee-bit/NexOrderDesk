using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NexOrderDesk.Core;
using NexOrderDesk.Services;
namespace NexOrderDesk.Themes;

public partial class BaseStyles : ResourceDictionary
{
    // Footer button inside every FlatCombo drop-down that has ComboHelper.ManageKey set.
    private void Manage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.TemplatedParent is not ComboBox cb) return;
        var key = ComboHelper.GetManageKey(cb);
        cb.IsDropDownOpen = false;
        cb.Dispatcher.BeginInvoke(new Action(() => OptionsManager.Show(key)), DispatcherPriority.Background);
    }
}
