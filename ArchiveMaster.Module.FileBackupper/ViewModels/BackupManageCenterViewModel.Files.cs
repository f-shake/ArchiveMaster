using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using ArchiveMaster.Views;
using Avalonia.Collections;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArchiveMaster.ViewModels;

public partial class BackupManageCenterViewModel
{
    [ObservableProperty]
    private ObservableCollection<BackupFile> createdFiles;

    [ObservableProperty]
    private ObservableCollection<BackupFile> deletedFiles;

    [ObservableProperty]
    private ObservableCollection<BackupFile> fileHistory;

    [ObservableProperty]
    private ObservableCollection<BackupFile> modifiedFiles;

    [ObservableProperty]
    private SimpleFileInfo selectedFile;

    [ObservableProperty]
    private AvaloniaList<SimpleFileInfo> treeFiles;

    private async Task LoadFileChangesAsync()
    {
        await using var db = new DbService(SelectedTask);
        var (created, modified, deleted) = await db.GetSnapshotChanges(SelectedSnapshot.Id);
        CreatedFiles = new ObservableCollection<BackupFile>(created.Select(p => new BackupFile(p)));
        ModifiedFiles = new ObservableCollection<BackupFile>(modified.Select(p => new BackupFile(p)));
        DeletedFiles = new ObservableCollection<BackupFile>(deleted.Select(p => new BackupFile(p)));
    }

    private async Task LoadFileHistoryAsync(SimpleFileInfo file)
    {
        Debug.Assert(file is BackupFile);
        await using var db = new DbService(SelectedTask);
        var history = await db.GetFileHistory(file.RelativePath);
        FileHistory = new ObservableCollection<BackupFile>(history.Select(p => new BackupFile(p)));
    }

    private async Task LoadFilesAsync()
    {
        var Service = new RestoreService(SelectedTask);
        var tree = await Service.GetSnapshotFileTreeAsync(SelectedSnapshot.Id);
        tree.Reorder();
        tree.Name = $"快照{SelectedSnapshot.BeginTime}";

        TreeFiles = new AvaloniaList<SimpleFileInfo>();
        TreeFiles.Add(tree);
    }

    // [RelayCommand]

    async partial void OnSelectedFileChanged(SimpleFileInfo value)
    {
        if (value is BackupFile)
        {
            await Services.ProgressOverlay.WithOverlayAsync(async () => await LoadFileHistoryAsync(value),
                ex => Services.Dialog.ShowErrorDialogAsync("加载文件历史记录失败", ex),
                "正在加载文件历史记录");
        }
    }

    [RelayCommand]
    private async Task SaveAsAsync(SimpleFileInfo fileOrDir)
    {
        switch (fileOrDir)
        {
            case BackupFile file:
                await SaveFile(file);
                break;
            case TreeDirInfo dir:
                await SaveFolder(dir);
                break;
        }
    }

    private async Task SaveFile(BackupFile file)
    {
        if (file.Entity.BackupFileName == null)
        {
            await Services.Dialog.ShowErrorDialogAsync("备份文件不存在", "该文件不存在实际备份文件，可能是由虚拟快照生成");
            return;
        }

        var extension = Path.GetExtension(file.Name).TrimStart('.');
        var saveFile = await Services.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions()
            {
                DefaultExtension = extension,
                SuggestedFileName = file.Name,
                FileTypeChoices =
                [
                    new FilePickerFileType($"{extension}文件")
                        { Patterns = [$"*.{(extension.Length == 0 ? "*" : extension)}"] }
                ]
            });

