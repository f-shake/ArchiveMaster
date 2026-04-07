using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PhotoTagSearcherViewModel(ViewModelServices services)
    : TwoStepViewModelBase<PhotoTagSearcherService, PhotoTagSearcherConfig>(services)
{
    public override bool EnableInitialize => false;

    [ObservableProperty]
    private TagType tagType = TagType.All;

    [ObservableProperty]
    private string searchKeyword = "";

    [ObservableProperty]
    private List<TaggingPhotoFileInfo> files;

    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        Files = Service.Files;
        return base.OnExecutedAsync(ct);
    }
}