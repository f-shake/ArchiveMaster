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
    private bool partial = false;

    [ObservableProperty]
    private List<TaggingPhotoFileInfo> files;

    [ObservableProperty]
    private bool hasLoaded = false;

    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        Files = Service.AllFiles;
        HasLoaded = true;
        return base.OnExecutedAsync(ct);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        Files = await Service.SearchAsync(TagType, SearchKeyword, Partial);
    }

    protected override void OnReset()
    {
        Files = null;
        SearchKeyword = "";
        HasLoaded = false;
    }
}