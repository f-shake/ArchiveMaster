using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
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
    private AvaloniaList<SimpleFileInfo> treeFiles;

    [ObservableProperty]
    private TreeFileDirInfo selectedFile;
    
    [ObservableProperty]
    private ObservablePhotoTags selectedTags;

    partial void OnSelectedFileChanged(TreeFileDirInfo oldValue, TreeFileDirInfo newValue)
    {
        if (SelectedTags != null && oldValue != null)
        {
            oldValue.Tag=selectedTags.ToPhotoTags();
        }
        
        if (newValue == null)
        {
            SelectedTags = null;
            return;
        }

        if (newValue.Tag is PhotoTags tagPhoto)
        {
            SelectedTags = new ObservablePhotoTags(tagPhoto);
        }
        else
        {
            SelectedTags = new ObservablePhotoTags();
        }
    }

    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        TreeFiles = new AvaloniaList<SimpleFileInfo>(Service.Tree.Subs);
        return base.OnExecutedAsync(ct);
    }

    protected override void OnReset()
    {
        base.OnReset();
        TreeFiles = null;
    }
}