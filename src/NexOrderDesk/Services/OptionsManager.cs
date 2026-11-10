using System.Windows;
using NexOrderDesk.Views;
namespace NexOrderDesk.Services;

public static class OptionsManager
{
    public static void Show(string? key)
    {
        if (key == null) return;
        try
        {
            var owner = Application.Current.MainWindow;
            var w = new OptionsManagerWindow(key);
            if (owner != null && owner.IsLoaded) w.Owner = owner;
            w.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not open the list editor:\n" + ex.Message, "Edit list", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        SettingsService.Instance.Save();
    }
}
