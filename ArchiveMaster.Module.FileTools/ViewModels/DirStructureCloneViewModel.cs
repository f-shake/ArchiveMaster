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
    DirStructureCloneViewModel(ViewModelServices services)
    : TwoStepViewModelBase<DirStructureCloneService, DirStructureCloneConfig>(services)
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
            await Services.Dialog.ShowErrorDialogAsync("��֧�ֵĲ���ϵͳ", "�ù���Ŀǰ��֧��Windowsϵͳ");
            Exit();
        }
    }

    protected override void OnReset()
    {
        TreeFiles = null;
    }
}