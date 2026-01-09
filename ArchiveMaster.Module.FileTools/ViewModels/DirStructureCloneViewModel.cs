using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class DirStructureCloneViewModel(ViewModelServices services)
    : TwoStepViewModelBase<DirStructureCloneService, DirStructureCloneConfig>(services)
{
    [ObservableProperty]
    private AvaloniaList<SimpleFileInfo> treeFiles;

    protected override Task OnInitializedAsync()
    {
        var files = new AvaloniaList<SimpleFileInfo>();
        files.AddRange(Service.RootDir.Subs);
        TreeFiles = files;
        return base.OnInitializedAsync();
    }

    public override async void OnEnter()
    {
        base.OnEnter();
        if (!OperatingSystem.IsWindows())
        {
            await Services.Dialog.ShowErrorDialogAsync("无法运行该工具", "该工具依赖Windows的API，当前系统不支持运行");
            Exit();
        }
    }

    protected override void OnReset()
    {
        TreeFiles = null;
    }

    protected override void OnCurrentConfigChanged(DirStructureCloneConfig oldConfig, DirStructureCloneConfig newConfig)
    {
        if (oldConfig != null)
        {
            oldConfig.PropertyChanged -= ConfigOnPropertyChanged;
        }

        if (newConfig != null)
        {
            newConfig.PropertyChanged += ConfigOnPropertyChanged;
        }

        void ConfigOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DirStructureCloneConfig.InputStructureFile))
            {
                (sender as DirStructureCloneConfig).SourceDirOrFile = null;
            }
            else if (e.PropertyName == nameof(DirStructureCloneConfig.ExportStructureFile))
            {
                (sender as DirStructureCloneConfig).TargetDirOrFile = null;
            }
        }
    }
}