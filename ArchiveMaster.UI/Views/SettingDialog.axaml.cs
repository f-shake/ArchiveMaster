using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.Views;

public partial class SettingDialog : DialogHost
{
    public SettingDialog()
    {
        InitializeComponent();
    }

    protected override void OnPrimaryButtonClick()
    {
        if (DataContext is SettingViewModel vm)
        {
            if (vm.OnSettingPanelClosing())
            {
                Close();
            }
        }
    }

    private void Control_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingViewModel vm)
        {
            vm.OnSettingPanelOpened();
        }
    }
}