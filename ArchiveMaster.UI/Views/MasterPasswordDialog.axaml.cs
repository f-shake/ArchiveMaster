using System;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.Views;

public partial class MasterPasswordDialog : DialogHost
{
    public MasterPasswordDialog()
    {
        InitializeComponent();
    }

    protected override void OnCloseButtonClick()
    {
        Close();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MasterPasswordViewModel vm)
        {
            vm.Close += (s, e2) => Close();
        }
    }
}