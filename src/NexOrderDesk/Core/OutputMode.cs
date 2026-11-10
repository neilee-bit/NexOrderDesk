using System.Windows;
namespace NexOrderDesk.Core;

/// <summary>
/// Inherited flags set on the sheet root.
/// IsActive = "output look" (submitted sheet, print, PDF): selectable arrows, +/- buttons, image outlines are hidden.
/// IsPrint  = the sheet is being rendered for paper/PDF: the bottom action bar is hidden.
/// </summary>
public static class OutputMode
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
        "IsActive", typeof(bool), typeof(OutputMode), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetIsActive(DependencyObject o) => (bool)o.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject o, bool v) => o.SetValue(IsActiveProperty, v);

    public static readonly DependencyProperty IsPrintProperty = DependencyProperty.RegisterAttached(
        "IsPrint", typeof(bool), typeof(OutputMode), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetIsPrint(DependencyObject o) => (bool)o.GetValue(IsPrintProperty);
    public static void SetIsPrint(DependencyObject o, bool v) => o.SetValue(IsPrintProperty, v);
}