        var path = saveFile?.TryGetLocalPath();
        if (path != null)
        {
            var dialog = new FileProgressDialog();
            string backupFile = Path.Combine(SelectedTask.BackupDir, file.Entity.BackupFileName);
            if (!File.Exists(backupFile))
            {
                await Services.Dialog.ShowErrorDialogAsync("备份文件不存在", "该文件不存在实际备份文件，可能是文件丢失");
                return;
            }

            var task1 = Services.Dialog.ShowCustomDialogAsync(dialog);
            var task2 = dialog.CopyFileAsync(backupFile, path, file.Time);
            await Task.WhenAll(task1, task2);
        }
    }

    private async Task SaveFolder(TreeDirInfo dir)
    {
        var rootDir = await Services.StorageProvider.OpenFolderPickerAndGetFirstAsync(new FolderPickerOpenOptions());
        var files = dir.Flatten();
        List<string> sourcePaths = new List<string>();
        List<string> destinationPaths = new List<string>();
        List<DateTime> times = new List<DateTime>();
        List<string> notExistedFiles = new List<string>();
        foreach (var file in files.Cast<BackupFile>())
        {
            if (file.Entity.BackupFileName == null)
            {
                notExistedFiles.Add(file.Entity.RawFileRelativePath);
                continue;
            }

            string backupFile = Path.Combine(SelectedTask.BackupDir, file.Entity.BackupFileName);
            if (!File.Exists(backupFile))
            {
                notExistedFiles.Add(file.Entity.RawFileRelativePath);
                continue;
            }

            string fileRelativePath = dir.RelativePath == null
                ? file.RelativePath
                : Path.GetRelativePath(dir.RelativePath, file.RelativePath);
            string destinationPath = Path.Combine(rootDir, fileRelativePath);
            sourcePaths.Add(backupFile);
            destinationPaths.Add(destinationPath);
            times.Add(file.Time);
        }

        bool copy = true;
        if (notExistedFiles.Count > 0)
        {
            copy = await Services.Dialog.ShowYesNoDialogAsync("部分文件不存在", "部分文件不存在实际备份文件，可能是虚拟备份或文件丢失。是否另存为其余文件？",
                string.Join(Environment.NewLine, notExistedFiles)) == true;
        }

        if (copy)
        {
            var dialog = new FileProgressDialog();
            var task1 = Services.Dialog.ShowCustomDialogAsync(dialog);
            var task2 = dialog.CopyFilesAsync(sourcePaths, destinationPaths, times);
            await Task.WhenAll(task1, task2);
        }
    }

    [RelayCommand]
    private async Task OrganizeFilesAsync()
    {
        BackupFilesCheckResult issuedFiles = null;
        await using var db = new DbService(SelectedTask);
        await Services.ProgressOverlay.WithOverlayAsync(async ct => { issuedFiles = await db.CheckFilesAsync(ct); },
            null,
            ex => Services.Dialog.ShowErrorDialogAsync("整理文件失败", ex),
            "正在整理文件");
        if (issuedFiles.RedundantFiles.Count + issuedFiles.LostFileItems.Count == 0)
        {
            await Services.Dialog.ShowOkDialogAsync("检查文件", "不存在多余或丢失的备份文件");
            return;
        }

        var result = await Services.Dialog.ShowYesNoDialogAsync("检查文件",
            $"存在{issuedFiles.RedundantFiles.Count}个多余文件；{Environment.NewLine}存在{issuedFiles.LostFileItems.Count}个丢失备份文件的记录{Environment.NewLine}是否删除多余文件并修复数据库？",
            $"多余文件（在备份文件夹中存在但数据库中不存在的文件）：{Environment.NewLine}"
            + string.Join(Environment.NewLine, issuedFiles.RedundantFiles.Select(p => p.Name))
            + $"{Environment.NewLine}{Environment.NewLine}丢失文件（被数据库记录但无物理文件，会导致恢复失败）：{Environment.NewLine}"
            + string.Join(Environment.NewLine,
                issuedFiles.LostFileItems.Select(p => $"{p.RawFileRelativePath}，快照编号：{p.SnapshotId}")));

        if (true.Equals(result))
        {
            await Services.ProgressOverlay.WithOverlayAsync(
                () => Task.Run(async () => { await db.DeleteRedundantFilesAndRemoveLostFileItemsAsync(issuedFiles); }),
                ex => Services.Dialog.ShowErrorDialogAsync("删除多余文件失败", ex),
                "正在整理文件");
        }
    }

    [RelayCommand]
    private async Task CopyAsync(object obj)
    {
        await Services.Clipboard.SetTextAsync(obj as string ?? (obj?.ToString() ?? ""));
    }
}