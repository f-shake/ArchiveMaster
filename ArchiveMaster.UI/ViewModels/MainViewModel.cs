using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ArchiveMaster.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static ArchiveMaster.ViewModels.MainViewModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Models;
using ArchiveMaster.Platforms;
using ArchiveMaster.Services;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using FzLib.Avalonia.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ViewModelServices services;

    [ObservableProperty]
    private bool isProgressRingOverlayActive;

    [ObservableProperty]
    private ObservableCollection<ToolPanelGroupInfo> panelGroups = new ObservableCollection<ToolPanelGroupInfo>();

    [ObservableProperty]
    private bool scrollViewBringIntoViewOnFocusChange;

    public MainViewModel(ViewModelServices services, IBackCommandService backCommandService = null)
    {
        this.services = services;
        foreach (var view in Initializer.Views)
        {
            PanelGroups.Add(view);
        }

        backCommandService?.RegisterBackCommand(() =>
        {
            // if (mainContent is PanelBase && IsToolOpened)
            // {
            //     IsToolOpened = false;
            //     return true;
            // }

            return false;
        });
        BackCommandService = backCommandService;
    }

    public IBackCommandService BackCommandService { get; }

    [RelayCommand]
    private async Task OpenMasterPasswordDialogAsync()
    {
        var dialog = HostServices.GetRequiredService<MasterPasswordDialog>();
        await services.Dialog.ShowCustomDialogAsync(dialog);
    }

    [RelayCommand]
    private async Task OpenSettingDialogAsync()
    {
        var dialog = HostServices.GetRequiredService<SettingDialog>();
        await services.Dialog.ShowCustomDialogAsync(dialog);
    }

    [RelayCommand]
    private void ScrollViewKeyDown()
    {
        //按Tab时，需要按钮自动进入视野；平常的话，会导致鼠标多点一下
        ScrollViewBringIntoViewOnFocusChange = true;
    }
}