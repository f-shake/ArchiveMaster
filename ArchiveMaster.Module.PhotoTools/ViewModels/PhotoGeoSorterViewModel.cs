using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PhotoGeoSorterViewModel(AppConfig appConfig, IDialogService dialogService)
    : TwoStepViewModelBase<PhotoGeoSorterService, PhotoGeoSorterConfig>(appConfig, dialogService)
{
    [ObservableProperty]
    public ObservableCollection<GpsFileInfo> files;

    protected override Task OnInitializedAsync()
    {
        Files = new ObservableCollection<GpsFileInfo>(Service.Files);
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Files = null;
    }
}