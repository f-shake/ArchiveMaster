using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PhotoTagGeneratorViewModel(ViewModelServices services)
    : TwoStepViewModelBase<PhotoTagGeneratorService, PhotoTagGeneratorConfig>(services)
{
    [ObservableProperty]
    private AvaloniaList<TaggingPhotoFileInfo> files = new AvaloniaList<TaggingPhotoFileInfo>();

    protected override Task OnInitializedAsync()
    {
        Files = new AvaloniaList<TaggingPhotoFileInfo>(Service.Files);
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Files = null;
    }
}