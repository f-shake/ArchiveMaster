using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ArchiveMaster.Basic;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class
    DirStructureCloneViewModel(AppConfig appConfig, IDialogService dialogService)
    : TwoStepViewModelBase<DirStructureCloneService, DirStructureCloneConfig>(appConfig, dialogService)
{
    [ObservableProperty]
    private BulkObservableCollection<SimpleFileInfo> treeFiles;

    protected override Task OnInitializedAsync()
    {
        var files = new BulkObservableCollection<SimpleFileInfo>();
        files.AddRange(Service.RootDir.Subs);
        TreeFiles = files;
        return base.OnInitializedAsync();
    }

    public override async void OnEnter()
    {
        base.OnEnter();
        if (!OperatingSystem.IsWindows())
        {
            await DialogService.ShowErrorDialogAsync("不支持的操作系统", "该工具目前仅支持Windows系统");
            Exit();
        }
    }

    protected override void OnReset()
    {
        TreeFiles = null;
    }
}