using System.Collections.ObjectModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class FileCopyTestViewModel(ViewModelServices services)
    : TwoStepViewModelBase<FileCopyTestService, FileCopyTestConfig>(services)
{
    [ObservableProperty]
    private ObservableCollection<CopyingFile> files;

    protected override Task OnInitializedAsync()
    {
        Files = new ObservableCollection<CopyingFile>(Service.Files);
        return base.OnInitializedAsync();
    }
}