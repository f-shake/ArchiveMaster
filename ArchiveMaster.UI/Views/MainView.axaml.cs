using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Avalonia.Dialogs;
using ArchiveMaster.Messages;
using ArchiveMaster.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using System.Reflection;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Interactivity;
using ArchiveMaster.Platforms;
using FzLib;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using ArchiveMaster.Models;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Serilog;

namespace ArchiveMaster.Views;

public partial class MainView : UserControl
{
    private readonly AppConfig appConfig;
    private readonly IDialogService dialogService;
    private readonly IPermissionService permissionService;

    public MainView(MainViewModel viewModel,
        AppConfig appConfig,
        IDialogService dialogService,
        IViewPadding viewPadding = null,
        IPermissionService permissionService = null)
    {
        this.appConfig = appConfig;
        this.dialogService = dialogService;
        this.permissionService = permissionService;
        DataContext = viewModel;

        InitializeComponent();
        if (viewPadding != null)
        {
            Padding = new Thickness(0, viewPadding.GetTop(), 0, viewPadding.GetBottom());
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        permissionService?.CheckPermissions();
        if (appConfig.LoadError != null)
        {
            await dialogService.ShowErrorDialogAsync("加载配置失败", appConfig.LoadError);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (Bounds.Width <= 420)
        {
            Resources["BoxWidth"] = 160d;
            Resources["BoxHeight"] = 200d;
            Resources["ShowDescription"] = false;
        }
        else
        {
            Resources["BoxWidth"] = 200d;
            Resources["BoxHeight"] = 280d;
            Resources["ShowDescription"] = true;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        WeakReferenceMessenger.Default.UnregisterAll(this);
        WeakReferenceMessenger.Default.Cleanup();
    }

    // private void ToolItem_OnKeyDown(object sender, KeyEventArgs e)
    // {
    //     if (e.Key == Key.Enter)
    //     {
    //         TopLevel.GetTopLevel(this).FocusManager.ClearFocus();
    //         (DataContext as MainViewModel).EnterToolCommand.Execute((sender as ToolItemBox).DataContext);
    //     }
    // }

    private void BeginChangingContent()
    {
        IsHitTestVisible = false;
    }

    private void EndChangingContent()
    {
        IsHitTestVisible = true;
    }

    private void PanelViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModelBase.IsWorking))
        {
            Dispatcher.UIThread.Invoke(() => grdLeft.IsEnabled = !((ViewModelBase)sender).IsWorking);
        }
    }

    private async void PanelViewModelRequestClosing(object sender, EventArgs e)
    {
        BeginChangingContent();
        mainContent.Opacity = 0;
        await Task.Delay(300);
        mainContent.Content = null;
        //清除其他ListBox的选中项
        ListBox lbx = sender as ListBox;
        foreach (var list in lstGroups.GetVisualDescendants()
                     .OfType<ListBox>())
        {
            list.SelectedItem = null;
        }
        EndChangingContent();
    }

    private async void SelectingItemsControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is null or { Count: 0 }
            || e.AddedItems[0] is not ToolPanelInfo panelInfo)
        {
            return;
        }

        //通知已打开的面板退出
        if (mainContent.Content is PanelBase { DataContext: ViewModelBase vm })
        {
            var args = new CancelEventArgs();
            await vm.OnExitAsync(args);
            if (args.Cancel)
            {
                return;
            }

            vm.PropertyChanged -= PanelViewModelPropertyChanged;
            vm.RequestClosing -= PanelViewModelRequestClosing;
        }

        //清除其他ListBox的选中项
        ListBox lbx = sender as ListBox;
        foreach (var list in lstGroups.GetVisualDescendants()
                     .OfType<ListBox>()
                     .Where(p => p != lbx))
        {
            list.SelectedItem = null;
        }

        BeginChangingContent();
        mainContent.Opacity = 0;
        await Task.Delay(300);

        //避免页面的创建卡住UI，先让ListBox的选择响应起来
        Dispatcher.UIThread.Post(() =>
        {
            if (panelInfo.PanelInstance == null)
            {
                panelInfo.PanelInstance = HostServices.GetService(panelInfo.ViewType) as PanelBase ??
                                          throw new Exception($"无法找到{panelInfo.ViewType}服务");

                panelInfo.PanelInstance.Title = panelInfo.Title;
                panelInfo.PanelInstance.Description = panelInfo.Description;
            }

            if (panelInfo.PanelInstance.DataContext is ViewModelBase vm)
            {
                vm.PropertyChanged += PanelViewModelPropertyChanged;
                vm.RequestClosing += PanelViewModelRequestClosing;
                vm.OnEnter();
            }

            mainContent.Content = panelInfo.PanelInstance;
            mainContent.Opacity = 1;
            EndChangingContent();
        });
    }
}