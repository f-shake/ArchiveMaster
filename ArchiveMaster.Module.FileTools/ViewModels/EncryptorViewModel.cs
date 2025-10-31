using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Enums;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;

namespace ArchiveMaster.ViewModels;

public partial class EncryptorViewModel(ViewModelServices services)
    : TwoStepViewModelBase<EncryptorService, EncryptorConfig>(services)
{
    [ObservableProperty]
    private bool isEncrypting = true;

    [ObservableProperty]
    private List<FileSystem.EncryptorFileInfo> processingFiles;

    public CipherMode[] CipherModes => Enum.GetValues<CipherMode>();

    public PaddingMode[] PaddingModes => Enum.GetValues<PaddingMode>();

    [RelayCommand]
    private Task CopyErrorAsync(Exception exception)
    {
        return Services.Clipboard.SetTextAsync(exception.ToString());
    }

    protected override Task OnInitializingAsync()
    {
        Config.Type = IsEncrypting
            ? EncryptorConfig.EncryptorTaskType.Encrypt
            : EncryptorConfig.EncryptorTaskType.Decrypt;
        return base.OnInitializingAsync();
    }


    protected override Task OnInitializedAsync()
    {
        ProcessingFiles = Service.ProcessingFiles;
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        ProcessingFiles = null;
    }
}