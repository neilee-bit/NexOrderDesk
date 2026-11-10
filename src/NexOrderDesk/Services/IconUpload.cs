using System.IO;
using System.Windows;
using Microsoft.Win32;
using NexOrderDesk.Models;
namespace NexOrderDesk.Services;

/// <summary>Lets the user pick any picture as an order-type icon. The file is copied into %LocalAppData%\NexOrderDesk\icons.</summary>
public static class IconUpload
{
    public static bool Choose(OrderTypeDef type, Window? owner)
    {
        var dlg = new OpenFileDialog { Filter = ImageLoader.OpenFilter, Title = $"Icon for \"{type.Name}\"" };
        if ((owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog()) != true) return false;
        if (ImageLoader.LoadFile(dlg.FileName) == null) return false;          // shows why it could not be read
        try
        {
            Directory.CreateDirectory(SettingsService.IconDir);
            var target = Path.Combine(SettingsService.IconDir, $"{type.Id}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(dlg.FileName).ToLowerInvariant()}");
            File.Copy(dlg.FileName, target, true);
            var old = type.IconFile;
            type.IconFile = target;
            if (old != null && old != target) { try { File.Delete(old); } catch { } }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not save the icon: " + ex.Message, "Icon");
            return false;
        }
    }

    public static void Clear(OrderTypeDef type)
    {
        var old = type.IconFile;
        type.IconFile = null;
        if (old != null) { try { File.Delete(old); } catch { } }
    }
}
