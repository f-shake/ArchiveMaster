using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class
    DirStructureCloneViewModel(ViewModelServices services)
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
            await Services.Dialog.ShowErrorDialogAsync("��֧�ֵĲ���ϵͳ", "�ù���Ŀǰ��֧��Windowsϵͳ");
            Exit();
        }
    }

    protected override void OnReset()
    {
        TreeFiles = null;
    }
}