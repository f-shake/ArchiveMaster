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

public partial class EncryptorViewModel : TwoStepViewModelBase<EncryptorService, EncryptorConfig>
{
    public IClipboardService Clipboard { get; }

    [ObservableProperty]
    private bool isEncrypting = true;

    [ObservableProperty]
    private List<FileSystem.EncryptorFileInfo> processingFiles;

    public CipherMode[] CipherModes => Enum.GetValues<CipherMode>();

    public PaddingMode[] PaddingModes => Enum.GetValues<PaddingMode>();

    public EncryptorViewModel(AppConfig appConfig, IDialogService dialogService, IClipboardService clipboard) : base(
        appConfig, dialogService)
    {
        Clipboard = clipboard;
    }

    [RelayCommand]
    private async Task CopyErrorAsync(Exception exception)
    {
        await WeakReferenceMessenger.Default.Send(Clipboard.SetTextAsync(exception.ToString()));
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