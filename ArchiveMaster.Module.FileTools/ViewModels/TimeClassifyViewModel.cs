using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class TimeClassifyViewModel(ViewModelServices services)
    : TwoStepViewModelBase<TimeClassifyService, TimeClassifyConfig>(services)
{
    [ObservableProperty]
    private List<FileSystem.FilesTimeDirInfo> sameTimePhotosDirs;

    protected override Task OnInitializedAsync()
    {
        SameTimePhotosDirs = Service.TargetDirs;
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        SameTimePhotosDirs = null;
    }
}