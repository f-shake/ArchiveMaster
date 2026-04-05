using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PhotoTagGeneratorViewModel(ViewModelServices services)
    : TwoStepViewModelBase<PhotoTagGeneratorService, PhotoTagGeneratorConfig>(services)
{
    [ObservableProperty]
    private List<TaggingPhotoFileInfo> files = new List<TaggingPhotoFileInfo>();

    protected override Task OnInitializedAsync()
    {
        Files = Service.Files;
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Files = null;
    }
}