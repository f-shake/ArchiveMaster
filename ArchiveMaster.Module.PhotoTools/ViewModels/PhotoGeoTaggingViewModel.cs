using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PhotoGeoTaggingViewModel(ViewModelServices services)
    : TwoStepViewModelBase<PhotoGeoTaggingService, PhotoGeoTaggingConfig>(services)
{
    [ObservableProperty]
    private List<GpsFileInfo> files = new List<GpsFileInfo>();

    protected override Task OnInitializedAsync()
    {
        Files = [.. Service.Files];
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Files = new List<GpsFileInfo>();
    }
}