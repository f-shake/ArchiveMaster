using System.Collections;
using ArchiveMaster.Basic;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Avalonia.Dialogs;
using System.Collections.ObjectModel;
using ArchiveMaster.Enums;

namespace ArchiveMaster.ViewModels;

public partial class VerifyViewModel(ViewModelServices services)
    : TwoStepViewModelBase<VerifyService, VerifyConfig>(services,
        WriteOnceArchiveModuleInfo.CONFIG_GROUP)
{
    [ObservableProperty]
    private BulkObservableCollection<SimpleFileInfo> fileTree;

    [ObservableProperty]
    private ObservableCollection<WriteOnceFile> files;

    [ObservableProperty]
    private RebuildInitializeReport report;

    protected override Task OnInitializedAsync()
    {
        FileTree = new BulkObservableCollection<SimpleFileInfo>(Service.FileTree.Subs);
        IComparer<ProcessStatus> comparer = Comparer<ProcessStatus>.Create((x, y) =>
        {
            int Order(ProcessStatus status) => status switch
            {
                ProcessStatus.Error => 0,
                ProcessStatus.Warn => 1,
                ProcessStatus.Success => 2,
                ProcessStatus.Skip => 3,
                ProcessStatus.Processing => 4,
                ProcessStatus.Ready => 5,
                _ => int.MaxValue
            };

            return Order(x).CompareTo(Order(y));
        });
        Files = new ObservableCollection<WriteOnceFile>(Service.Files.OrderBy(p => p.Status, comparer));
        Report = Service.Report;
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        base.OnReset();
        FileTree = null;
        Files = null;
        Report = null;
    }
}