using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class TextEncryptionViewModel : ViewModelBase
{
    [ObservableProperty]
    private string result;

    [ObservableProperty]
    private TextSource source = new TextSource() { FromFile = false }; //敏感数据，不保存到配置文件
    
    public TextEncryptionViewModel(AppConfig appConfig, IDialogService dialogService) : base(dialogService)
    {
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
    private async Task DecryptAsync()
    {
        Config.LastOperation = false;
        await Service.DecryptAsync(Source);
        Result = Service.ProcessedText;
    }

    [RelayCommand]
    private async Task EncryptAsync()
    {
        Config.LastOperation = true;
        await Service.EncryptAsync(Source);
        Result = Service.ProcessedText;
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
        else
        {
            if (Config.LastOperation == true)
            {
                await EncryptAsync();
            }
            else
            {
                await DecryptAsync();
            }
        }
    }
}