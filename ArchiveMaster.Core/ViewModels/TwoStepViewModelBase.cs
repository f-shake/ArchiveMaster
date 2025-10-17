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
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.ViewModels;

// [Flags]
// public enum TwoStepState
// {
//     CanInitialize = 0x01,
//     CanExecute = 0x02,
//     CanCancel = 0x04,
//     CanReset = 0x08,
//
//     Ready = CanInitialize,
//     Initializing = CanCancel,
//     Initialized = CanExecute | CanReset,
//     Executing = CanCancel,
//     Executed = CanReset,
// }

public abstract partial class TwoStepViewModelBase<TService, TConfig> : MultiPresetViewModelBase<TConfig>
    where TService : TwoStepServiceBase<TConfig>
    where TConfig : ConfigBase, new()
{
    #region 构造函数

    protected TwoStepViewModelBase(AppConfig appConfig, IDialogService dialogService, string configGroupName)
        : base(appConfig, dialogService, configGroupName)
    {
    }

    protected TwoStepViewModelBase(AppConfig appConfig, IDialogService dialogService)
        : this(appConfig, dialogService, typeof(TConfig).Name)
    {
    }

    #endregion

    #region 辅助字段

    /// <summary>
    /// 是否允许接收来自Service的进度和消息
    /// </summary>
    private bool canReceiveServiceMessage = false;

    #endregion

    #region 按钮可执行性

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

    /// <summary>
    /// 是否允许重置
    /// </summary>
    [ObservableProperty]
    private bool canReset = false;

    private void UpdateCommandExecutable(bool? canInitialize = null,
        bool? canExecute = null,
        bool? canCancel = null,
        bool? canReset = null)
    {
        if (canInitialize.HasValue)
        {
            CanInitialize = canInitialize.Value;
            InitializeCommand.NotifyCanExecuteChanged();
        }

        if (canExecute.HasValue)
        {
            CanExecute = canExecute.Value;
            ExecuteCommand.NotifyCanExecuteChanged();
        }

        if (canCancel.HasValue)
        {
            CanCancel = canCancel.Value;
            CancelCommand.NotifyCanExecuteChanged();
        }

        if (canReset.HasValue)
        {
            CanReset = canReset.Value;
            ResetCommand.NotifyCanExecuteChanged();
        }
    }

    #endregion

    #region 界面内容

    /// <summary>
    /// 显示在左下角的信息
    /// </summary>
    [ObservableProperty]
    private string message = "就绪";

    /// <summary>
    /// 进度
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ProgressIndeterminate))]
    private double progress;

    /// <summary>
    /// 当进度为double.NaN时，认为进度为非确定模式
    /// </summary>
    public bool ProgressIndeterminate => double.IsNaN(Progress);

    #endregion

    #region 行为控制

    /// <summary>
    /// 是否启用Two-Step中的初始化。若禁用，将不显示初始化按钮和配置面板
    /// </summary>
    public virtual bool EnableInitialize => true;

    /// <summary>
    /// 是否允许重复执行
    /// </summary>
    public virtual bool EnableRepeatExecute => false;

    /// <summary>
    /// 是否在执行完成后检查文件状态
    /// </summary>
    protected bool CheckWarningFilesOnExecuted { get; set; } = true;

    /// <summary>
    /// 是否在初始化后检查文件状态
    /// </summary>
    protected bool CheckWarningFilesOnInitialized { get; set; } = true;

    #endregion

    #region Service相关

    /// <summary>
    /// 核心服务
    /// </summary>
    protected TService Service { get; private set; }

    /// <summary>
    /// 创建服务，并绑定事件
    /// </summary>
    protected void CreateService()
    {
        Service = CreateServiceImplement();
        Debug.Assert(Service != null);
        if (Service == null)
        {
            throw new NullReferenceException($"{nameof(Service)}为空");
        }
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

    #endregion

    #region 事件

    public override void OnEnter()
    {
        base.OnEnter();
        ResetCommand.Execute(null);
    }

    /// <summary>
    /// 选取的配置改变，进行重置
    /// </summary>
    protected override void OnConfigChanged()
    {
        ResetCommand.Execute(null);
    }

    /// <summary>
    /// 执行完成后的任务
    /// </summary>
    /// <returns></returns>
    protected virtual Task OnExecutedAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 执行前的任务
    /// </summary>
    /// <returns></returns>
    protected virtual Task OnExecutingAsync(CancellationToken ct)
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

    #endregion

    #region 文件检查

    private async Task CheckWarningFiles(IEnumerable<SimpleFileInfo> files)
    {
        Debug.Assert(files != null);
        var wrongFiles = files.Where(p => p.Status is Enums.ProcessStatus.Warn or Enums.ProcessStatus.Error).ToList();
        if (wrongFiles.Count > 0)
        {
            string message = $"执行完成，但存在{wrongFiles.Count}个警告或错误文件，请仔细检查";
            string details = string.Join(Environment.NewLine, wrongFiles.Select(p =>
            {
                if (string.IsNullOrWhiteSpace(p.Message))
                {
                    return p.RelativePath;
                }

                return $"{p.RelativePath}（{p.Message}）";
            }));
            await DialogService.ShowWarningDialogAsync("存在警告", message, details);
        }
    }

    /// <summary>
    /// 执行后检查
    /// </summary>
    /// <param name="token"></param>
    /// <returns>如果不存在需要处理的文件，返回true</returns>
    private async Task CheckWarningFilesOnExecutedAsync(CancellationToken ct)
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

        await CheckWarningFiles(files);
    }

    /// <summary>
    /// 初始化后检查并抛出警告
    /// </summary>
    /// <param name="token"></param>
    /// <returns>如果不存在需要处理的文件，返回true</returns>
    private async Task<bool> CheckWarningFilesOnInitializedAsync(CancellationToken ct)
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

        await CheckWarningFiles(files);

        return false;
    }

    #endregion

    #region 按钮命令

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
            UpdateCommandExecutable(canInitialize: false);
        }
        else if (ExecuteCommand.IsRunning)
        {
            ExecuteCommand.Cancel();
            UpdateCommandExecutable(canExecute: false);
        }
    }

    /// <summary>
    /// 执行任务
    /// </summary>
    /// <param name="ct"></param>
    /// <exception cref="NullReferenceException"></exception>
    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync(CancellationToken ct)
    {
        if (!EnableInitialize)
        {
            AppConfig.SaveBackground();
            CreateService();
        }
        
        UpdateCommandExecutable(false, false, true, false);

        await TryRunServiceMethodAsync(async () =>
        {
            await OnExecutingAsync(ct);
            Config.Check();
            await Service.ExecuteAsync(ct);
            Service.Dispose();
            await OnExecutedAsync(ct);
            await CheckWarningFilesOnExecutedAsync(ct);
        }, "执行失败");

        UpdateCommandExecutable(null, EnableRepeatExecute, false, true);
    }

    /// <summary>
    /// 初始化任务
    /// </summary>
    /// <param name="ct"></param>
    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanInitialize))]
    private async Task InitializeAsync(CancellationToken ct)
    {
        AppConfig.SaveBackground();
        UpdateCommandExecutable(false,false,true,false);

        if (await TryRunServiceMethodAsync(async () =>
            {
                CreateService();
                await OnInitializingAsync();
                Config.Check();
                await Service.InitializeAsync(ct);
                await OnInitializedAsync();
            }, "初始化失败") //初始化成功
            && !await CheckWarningFilesOnInitializedAsync(ct)) //有需要处理的文件
        {
         
            UpdateCommandExecutable(false,true,false,true);
        }
        else
        {
            UpdateCommandExecutable(true,false,false,false);
        }
    }

    /// <summary>
    /// 重置
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReset))]
    private void Reset()
    {
        UpdateCommandExecutable(true,!EnableInitialize,false,false);

        Message = "就绪";
        OnReset();
        DisposeService();
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

    #endregion
}