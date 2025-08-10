using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ArchiveMaster.Messages;
using ArchiveMaster.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using Avalonia.Controls;
using FzLib.Avalonia.Dialogs;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ArchiveMaster.ViewModels;

public abstract partial class TwoStepViewModelBase<TService, TConfig> : MultiPresetViewModelBase<TConfig>
    where TService : TwoStepServiceBase<TConfig>
    where TConfig : ConfigBase, new()
{
    /// <summary>
    /// 能否取消
    /// </summary>
    [ObservableProperty]
    private bool canCancel = false;

    /// <summary>
    /// 是否允许执行
    /// </summary>
    [ObservableProperty]
    private bool canExecute = false;

    /// <summary>
    /// 是否允许初始化
    /// </summary>
    [ObservableProperty]
    private bool canInitialize = true;

    private bool canReceiveServiceMessage = false;

    /// <summary>
    /// 是否允许重置
    /// </summary>
    [ObservableProperty]
    private bool canReset = false;

    [ObservableProperty]
    private string message = "就绪";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressIndeterminate))]
    private double progress;

    protected TwoStepViewModelBase(AppConfig appConfig, IDialogService dialogService, string configGroupName)
        : base(appConfig, dialogService, configGroupName)
    {
    }

    protected TwoStepViewModelBase(AppConfig appConfig, IDialogService dialogService)
        : this(appConfig, dialogService, typeof(TConfig).Name)
    {
    }

    /// <summary>
    /// 是否启用Two-Step中的初始化。若禁用，将不显示初始化按钮
    /// </summary>
    public virtual bool EnableInitialize => true;

    /// <summary>
    /// 当进度为double.NaN时，认为进度为非确定模式
    /// </summary>
    public bool ProgressIndeterminate => double.IsNaN(Progress);

    protected bool CheckWarningFilesOnExecuted { get; set; } = true;

    protected bool CheckWarningFilesOnInitialized { get; set; } = true;

    /// <summary>
    /// 核心服务
    /// </summary>
    protected TService Service { get; private set; }

    public override void OnEnter()
    {
        base.OnEnter();
        ResetCommand.Execute(null);
    }

    /// <summary>
    /// 创建服务
    /// </summary>
    protected void CreateService()
    {
        Service = CreateServiceImplement();
        Debug.Assert(Service != null);
        Service.ProgressUpdate += Service_ProgressUpdate;
        Service.MessageUpdate += Service_MessageUpdate;
    }

    /// <summary>
    /// 创建服务实例的具体实现，可以重写
    /// </summary>
    /// <returns></returns>
    protected virtual TService CreateServiceImplement()
    {
        var service = HostServices.GetRequiredService<TService>();
        service.Config = Config;
        return service;
    }

    protected override void OnConfigChanged()
    {
        ResetCommand.Execute(null);
    }

    /// <summary>
    /// 执行完成后的任务
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual Task OnExecutedAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 执行前的任务
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual Task OnExecutingAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 初始化后的任务
    /// </summary>
    /// <returns></returns>
    protected virtual Task OnInitializedAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 初始化前的任务
    /// </summary>
    /// <returns></returns>
    protected virtual Task OnInitializingAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 重置操作
    /// </summary>
    protected virtual void OnReset()
    {
    }

    /// <summary>
    /// 取消正在执行或初始化的任务
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        CanCancel = false;
        CancelCommand.NotifyCanExecuteChanged();
        WeakReferenceMessenger.Default.Send(new LoadingMessage(true));
        if (InitializeCommand.IsRunning)
        {
            InitializeCommand.Cancel();
            CanInitialize = false;
            InitializeCommand.NotifyCanExecuteChanged();
        }
        else if (ExecuteCommand.IsRunning)
        {
            ExecuteCommand.Cancel();
            CanExecute = false;
            ExecuteCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 执行后检查
    /// </summary>
    /// <param name="token"></param>
    /// <returns>如果不存在需要处理的文件，返回true</returns>
    private async Task CheckWarningFilesOnExecutedAsync(CancellationToken token)
    {
        if (!CheckWarningFilesOnExecuted)
        {
            return;
        }

        var files = Service.GetExecutedFiles();
        if (files == null)
        {
            return;
        }

        var wrongFiles = files.Where(p => p.Status is Enums.ProcessStatus.Warn or Enums.ProcessStatus.Error).ToList();
        if (wrongFiles.Count>0)
        {
            await DialogService.ShowWarningDialogAsync("存在警告", $"执行完成，但存在{wrongFiles.Count}个警告或错误文件，请仔细检查",
                string.Join(Environment.NewLine, wrongFiles.Select(p => p.RelativePath)));
        }
    }

    /// <summary>
    /// 初始化后检查并抛出警告
    /// </summary>
    /// <param name="token"></param>
    /// <returns>如果不存在需要处理的文件，返回true</returns>
    private async Task<bool> CheckWarningFilesOnInitializedAsync(CancellationToken token)
    {
        if (!CheckWarningFilesOnInitialized)
        {
            return false;
        }

        var files = Service.GetInitializedFiles();
        if (files == null)
        {
            return false;
        }

        if (!files.Any())
        {
            await DialogService.ShowWarningDialogAsync("结果为空", "不存在符合条件的需要处理的文件");
            return true;
        }

        var wrongFiles = files.Where(p => p.Status is Enums.ProcessStatus.Warn or Enums.ProcessStatus.Error).ToList();
        if (wrongFiles.Count>0)
        {
            await DialogService.ShowWarningDialogAsync("存在警告", $"执行完成，但存在{wrongFiles.Count}个警告或错误文件，请仔细检查",
                string.Join(Environment.NewLine, wrongFiles.Select(p => p.RelativePath)));
        }

        return false;
    }

    /// <summary>
    /// 注销服务
    /// </summary>
    private void DisposeService()
    {
        if (Service == null)
        {
            return;
        }

        Service.ProgressUpdate -= Service_ProgressUpdate;
        Service.MessageUpdate -= Service_MessageUpdate;
        Service = null;
    }

    /// <summary>
    /// 执行任务
    /// </summary>
    /// <param name="token"></param>
    /// <exception cref="NullReferenceException"></exception>
    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync(CancellationToken token)
    {
        if (!EnableInitialize)
        {
            AppConfig.Save(false);
            CreateService();
        }

        if (Service == null)
        {
            throw new NullReferenceException($"{nameof(Service)}为空");
        }

        CanExecute = false;
        CanReset = false;
        ResetCommand.NotifyCanExecuteChanged();
        CanCancel = true;
        CancelCommand.NotifyCanExecuteChanged();

        await TryRunServiceMethodAsync(async () =>
        {
            await OnExecutingAsync(token);
            Config.Check();
            await Service.ExecuteAsync(token);
            Service.Dispose();
            await OnExecutedAsync(token);
            await CheckWarningFilesOnExecutedAsync(token);
        }, "执行失败");

        CanReset = true;
        ResetCommand.NotifyCanExecuteChanged();
        CanCancel = false;
        CancelCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 初始化任务
    /// </summary>
    /// <param name="token"></param>
    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanInitialize))]
    private async Task InitializeAsync(CancellationToken token)
    {
        AppConfig.Save(false);
        CanInitialize = false;
        InitializeCommand.NotifyCanExecuteChanged();
        CanReset = false;
        ResetCommand.NotifyCanExecuteChanged();
        CanCancel = true;
        CancelCommand.NotifyCanExecuteChanged();

        if (await TryRunServiceMethodAsync(async () =>
            {
                CreateService();
                await OnInitializingAsync();
                Config.Check();
                await Service.InitializeAsync(token);
                await OnInitializedAsync();
            }, "初始化失败") //初始化成功
            && !await CheckWarningFilesOnInitializedAsync(token)) //有需要处理的文件
        {
            CanExecute = true;
            CanReset = true;
        }
        else
        {
            CanExecute = false;
            CanReset = false;
            CanInitialize = true;
        }

        ExecuteCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
        InitializeCommand.NotifyCanExecuteChanged();
        CanCancel = false;
        CancelCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 重置
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReset))]
    private void Reset()
    {
        CanReset = false;
        CanInitialize = EnableInitialize;
        CanExecute = !EnableInitialize;

        ResetCommand.NotifyCanExecuteChanged();
        ExecuteCommand.NotifyCanExecuteChanged();
        InitializeCommand.NotifyCanExecuteChanged();

        Message = "就绪";
        OnReset();
        DisposeService();
    }

    private void Service_MessageUpdate(object sender, MessageUpdateEventArgs e)
    {
        if (!canReceiveServiceMessage)
        {
            return;
        }

        Message = e.Message;
    }

    private void Service_ProgressUpdate(object sender, ProgressUpdateEventArgs e)
    {
        Progress = e.Progress;
    }

    private async Task<bool> TryRunServiceMethodAsync(Func<Task> action, string errorTitle)
    {
        Progress = double.NaN;
        Message = "正在处理";
        IsWorking = true;
        try
        {
            canReceiveServiceMessage = true;
            await action();
            return true;
        }
        catch (OperationCanceledException)
        {
            await DialogService.ShowOkDialogAsync("操作已取消", "操作已被用户取消");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "执行工具失败");
            await DialogService.ShowErrorDialogAsync(errorTitle, ex);
            return false;
        }
        finally
        {
            Progress = 0;
            IsWorking = false;
            canReceiveServiceMessage = false;
            Message = "完成";
            WeakReferenceMessenger.Default.Send(new LoadingMessage(false));
        }
    }
}