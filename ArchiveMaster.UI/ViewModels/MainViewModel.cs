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
using ArchiveMaster.Messages;
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
    private readonly IDialogService dialogService;

    [ObservableProperty]
    private bool isProgressRingOverlayActive;
    
    // [ObservableProperty]
    // private bool isToolOpened;

    [ObservableProperty]
    private object mainContent;

    [ObservableProperty]
    private bool scrollViewBringIntoViewOnFocusChange;

    [ObservableProperty]
    private ObservableCollection<ToolPanelGroupInfo> panelGroups = new ObservableCollection<ToolPanelGroupInfo>();

    [ObservableProperty]
    private AppConfig appConfig;

    public MainViewModel(AppConfig appConfig,IDialogService dialogService, IBackCommandService backCommandService = null)
    {
        this.dialogService = dialogService;
        AppConfig = appConfig;
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
        
        WeakReferenceMessenger.Default.Register<LoadingMessage>(this, (o, m) => IsProgressRingOverlayActive = m.IsVisible);
    }

    public IBackCommandService BackCommandService { get; }

    [RelayCommand]
    private void ScrollViewKeyDown()
    {
        //按Tab时，需要按钮自动进入视野；平常的话，会导致鼠标多点一下
        ScrollViewBringIntoViewOnFocusChange = true;
    }

    // [RelayCommand]
    // private void EnterTool(ToolPanelInfo panelInfo)
    // {
    //     if (panelInfo.PanelInstance == null)
    //     {
    //         panelInfo.PanelInstance = HostServices.GetService(panelInfo.ViewType) as PanelBase ??
    //                                   throw new Exception($"无法找到{panelInfo.ViewType}服务");
    //         // if (panelInfo.PanelInstance.DataContext is ViewModelBase vm)
    //         // {
    //         //     vm.RequestClosing += async (s, e) =>
    //         //     {
    //         //         CancelEventArgs args = new CancelEventArgs();
    //         //         if ((s as StyledElement)?.DataContext is ViewModelBase vm)
    //         //         {
    //         //             await vm.OnExitAsync(args);
    //         //         }
    //         //
    //         //         if (!args.Cancel)
    //         //         {
    //         //             IsToolOpened = false;
    //         //         }
    //         //     };
    //         // }
    //
    //         panelInfo.PanelInstance.Title = panelInfo.Title;
    //         panelInfo.PanelInstance.Description = panelInfo.Description;
    //     }
    //
    //     (panelInfo.PanelInstance.DataContext as ViewModelBase)?.OnEnter();
    //     MainContent = panelInfo.PanelInstance;
    //     // IsToolOpened = true;
    // }

    [RelayCommand]
    private async Task OpenSettingDialogAsync()
    {
        var dialog = HostServices.GetRequiredService<SettingDialog>();
        await dialogService.ShowCustomDialogAsync(dialog);
    }
}