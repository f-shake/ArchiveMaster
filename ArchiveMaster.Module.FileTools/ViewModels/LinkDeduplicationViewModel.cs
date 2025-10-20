using ArchiveMaster.Basic;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class LinkDeduplicationViewModel(ViewModelServices services)
    : TwoStepViewModelBase<LinkDeduplicationService, LinkDeduplicationConfig>(services)
{
    [ObservableProperty]
    private BulkObservableCollection<SimpleFileInfo> groups;

    protected override Task OnInitializedAsync()
    {
        Groups = new BulkObservableCollection<SimpleFileInfo>(Service.TreeRoot.SubDirs);
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Groups = null;
    }
}