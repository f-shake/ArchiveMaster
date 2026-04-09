using System.ComponentModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Collections;
using Avalonia.Platform.Storage;
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
    private bool hasChanged;


    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        TreeFiles = new AvaloniaList<SimpleFileInfo>(Service.Tree.Subs);
        foreach (var file in Service.Tree.Flatten())
        {
            var editableTaggingFile = file.Tag as EditableTaggingFileInfo;
            if (editableTaggingFile == null)
            {
                throw new Exception($"文件{file.RelativePath}没有标签");
            }

            editableTaggingFile.TagsChanged += EditableTaggingFileOnTagsChanged;
        }

        return base.OnExecutedAsync(ct);
    }

    private void EditableTaggingFileOnTagsChanged(object sender, EventArgs e)
    {
        HasChanged = true;
    }

    protected override void OnReset()
    {
        base.OnReset();
        TreeFiles = null;
        HasChanged = false;
    }

    private async Task SaveTagsAsync(string tagFile)
    {
        HasChanged = false;
        await Services.ProgressOverlay.WithOverlayAsync(
            async ct => { await Service.SaveTagsAsync(tagFile, ct); }, null,
            async ex => { await Services.Dialog.ShowErrorDialogAsync("保存标签失败", ex); }, "正在保存标签");
    }

    [RelayCommand]
    private async Task SaveTagsAsync()
    {
        await SaveTagsAsync(Config.TagFile);
    }

    [RelayCommand]
    private async Task SaveTagsAsAsync()
    {
        var path = await Services.StorageProvider.CreatePickerBuilder()
            .AddFilter("ArchiveMaster Photo Tags（图像标签库）文件", "ampt")
            .SuggestedFileName(Path.GetFileNameWithoutExtension(Config.TagFile))
            .SaveFilePickerAndGetPathAsync();
        if (path == null)
        {
            return;
        }

        await SaveTagsAsync(path);
    }

    public override async Task OnExitAsync(CancelEventArgs args)
    {
        if (HasChanged)
        {
            var needSave = await Services.Dialog.ShowYesNoDialogAsync("标签已修改", "标签已被修改，是否保存？");
            if (needSave == true)
            {
                await SaveTagsAsync();
            }
        }

        await base.OnExitAsync(args);
    }
}