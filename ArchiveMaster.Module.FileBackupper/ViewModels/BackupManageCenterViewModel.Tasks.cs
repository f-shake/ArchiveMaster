using System.Collections.ObjectModel;
using System.ComponentModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArchiveMaster.ViewModels;

public partial class BackupManageCenterViewModel
{
    [ObservableProperty]
    private bool canCancelBackingUp = true;

    [ObservableProperty]
    private bool canMakeBackup = true;

    [ObservableProperty]
    private bool isTaskOperationEnable;

    [ObservableProperty]
    private BackupTask selectedTask;


    [ObservableProperty]
    private ObservableCollection<BackupTask> tasks;

    [RelayCommand(CanExecute = nameof(CanCancelBackingUp))]
    private async Task CancelMakingBackup()
    {
        await backupService.CancelCurrentAsync();
    }

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        FullSnapshots = null;
        SelectedTask = null;
        Tasks = new ObservableCollection<BackupTask>(Config.Tasks);
        await Tasks.UpdateStatusAsync();
    }

    [RelayCommand(CanExecute = nameof(CanMakeBackup))]
    private async Task MakeBackupAsync(SnapshotType type)
    {
        try
        {
            await backupService.MakeABackupAsync(SelectedTask, type);
        }
        catch (Exception ex)
        {
            await Services.Dialog.ShowErrorDialogAsync("备份失败", ex);
        }
    }


    async partial void OnSelectedTaskChanged(BackupTask oldValue, BackupTask newValue)
    {
        FullSnapshots = null;
        LastLog = null;

        if (oldValue != null)
        {
            oldValue.PropertyChanged -= SelectedBackupTaskPropertyChanged;
        }

        if (newValue != null)
        {
            await Services.ProgressOverlay.WithOverlayAsync(async () =>
                {
                    newValue.Check();
                    await LoadSnapshotsAsync();
                    LogTimeFrom = DateTime.Today;
                    LogTimeTo = DateTime.Today.AddDays(1);
                    await LoadLogsAsync();
                    SelectedTabIndex = 3;
                    await UpdateOperationsEnableAsync();
                    newValue.PropertyChanged += SelectedBackupTaskPropertyChanged;
                }, ex => Services.Dialog.ShowErrorDialogAsync("加载任务失败", ex),
                "正在加载任务");
        }
    }

    private async void SelectedBackupTaskPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (SelectedTask == null || sender != SelectedTask)
        {
            return;
        }

        if (e.PropertyName == nameof(BackupTask.Status))
        {
            await UpdateOperationsEnableAsync();
        }
    }

    private Task UpdateOperationsEnableAsync()
    {
        return Dispatcher.UIThread.InvokeAsync(async () =>
        {
            switch (SelectedTask?.Status)
            {
                case BackupTaskStatus.Ready:
                    CanMakeBackup = true;
                    CanCancelBackingUp = false;
                    IsTaskOperationEnable = true;
                    await SelectedTask.UpdateStatusAsync();
                    await LoadSnapshotsAsync();
                    break;
                case BackupTaskStatus.FullBackingUp:
                case BackupTaskStatus.IncrementBackingUp:
                    CanMakeBackup = false;
                    CanCancelBackingUp = true;
                    IsTaskOperationEnable = true;
                    break;
                default:
                    IsTaskOperationEnable = true;
                    return;
            }

            MakeBackupCommand.NotifyCanExecuteChanged();
            CancelMakingBackupCommand.NotifyCanExecuteChanged();
        });
    }
}