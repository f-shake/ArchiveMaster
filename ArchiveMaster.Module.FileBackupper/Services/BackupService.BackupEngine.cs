using System.Buffers;
using System.Diagnostics;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using FzLib.Avalonia.Converters;
using FzLib.IO;
using Microsoft.Extensions.Logging;

namespace ArchiveMaster.Services;

public partial class BackupService
{
    class BackupEngine(BackupTask backupTask)
    {
        public BackupTask BackupTask { get; } = backupTask;

        public async Task BackupAsync(SnapshotType type, CancellationToken cancellationToken = default)
        {
            if (BackupTask.Status is BackupTaskStatus.FullBackingUp or BackupTaskStatus.IncrementBackingUp)
            {
                throw new InvalidOperationException("任务正在备份中，无法进行备份");
            }

            bool succeed = false;

            await Task.Run(async () =>
            {
                await using var db = new DbService(BackupTask);
                BackupSnapshotEntity snapshot = new BackupSnapshotEntity()
                {
                    BeginTime = DateTime.Now,
                    Type = type
                };

                try
                {
                    BackupTask.BeginBackup(type);

                    db.Add(snapshot);
                    await db.SaveChangesAsync(cancellationToken);
                    await LogAsync(db, LogLevel.Information, $"开始备份，模式：{DescriptionConverter.GetDescription(type)}",
                        snapshot);
                    await LogAsync(db, LogLevel.Information, $"正在枚举文件", snapshot);
                    var files = GetSourceFiles(cancellationToken);
                    await LogAsync(db, LogLevel.Information, $"完成枚举磁盘文件，共{files.Count}个", snapshot);

                    if (type is SnapshotType.Increment)
                    {
                        await HandleIncrementalBackupAsync(db, snapshot, files, cancellationToken);
                    }
                    else
                    {
                        await HandleFullBackupAsync(db, snapshot, files, type is SnapshotType.VirtualFull,
                            cancellationToken);
                    }

                    snapshot.EndTime = DateTime.Now;
                    await db.SaveChangesAsync(cancellationToken);
                    await LogAsync(db, LogLevel.Information, "备份完成", snapshot);
                    succeed = true;
                }
                catch (OperationCanceledException)
                {
                    await LogAsync(db, LogLevel.Error, $"备份被中止", snapshot);
                }
                catch (Exception ex)
                {
                    await LogAsync(db, LogLevel.Error, $"备份过程中出现错误：{ex.Message}", snapshot, ex.ToString());
                    throw;
                }
                finally
                {
                    BackupTask.EndBackup(type is SnapshotType.Full or SnapshotType.VirtualFull, succeed);
                }
            }, cancellationToken);
        }

        private async static Task LogAsync(DbService db, LogLevel logLevel, string message,
            BackupSnapshotEntity snapshot = null, string detail = null)
        {
            var log = await db.LogAsync(logLevel, message, snapshot, detail);
            NewLog?.Invoke(null, new BackupLogEventArgs(db.BackupTask, log));
        }

        private async Task CreateNewBackupFileAsync(DbService db, BackupSnapshotEntity snapshot,
            FileInfo file, FileRecordType recordType, bool isVirtual, CancellationToken cancellationToken)
        {
            Debug.WriteLine(cancellationToken.GetHashCode());
            if (recordType == FileRecordType.Deleted)
            {
                throw new ArgumentException("不支持删除类型", nameof(recordType));
            }

            string rawRelativeFilePath = Path.GetRelativePath(BackupTask.SourceDir, file.FullName);
            Debug.WriteLine(rawRelativeFilePath);

            BackupFileEntity dbFile = new BackupFileEntity()
            {
                Snapshot = snapshot,
                RawFileRelativePath = rawRelativeFilePath,
                Type = recordType,
                Length = file.Length,
                Time = file.LastWriteTime,
            };

            if (!isVirtual)
            {
                dbFile.BackupFileName = Guid.NewGuid().ToString("N");
                string backupFilePath = Path.Combine(BackupTask.BackupDir, dbFile.BackupFileName);
                dbFile.Hash = await FileHashHelper.ComputeHashStringAsync(file.FullName, cancellationToken: cancellationToken);

                var existedFile = db.GetSameFile(file.LastWriteTime, file.Length, dbFile.Hash);
                if (existedFile != null) //已经存在一样的物理文件了
                {
                    await LogAsync(db, LogLevel.Information,
                        $"文件{rawRelativeFilePath}找到了已经存在的相同物理文件{dbFile.BackupFileName}", snapshot);
                    //File.Delete(backupFilePath);
                    dbFile.BackupFileName = existedFile.BackupFileName;
                }
                else
                {
                    await FileCopyHelper.CopyFileAsync(file.FullName, backupFilePath,
                        cancellationToken: cancellationToken);
                }
            }

            db.Add(dbFile);
        }


