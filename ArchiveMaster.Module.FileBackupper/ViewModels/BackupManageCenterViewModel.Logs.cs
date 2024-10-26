using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using ArchiveMaster.Configs;
using ArchiveMaster.Converters;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Messages;
using Microsoft.Extensions.Logging;

namespace ArchiveMaster.ViewModels;

public partial class BackupManageCenterViewModel
{
    [ObservableProperty]
    private ObservableCollection<BackupLogEntity> logs;

    [ObservableProperty]
    private LogLevel logType = LogLevel.None;

    [ObservableProperty]
    private string logSearchText;

    private List<BackupLogEntity> logList;

    int countPerPage = 50;

    private async Task LoadLogsAsync()
    {
        await using var db = new DbService(SelectedTask);
        logList = await db.GetLogsAsync(SelectedSnapshot.Snapshot.Id, LogType, LogSearchText);
        int pages = Math.Max((int)Math.Ceiling(1.0 * logList.Count / countPerPage), 1);

        LogPages = new ObservableCollection<int>(Enumerable.Range(1, pages));
        // if (LogPage != 1)
        // {
        //     LogPage = 1;
        // }
        // else
        // {
        //     OnLogPageChanged(1);
        // }
        // OnLogPageChanged(1);
    }


    partial void OnLogPagesChanged(ObservableCollection<int> value)
    {
        LogPage = -1;
        LogPage = 0;
        Logs = new ObservableCollection<BackupLogEntity>(logList.Take(countPerPage));
    }

    partial void OnLogPageChanged(int value)
    {
        Debug.WriteLine($"LogPage改变：{value}");
        if (value == -1)
        {
            Logs = null;
            return;
        }
        if (logList != null)
        {
            Logs = new ObservableCollection<BackupLogEntity>(logList.Skip(LogPage * countPerPage)
                .Take(countPerPage));
        }
    }


    [RelayCommand]
    private Task ShowDetailAsync(BackupLogEntity log)
    {
        return this.SendMessage(new CommonDialogMessage()
        {
            Type = CommonDialogMessage.CommonDialogType.Ok,
            Message = log.Message,
            Title = LogLevelConverter.GetDescription(log.Type),
            Detail = log.Detail
        }).Task;
    }

    [RelayCommand]
    private async Task SearchLogsAsync()
    {
        await TryDoAsync("加载日志", LoadLogsAsync);
    }


    [ObservableProperty]
    private ObservableCollection<int> logPages;

    [ObservableProperty]
    private int logPage = -1;
}