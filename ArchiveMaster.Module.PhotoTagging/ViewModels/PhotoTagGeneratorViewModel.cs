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

    protected override async Task OnInitializedAsync()
    {
        Files = new AvaloniaList<TaggingPhotoFileInfo>(Service.Files);
        if (Service.UnusedExistingTaggedPhotos.Count > 0)
        {
            await Services.Dialog.ShowWarningDialogAsync("标签文件中存在未匹配的文件",
                $"已保存的标签文件中，有{Service.UnusedExistingTaggedPhotos.Count}个标签未能匹配实际文件。这些标签将在开始生成后被弃用。",
                string.Join(Environment.NewLine, Service.UnusedExistingTaggedPhotos.Select(p => p.RelativePath)));
        }
    }

    protected override void OnReset()
    {
        Files = null;
    }
}