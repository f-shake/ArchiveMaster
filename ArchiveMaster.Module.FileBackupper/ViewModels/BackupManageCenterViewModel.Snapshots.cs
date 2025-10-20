using System.Collections.ObjectModel;
using ArchiveMaster.Basic;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ArchiveMaster.ViewModels;

public partial class BackupManageCenterViewModel
{
    [ObservableProperty]
    private BackupSnapshotEntity selectedSnapshot;

    [ObservableProperty]
    private FullSnapshotItem selectedFullSnapshot;

    [ObservableProperty]
    private ObservableCollection<FullSnapshotItem> fullSnapshots;

    [ObservableProperty]
    private int totalSnapshotCount;

    async partial void OnSelectedSnapshotChanged(BackupSnapshotEntity value)
    {
        if (value == null)
        {
            Logs = null;
            TreeFiles = null;
            FileHistory = null;
            SelectedFile = null;
            CreatedFiles = null;
            ModifiedFiles = null;
            DeletedFiles = null;
            return;
        }

        await Services.ProgressOverlay.WithOverlayAsync(async () =>
            {
                SelectedTabIndex = 0;
                LogSearchText = null;
                LogType = LogLevel.None;
                LogTimeFrom = value.BeginTime.AddHours(-1);
                LogTimeTo = value.EndTime.AddHours(1);
                await LoadLogsAsync();
                await LoadFilesAsync();
                await LoadFileChangesAsync();
            }, ex => Services.Dialog.ShowErrorDialogAsync("加载快照详情失败", ex),
            "正在加载快照详情");
    }


    [RelayCommand]
    private async Task LoadSnapshotsAsync()
    {
        try
        {
            DbService db = new DbService(SelectedTask);
            var snapshots = await db.GetSnapshotsAsync();
            FullSnapshotItem fullSnapshot = null;
            FullSnapshots = new ObservableCollection<FullSnapshotItem>();
            foreach (var snapshot in snapshots)
            {
                switch (snapshot.Type)
                {
                    case SnapshotType.Full:
                    case SnapshotType.VirtualFull:
                        fullSnapshot = new FullSnapshotItem(snapshot);
                        FullSnapshots.Add(fullSnapshot);
                        break;
                    case SnapshotType.Increment:
                        if (fullSnapshot == null)
                        {
                            throw new Exception($"增量快照{snapshot.BeginTime}前没有全量快照");
                        }

                        fullSnapshot.Snapshots.Add(snapshot);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (FullSnapshots.Count > 0)
            {
                SelectedFullSnapshot = FullSnapshots[^1];
            }
        }
        catch (Exception)
        {
            SelectedTask = null;
            throw;
        }
    }

    [RelayCommand]
    private async Task DeleteSnapshotAsync(BackupSnapshotEntity snapshot)
    {
        if (backupService.IsBackingUp)
        {
            await Services.Dialog.ShowErrorDialogAsync("删除快照", $"目前有任务正在备份，无法删除快照");
            return;
        }

        string message = null;
        int index = SelectedFullSnapshot.Snapshots.IndexOf(snapshot);
        if (SelectedFullSnapshot.Snapshots.Count <= 1 || index == SelectedFullSnapshot.Snapshots.Count - 1) //最后一个，可以直接删
        {
            message = "是否删除此快照？";
        }
        else
        {
            message = "删除此快照，将同步删除后续的增量快照，是否删除此快照？";
        }

        bool confirm = true.Equals(await Services.Dialog.ShowYesNoDialogAsync("删除快照", message));

        if (confirm)
        {
            await Services.ProgressOverlay.WithOverlayAsync(async () =>
                {
                    ThrowIfIsBackingUp();
                    await using var db = new DbService(SelectedTask);
                    await db.DeleteSnapshotAsync(snapshot);
                    await LoadSnapshotsAsync();
                }, ex => Services.Dialog.ShowErrorDialogAsync("删除快照失败", ex),
                "正在删除快照");
        }
    }
}