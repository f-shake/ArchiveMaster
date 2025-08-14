using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class FileFilterOperationViewModel(AppConfig appConfig, IDialogService dialogService)
    : TwoStepViewModelBase<FileFilterOperationService, FileFilterOperationConfig>(appConfig, dialogService)
{
    [ObservableProperty]
    private List<FileFilterOperationFileInfo> files;

    protected override async Task OnInitializedAsync()
    {
        Files = Service.Files;
        var duplicateTargetNameFiles = Files
            .GroupBy(p => p.TargetPath)
            .Where(p=>p.Count()>1)
            .Select(p => p.Key)
            .ToList();
        if (duplicateTargetNameFiles.Count > 0)
        {
            await DialogService.ShowWarningDialogAsync("存在重复目标文件",
                $"存在{duplicateTargetNameFiles.Count}个重复的目标文件。若强制执行，部分文件会自动重命名。",
                string.Join(Environment.NewLine, duplicateTargetNameFiles));
        }
    }

    protected override void OnReset()
    {
        Files = null;
    }
}