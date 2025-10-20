using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Basic;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class DuplicateFileCleanupViewModel(ViewModelServices services)
    : TwoStepViewModelBase<DuplicateFileCleanupService, DuplicateFileCleanupConfig>(services)
{
    [ObservableProperty]
    private BulkObservableCollection<SimpleFileInfo> groups;

    protected override Task OnInitializedAsync()
    {
        Groups = new BulkObservableCollection<SimpleFileInfo>(Service.DuplicateGroups.SubDirs);
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Groups = null;
    }
}