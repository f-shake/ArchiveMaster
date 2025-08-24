using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ArchiveMaster.Services;

public partial class BackupService(AppConfig config)
{
    private CancellationToken ct;

    private CancellationTokenSource cts;

    public static event EventHandler<BackupLogEventArgs> NewLog;

    public FileBackupperConfig Config { get; } = config.GetOrCreateConfigWithDefaultKey<FileBackupperConfig>();

    public bool IsAutoBackingUp { get; private set; }

    public bool IsBackingUp { get; private set; }

    public Task CancelCurrentAsync()
    {
        return cts?.CancelAsync() ?? Task.CompletedTask;
    }

    public async Task CheckAndBackupAllAsync()
    {
        if (!Config.EnableBackgroundBackup)
        {
            return;
        }

        if (IsBackingUp)
        {
            Log.Information("正在备份，无法进行新一轮的检查和执行");
            return;
        }

        IsBackingUp = true;

        try
        {
            foreach (var task in Config.Tasks
                         .Where(p => p.Status is BackupTaskStatus.Ready or BackupTaskStatus.Error)
                         .Where(p => p.ByTimeInterval)
                         .ToList())
            {
                ct.ThrowIfCancellationRequested();

                if (!Config.Tasks.Contains(task))
                {
                    continue; //防止在长时间备份时，任务被删除
                }

                var interval = task.TimeInterval;
                if (interval.TotalMinutes < 1)
                {
                    //防止间隔被设置得很短
                    interval = TimeSpan.FromMinutes(1);
                }


                if (task.LastBackupTime + interval > DateTime.Now) //下一次备份时间还没到
                {
                    continue;
                }

                //开始备份
                await using var db = new DbService(task);
                await db.LogAsync(LogLevel.Information, $"根据间隔时间备份规则，已到应备份时间");
                BackupEngine engine = new BackupEngine(task);

                var fullSnapshot =
                    await db.GetLastSnapshotAsync([SnapshotType.VirtualFull, SnapshotType.Full], ct);

                try
                {
                    if (fullSnapshot == null)
                    {
                        await db.LogAsync(LogLevel.Information,
                            $"未找到全量备份快照，即将开始{(task.IsDefaultVirtualBackup ? "虚拟" : "")}全量备份");
                        await engine.BackupAsync(
                            task.IsDefaultVirtualBackup ? SnapshotType.VirtualFull : SnapshotType.Full, ct);
                    }
                    else
                    {
                        var snapshotCountAfterLastFullSnapshot = await db.GetSnapshotCountAsync(
                            otherQueryAction: q => { return q.Where(p => p.BeginTime > fullSnapshot.BeginTime); },
                            ct: ct);
                        if (snapshotCountAfterLastFullSnapshot > task.MaxAutoIncrementBackupCount)
                        {
                            await db.LogAsync(LogLevel.Information,
                                $"最后一次全量备份后的增量备份数量已超过允许值，即将开始{(task.IsDefaultVirtualBackup ? "虚拟" : "")}全量备份");
                            await engine.BackupAsync(
                                task.IsDefaultVirtualBackup ? SnapshotType.VirtualFull : SnapshotType.Full, ct);
                        }
                        else
                        {
                            await engine.BackupAsync(SnapshotType.Increment, ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "定时备份失败");
                }
            }
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    public async Task MakeABackupAsync(BackupTask task, SnapshotType type)
    {
        if (IsBackingUp)
        {
            throw new InvalidOperationException("存在正在进行的备份任务，无法开始新的备份");
        }

        IsBackingUp = true;
        CreateCancellationToken();

        try
        {
            await using var db = new DbService(task);
            if (type == SnapshotType.Increment)
            {
                var hasFullSnapshot = (await db.GetSnapshotsAsync(SnapshotType.Full, ct: ct)).Count != 0
                                      || (await db.GetSnapshotsAsync(SnapshotType.VirtualFull, ct: ct)).Count != 0;
                if (!hasFullSnapshot)
                {
                    throw new ArgumentException("没有找到全量备份，无法进行增量备份");
                }
            }

            BackupEngine engine = new BackupEngine(task);
            await engine.BackupAsync(type, ct);
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    public void StartAutoBackup()
    {
        _ = Task.Run(async () =>
           {
               try
               {
                   foreach (var task in Config.Tasks)
                   {
                       await task.UpdateStatusAsync();
                   }

                   IsAutoBackingUp = true;
                   while (IsAutoBackingUp)
                   {
                       try
                       {
#if DEBUG
                           await Task.Delay(10000);
#else
                           await Task.Delay(60 * 1000);
#endif
                           if (!IsBackingUp)
                           {
                               CreateCancellationToken();
                               await CheckAndBackupAllAsync();
                           }
                       }
                       catch (OperationCanceledException)
                       {
                           Log.Information("循环备份任务被单次取消，等待下次一次循环");
                       }
                       catch (Exception ex)
                       {
                           Log.Error(ex, "检查和备份任务出错");
                       }
                   }
               }
               catch (Exception ex)
               {
                   Log.Error(ex, "循环备份任务执行出错，已退出自动备份");
               }
               finally
               {
                   IsAutoBackingUp = false;
               }
           });
    }

    public Task StopAutoBackupAsync()
    {
        IsAutoBackingUp = false;
        return CancelCurrentAsync();
    }

    private void CreateCancellationToken()
    {
        cts = new CancellationTokenSource();
        ct = cts.Token;
    }
}