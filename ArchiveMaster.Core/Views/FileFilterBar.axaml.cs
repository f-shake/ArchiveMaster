using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Avalonia.Converters;
using FzLib.Avalonia.Dialogs;
using FzLib.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Controls;

namespace ArchiveMaster.Views;

public partial class FileFilterBar : UserControl
{
    public FileFilterBar()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<FileFilterRule> FilterProperty =
        AvaloniaProperty.Register<FilePickerTextBox, FileFilterRule>(nameof(Filter));

    public static readonly StyledProperty<object> OpenPanelButtonContentProperty =
        AvaloniaProperty.Register<FileFilterBar, object>(
            nameof(OpenPanelButtonContent), "筛选..");

    public object OpenPanelButtonContent
    {
        get => GetValue(OpenPanelButtonContentProperty);
        set => SetValue(OpenPanelButtonContentProperty, value);
    }

    public FileFilterRule Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    private void FileFilterPopup_Closed(object sender, EventArgs e)
    {
        var binding = BindingOperations.GetBindingExpressionBase(tbkFilterDescription, TextBlock.TextProperty);
        binding?.UpdateTarget();
    }
}