using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PhotoTagManagerViewModel(ViewModelServices services)
    : TwoStepViewModelBase<PhotoTagManagerService, PhotoTagManagerConfig>(services)
{
    public override bool EnableInitialize => false;

    [ObservableProperty]
    private AvaloniaList<TreeFileDirInfo> treeFiles;

    [ObservableProperty]
    private TreeFileDirInfo selectedFile;
    
    [ObservableProperty]
    private ObservablePhotoTags selectedTags;

    partial void OnSelectedFileChanged(TreeFileDirInfo value)
    {
        if (value == null)
        {
            SelectedTags = null;
            return;
        }

        if (value.Tag is TaggingPhotoFileInfo tagPhoto)
        {
            SelectedTags = new ObservablePhotoTags(tagPhoto.Tags);
        }
        else
        {
            SelectedTags = new ObservablePhotoTags();
        }
        
    }

    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        TreeFiles = new AvaloniaList<TreeFileDirInfo>(Service.Tree.Subs);
        return base.OnExecutedAsync(ct);
    }

    protected override void OnReset()
    {
        base.OnReset();
        TreeFiles = null;
    }
}