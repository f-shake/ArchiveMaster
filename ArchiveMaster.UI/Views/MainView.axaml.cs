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
using ArchiveMaster.Services;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Serilog;

namespace ArchiveMaster.Views;

public partial class MainView : UserControl
{
    private readonly AppConfig appConfig;
    private readonly IDialogService dialogService;
    private readonly IPermissionService permissionService;

    private bool isFirstLoad = true;

    public MainView()
    {
        throw new Exception("请调用带参数的构造函数");
    }

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
        
        //Linux下会遇到无法找到TopLevel导致无法显示的问题，所以这里手动设置一下
        dialogService.DefaultOwner=TopLevel.GetTopLevel(this);
        
        if (appConfig.LoadError != null)
        {
            await dialogService.ShowErrorDialogAsync("加载配置失败", appConfig.LoadError);
        }

        try
        {
            var pswd = SecurePasswordStoreService.DecryptMasterPassword(GlobalConfigs.Instance.MasterPassword);
            if (pswd == GlobalConfigs.DefaultPassword)
            {
                await dialogService.ShowWarningDialogAsync("提示", "当前未设置主密码，请先设置主密码");
                var dialog = HostServices.GetRequiredService<MasterPasswordDialog>();
                await dialogService.ShowCustomDialogAsync(dialog);
            }
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorDialogAsync("加载主密码失败", 
                "主密码解析失败，可能是配置文件错误或软硬件发生改变，请重新设置主密码", 
                ex.ToString());     
            var dialog = HostServices.GetRequiredService<MasterPasswordDialog>();
            await dialogService.ShowCustomDialogAsync(dialog);
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

    private void BeginChangingContent()
    {
        Dispatcher.UIThread.Invoke(() => IsHitTestVisible = false);
    }

    private void EndChangingContent()
    {
        Dispatcher.UIThread.Invoke(() => IsHitTestVisible = true);
    }

    private async void EnterNewTool(ToolPanelInfo panelInfo)
    {
        try
        {
            TimeSpan animationDuration = (TimeSpan)Resources["AnimationDuration"];

            //若为空，则新建页面对象
            if (panelInfo.PanelInstance == null)
            {
                panelInfo.PanelInstance = HostServices.GetService(panelInfo.ViewType) as PanelBase ??
                                          throw new Exception($"无法找到{panelInfo.ViewType}服务");
                panelInfo.PanelInstance.Title = panelInfo.Title;
                panelInfo.PanelInstance.Description = panelInfo.Description;
            }

            //注册ViewModel的事件，通知进入
            if (panelInfo.PanelInstance.DataContext is ViewModelBase vm)
            {
                vm.PropertyChanged += PanelViewModelPropertyChanged;
                vm.RequestClosing += PanelViewModelRequestClosing;
                vm.OnEnter();
            }

            mainContent.Content = panelInfo.PanelInstance;
            if (isFirstLoad)
            {
                //第一次加载特别卡，所以先显示出来，然后等一会儿再开始动画
                await Task.Delay(animationDuration);
                isFirstLoad = false;
            }

            //开始进入动画
            mainContent.Opacity = 1;
            mainContent.RenderTransform = TransformOperations.Parse("Scale(1)");
            await Task.Delay(animationDuration);
            EndChangingContent();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开面板失败");
            await dialogService.ShowErrorDialogAsync("打开面板失败", ex.Message);
        }
    }

    private async Task<bool> ExitCurrentPanel()
    {
        if (mainContent.Content is PanelBase { DataContext: ViewModelBase vm })
        {
            var args = new CancelEventArgs();
            await vm.OnExitAsync(args);
            if (args.Cancel)
            {
                return true;
            }

            vm.PropertyChanged -= PanelViewModelPropertyChanged;
            vm.RequestClosing -= PanelViewModelRequestClosing;

            //开始退出动画
            mainContent.RenderTransform = TransformOperations.Parse("Scale(0.98)");
            mainContent.Opacity = 0.2;
        }
        else
        {
            mainContent.Opacity = 0;
        }

        return false;
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
        if (await ExitCurrentPanel())
        {
            return;
        }

        //清除其他ListBox的选中项
        ListBox lbx = sender as ListBox;
        foreach (var list in lstGroups.GetVisualDescendants()
                     .OfType<ListBox>()
                     .Where(p => p != lbx))
        {
            list.SelectedItem = null;
        }

        TimeSpan animationDuration = (TimeSpan)Resources["AnimationDuration"];
        BeginChangingContent();
        //动画期间，不做任何操作
        await Task.Delay(animationDuration);

        //避免页面的创建卡住UI，先让ListBox的选择响应起来
        Dispatcher.UIThread.Post(() => EnterNewTool(panelInfo));
    }
}