        private List<FileInfo> GetSourceFiles(CancellationToken cancellationToken)
        {
            var files = new DirectoryInfo(BackupTask.SourceDir)
                .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                .ApplyFilter(cancellationToken, BackupTask.Filter)
                .ToList();
            return files;
        }

        private async Task HandleFullBackupAsync(DbService db, BackupSnapshotEntity snapshot, List<FileInfo> files,
            bool isVirtualFull, CancellationToken cancellationToken)
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string rawRelativeFilePath = Path.GetRelativePath(BackupTask.SourceDir, file.FullName);
                try
                {
                    await LogAsync(db, LogLevel.Information, $"开始备份文件 {rawRelativeFilePath}", snapshot);
                    await CreateNewBackupFileAsync(db, snapshot, file, FileRecordType.Created, isVirtualFull,
                        cancellationToken);
                    await LogAsync(db, LogLevel.Information, $"文件 {rawRelativeFilePath} 已备份", snapshot);
                    snapshot.CreatedFileCount++;
                }
                catch (IOException ex)
                {
                    await LogAsync(db, LogLevel.Error, $"文件 {rawRelativeFilePath} 备份失败", snapshot, ex.ToString());
                }
            }
        }

        private async Task HandleIncrementalBackupAsync(DbService db, BackupSnapshotEntity snapshot,
            List<FileInfo> files, CancellationToken cancellationToken)
        {
            var latestFiles = (await db.GetLatestFilesAsync(snapshot)).ToDictionary(p => p.RawFileRelativePath);
            await LogAsync(db, LogLevel.Information, $"已获取数据库中当前镜像的最新文件集合，共{latestFiles.Count}个", snapshot);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string rawRelativeFilePath = Path.GetRelativePath(BackupTask.SourceDir, file.FullName);
                try
                {
                    if (latestFiles.TryGetValue(rawRelativeFilePath, out var latestFile))
                    {
                        if (latestFile.Time != file.LastWriteTime || latestFile.Length != file.Length)
                        {
                            await LogAsync(db, LogLevel.Information, $"文件 {rawRelativeFilePath} 已修改，开始备份", snapshot);
                            await CreateNewBackupFileAsync(db, snapshot, file, FileRecordType.Modified, false,
                                cancellationToken);
                            snapshot.ModifiedFileCount++;
                        }

                        latestFiles.Remove(rawRelativeFilePath);
                    }
                    else
                    {
                        await LogAsync(db, LogLevel.Information, $"文件 {rawRelativeFilePath} 已新增，开始备份", snapshot);
                        await CreateNewBackupFileAsync(db, snapshot, file, FileRecordType.Created, false,
                            cancellationToken);
                        snapshot.CreatedFileCount++;
                    }
                }
                catch (IOException ex)
                {
                    await LogAsync(db, LogLevel.Error, $"文件{rawRelativeFilePath}备份失败", snapshot, ex.ToString());
                }
            }

            foreach (var deletingFilePath in latestFiles.Keys)
            {
                await LogAsync(db, LogLevel.Information, $"文件{deletingFilePath}已删除", snapshot);
                BackupFileEntity record = new BackupFileEntity()
                {
                    Snapshot = snapshot,
                    RawFileRelativePath = deletingFilePath,
                    Type = FileRecordType.Deleted
                };
                db.Add(record);
                snapshot.DeletedFileCount++;
            }

            if (snapshot.IsEmpty())
            {
                await LogAsync(db, LogLevel.Information, "没有文件改变");
            }
        }
    }

    public class BackupLogEventArgs : EventArgs
    {
        public BackupLogEventArgs(BackupTask task, BackupLogEntity log)
        {
            Log = log;
            Task = task;
        }

        public BackupLogEntity Log { get; set; }
        public BackupTask Task { get; set; }
    }
}