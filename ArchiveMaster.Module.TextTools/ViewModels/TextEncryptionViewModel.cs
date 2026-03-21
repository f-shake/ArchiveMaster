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
    [ObservableProperty]
    private string ciphertext;

    [ObservableProperty]
    private string plaintext;

    public TextEncryptionViewModel(ViewModelServices services) : base(services)
    {
        Config = Services.AppConfig.GetOrCreateConfigWithDefaultKey<TextEncryptionConfig>();
        Service = new TextEncryptionService(Config, services.AppConfig);
    }

    public TextEncryptionConfig Config { get; }

    public TextEncryptionService Service { get; }

    protected async override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (IsWorking || !Config.AutoFlush)
        {
            return;
        }

        if (e.PropertyName is nameof(Ciphertext))
        {
            await DecryptAsync();
        }
        else if (e.PropertyName is nameof(Plaintext))
        {
            await EncryptAsync();
        }
    }

    [RelayCommand]
    private async Task CopyPlaintextAsync()
    {
        await Services.Clipboard.SetTextAsync(Plaintext);
    }

    [RelayCommand]
    private async Task CopyCiphertextAsync()
    {
        await Services.Clipboard.SetTextAsync(Ciphertext);
    }

    [RelayCommand]
    private async Task DecryptAsync()
    {
        try
        {
            Config.Check();
            IsWorking = true;
            Plaintext = await Service.DecryptAsync(Ciphertext);
        }
        catch (Exception ex)
        {
            await Services.Dialog.ShowErrorDialogAsync("解密失败", ex);
        }
        finally
        {
            IsWorking = false;
        }
    }

    [RelayCommand]
    private async Task EncryptAsync()
    {
        try
        {
            Config.Check();
            IsWorking = true;
            Ciphertext = await Service.EncryptAsync(Plaintext);
        }
        catch (Exception ex)
        {
            await Services.Dialog.ShowErrorDialogAsync("加密失败", ex);
        }
        finally
        {
            IsWorking = false;
        }
    }

    [RelayCommand]
    private async Task PastePlaintextAsync()
    {
        Plaintext = await Services.Clipboard.GetTextAsync();
    }

    [RelayCommand]
    private async Task PasteCiphertextAsync()
    {
        Ciphertext = await Services.Clipboard.GetTextAsync();
    }
}