using ArchiveMaster.Basic;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Avalonia.Dialogs;
using System.Collections.ObjectModel;

namespace ArchiveMaster.ViewModels;

public partial class RebuildViewModel(ViewModelServices services)
    : TwoStepViewModelBase<RebuildService, RebuildConfig>(services,
        WriteOnceArchiveModuleInfo.CONFIG_GROUP)
{
    [ObservableProperty]
    private BulkObservableCollection<SimpleFileInfo> fileTree;

    [ObservableProperty]
    private ObservableCollection<WriteOnceFile> matchedFiles;

    [ObservableProperty]
    private RebuildInitializeReport report;

    protected override Task OnInitializedAsync()
    {
        FileTree = new BulkObservableCollection<SimpleFileInfo>(Service.FileTree.Subs);
        MatchedFiles = new ObservableCollection<WriteOnceFile>(Service.MatchedFiles);
        Report = Service.InitializeReport;
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        base.OnReset();
        FileTree = null;
        MatchedFiles = null;
        Report = null;
    }
}