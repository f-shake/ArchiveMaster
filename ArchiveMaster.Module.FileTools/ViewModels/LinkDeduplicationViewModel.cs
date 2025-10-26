using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class LinkDeduplicationViewModel(ViewModelServices services)
    : TwoStepViewModelBase<LinkDeduplicationService, LinkDeduplicationConfig>(services)
{
    [ObservableProperty]
    private AvaloniaList<SimpleFileInfo> groups;

    protected override Task OnInitializedAsync()
    {
        Groups = new AvaloniaList<SimpleFileInfo>(Service.TreeRoot.SubDirs);
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Groups = null;
    }
}