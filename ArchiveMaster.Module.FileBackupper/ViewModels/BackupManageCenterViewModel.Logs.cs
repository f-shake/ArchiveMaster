using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using ArchiveMaster.Configs;
using ArchiveMaster.Converters;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ArchiveMaster.ViewModels;

public partial class BackupManageCenterViewModel
{
    private const int PageSize =
#if DEBUG
        10;
#else
        100;
#endif

    [ObservableProperty]
    private int logPage = -1;

    [ObservableProperty]
    private ObservableCollection<int> logPages;

    [ObservableProperty]
    private ObservableCollection<BackupLogEntity> logs;

    [ObservableProperty]
    private string logSearchText;

    [ObservableProperty]
    private LogLevel logType = LogLevel.None;

    private PagedList<BackupLogEntity> pagedLogs;

    [ObservableProperty]
    private DateTime logTimeFrom;

    [ObservableProperty]
    private DateTime logTimeTo;

    [ObservableProperty]
    private BackupLogEntity lastLog;

    private async Task LoadLogsAsync()
    {
        try
        {
            await using var db = new DbService(SelectedTask);
            pagedLogs = await db.GetLogsAsync(SelectedSnapshot?.Id, LogType, LogSearchText, (LogTimeFrom, LogTimeTo), 0,
                PageSize);

            if (pagedLogs.PageCount == 0)
            {
                LogPages = null;
            }
            else
            {
                LogPages = new ObservableCollection<int>(Enumerable.Range(1, pagedLogs.PageCount));
                Logs = new ObservableCollection<BackupLogEntity>(pagedLogs.Items);
                LogPage = 0;
            }
        }
        catch (Exception ex)
        {
            LogPages = null;
            await Services.Dialog.ShowErrorDialogAsync("加载日志失败", ex);
        }
    }


    async partial void OnLogPageChanged(int value)
    {
        Debug.WriteLine($"LogPage改变：{value}");
        if (value < 0)
        {
            Logs = null;
            return;
        }

        await using var db = new DbService(SelectedTask);
        var logs = await db.GetLogsAsync(SelectedSnapshot?.Id, LogType, LogSearchText, (LogTimeFrom, LogTimeTo), value,
            PageSize);
        Logs = new ObservableCollection<BackupLogEntity>(logs.Items);
    }


    [RelayCommand]
    private async Task SearchLogsAsync()
    {
        await Services.ProgressOverlay.WithOverlayAsync(LoadLogsAsync,
            ex => Services.Dialog.ShowErrorDialogAsync("加载日志失败", ex),
            "正在加载日志");
    }

    [RelayCommand]
    private Task ShowDetailAsync(BackupLogEntity log)
    {
        return log == null
            ? Task.CompletedTask
            : Services.Dialog.ShowOkDialogAsync(Converters.Converters.LogMap.Map[log.Type.ToString()], log.Message,
                log.Detail);
    }
}