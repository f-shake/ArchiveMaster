using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class TextEncryptionViewModel : ViewModelBase
{
    public IClipboardService ClipboardService { get; }

    [ObservableProperty]
    private string result;

    [ObservableProperty]
    private TextSource source = new TextSource() { FromFile = false }; //敏感数据，不保存到配置文件

    private readonly ConcurrentQueue<Func<Task>> taskQueue = new ConcurrentQueue<Func<Task>>();

    public TextEncryptionViewModel(AppConfig appConfig, IDialogService dialogService,
        IClipboardService clipboardService) : base(dialogService)
    {
        ClipboardService = clipboardService;
        Config = appConfig.GetOrCreateConfigWithDefaultKey<TextEncryptionConfig>();
        Service = new TextEncryptionService(Config, appConfig);
        Source.PropertyChanged += ConfigOnPropertyChanged;
        Config.Password.PropertyChanged += ConfigOnPropertyChanged;
        Config.PropertyChanged += ConfigOnPropertyChanged;
    }

    public TextEncryptionConfig Config { get; }

    public TextEncryptionService Service { get; }

    private void ConfigOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TextSource.Text)
            or nameof(TextEncryptionConfig.Prefix)
            or nameof(TextEncryptionConfig.Suffix)
            or nameof(SecurePassword.Password))
        {
            ExecuteLastAsync();
        }
    }

    [RelayCommand]
    private Task CopyResultAsync()
    {
        return ClipboardService.SetTextAsync(Result);
    }

    [RelayCommand]
    private Task DecryptAsync()
    {
        return QueueTaskAsync(() => EncryptOrDecryptAsync(false));
    }

    [RelayCommand]
    private Task EncryptAsync()
    {
        return QueueTaskAsync(() => EncryptOrDecryptAsync(true));
    }

    private async Task EncryptOrDecryptAsync(bool encrypt)
    {
        Config.LastOperation = encrypt;
        try
        {
            if (encrypt)
            {
                await Service.EncryptAsync(Source);
            }
            else
            {
                await Service.DecryptAsync(Source);
            }

            Result = Service.ProcessedText;
        }
        catch (Exception ex)
        {
            await DialogService.ShowErrorDialogAsync($"{(encrypt ? "加密" : "解密")}失败", ex);
        }
    }

    private async Task ExecuteLastAsync()
    {
        if (Source.FromFile)
        {
            return;
        }

        if (Source.Text == null)
        {
            Result = null;
        }
        else if (Config.LastOperation.HasValue)
        {
            await QueueTaskAsync(() => EncryptOrDecryptAsync(Config.LastOperation.Value));
        }
    }

    private async Task QueueTaskAsync(Func<Task> func)
    {
        //由于触发器较多，可能存在上个任务未执行完毕，又触发了新的任务，因此使用队列来处理任务
        taskQueue.Enqueue(func);

        if (IsWorking)
        {
            return;
        }

        while (taskQueue.Count > 0)
        {
            IsWorking = true;
            try
            {
                while (taskQueue.Count > 1)
                {
                    //丢弃非最后一个任务
                    taskQueue.TryDequeue(out _);
                }

                if (taskQueue.TryDequeue(out var lastTaskFunc))
                {
                    await lastTaskFunc();
                }
            }
            catch (Exception ex)
            {
                //应该不会到这儿
                await DialogService.ShowErrorDialogAsync("执行任务时发生错误", ex);
            }
            finally
            {
                IsWorking = false;
            }
        }
    }
}