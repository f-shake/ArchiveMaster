using ArchiveMaster.Configs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FzLib.IO;
using Mapster;

namespace ArchiveMaster.Views;

public partial class FileFilterControl : UserControl
{
    public static readonly StyledProperty<FileFilterRule> FilterProperty =
        FileFilterPanel.FilterProperty.AddOwner<FileFilterControl>();

    public static readonly StyledProperty<object> ButtonContentProperty = AvaloniaProperty.Register<FileFilterControl, object>(
        nameof(ButtonContent),"设置..");

    public object ButtonContent
    {
        get => GetValue(ButtonContentProperty);
        set => SetValue(ButtonContentProperty, value);
    }
    public FileFilterControl()
    {
        InitializeComponent();
    }

    public event EventHandler Closed
    {
        add => (btn.Flyout as PopupFlyout).Closed += value;
        remove => (btn.Flyout as PopupFlyout).Closed -= value;
    }

    public event EventHandler Opened
    {
        add => (btn.Flyout as PopupFlyout).Opened += value;
        remove => (btn.Flyout as PopupFlyout).Opened -= value;
    }

    public FileFilterRule Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }
}