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

public partial class FileFilterOperationViewModel(ViewModelServices services)
    : TwoStepViewModelBase<FileFilterOperationService, FileFilterOperationConfig>(services)
{
    [ObservableProperty]
    private List<FileFilterOperationFileInfo> files;

    protected override async Task OnInitializedAsync()
    {
        Files = Service.Files;
        if (Config.Type is FileFilterOperationType.Copy or FileFilterOperationType.Move
            or FileFilterOperationType.HardLink or FileFilterOperationType.SymbolLink)
        {
            var duplicateTargetNameFiles = Files
                .GroupBy(p => p.TargetPath)
                .Where(p => p.Count() > 1)
                .Select(p => p.Key)
                .ToList();
            if (duplicateTargetNameFiles.Count > 0)
            {
                await Services.Dialog.ShowWarningDialogAsync("存在重复目标文件",
                    $"存在{duplicateTargetNameFiles.Count}个重复的目标文件。若强制执行，部分文件会自动重命名。",
                    string.Join(Environment.NewLine, duplicateTargetNameFiles));
            }
        }
    }

    protected override void OnReset()
    {
        Files = null;
    }
}