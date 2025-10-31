using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class RepairModifiedTimeViewModel(ViewModelServices services)
    : TwoStepViewModelBase<RepairModifiedTimeService, RepairModifiedTimeConfig>(services)
{
    [ObservableProperty]
    private List<ExifTimeFileInfo> files = new List<ExifTimeFileInfo>();

    protected override Task OnInitializedAsync()
    {
        Files = Service.Files.ToList();
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Files = new List<ExifTimeFileInfo>();
    }
}