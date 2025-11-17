using System.Collections.Concurrent;
using System.Diagnostics;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArchiveMaster.Services;

public partial class DbService
{
    public Task<List<BackupFileEntity>> GetFileHistory(string relativePath)
    {
        var query = db.Files
            .Where(p => p.RawFileRelativePath == relativePath)
            .Where(p => p.Type == FileRecordType.Created || p.Type == FileRecordType.Modified)
            .Where(p => !p.IsDeleted)
            .Include(p => p.Snapshot)
            .Where(p => p.Snapshot.EndTime > default(DateTime))
            .Where(p => !p.Snapshot.IsDeleted)
            .OrderBy(p => p.Snapshot.BeginTime);

        return query.ToListAsync();
    }

    public async Task<IEnumerable<BackupFileEntity>> GetLatestFilesAsync(int snapshotId)
    {
        Initialize();
        BackupSnapshotEntity snapshot = await GetValidSnapshots()
                                            .FirstOrDefaultAsync(p => p.Id == snapshotId)
                                        ?? throw new KeyNotFoundException(
                                            $"找不到ID为{snapshotId}的{nameof(BackupSnapshotEntity)}");
        return await GetLatestFilesAsync(snapshot);
    }

    public async Task<IEnumerable<BackupFileEntity>> GetLatestFilesAsync(BackupSnapshotEntity snapshot)
    {
        Initialize();
        BackupSnapshotEntity fullSnapshot;
        List<BackupSnapshotEntity> incrementalSnapshots = new List<BackupSnapshotEntity>();
        if (snapshot.Type is SnapshotType.Full or SnapshotType.VirtualFull)
        {
            fullSnapshot = snapshot;
        }
        else
        {
            fullSnapshot = await GetValidSnapshots()
                               .Where(p => p.Type == SnapshotType.Full || p.Type == SnapshotType.VirtualFull)
                               .Where(p => p.BeginTime < snapshot.BeginTime)
                               .OrderByDescending(p => p.BeginTime)
                               .FirstOrDefaultAsync()
                           ?? throw new KeyNotFoundException(
                               $"找不到该{nameof(BackupSnapshotEntity)}对应的全量备份{nameof(BackupSnapshotEntity)}");
            incrementalSnapshots.AddRange(await GetValidSnapshots()
                .Where(p => p.Type == SnapshotType.Increment)
                .Where(p => p.BeginTime > fullSnapshot.EndTime)
                .Where(p => p.EndTime < snapshot.BeginTime)
                .OrderBy(p => p.BeginTime)
                .ToListAsync());

            incrementalSnapshots.Add(snapshot);
        }

        var fileRecords = (await db.Files
                .Where(p => p.SnapshotId == fullSnapshot.Id)
                .ToListAsync())
            .ToDictionary(p => p.RawFileRelativePath);


        foreach (var incrementalSnapshot in incrementalSnapshots)
        {
            var incrementalFiles = await db.Files
                .Where(p => p.SnapshotId == incrementalSnapshot.Id)
                .ToListAsync();
            foreach (var incrementalFile in incrementalFiles)
            {
                switch (incrementalFile.Type)
                {
                    case FileRecordType.Created:
                        if (fileRecords.ContainsKey(incrementalFile.RawFileRelativePath))
                        {
                            await LogAsync(LogLevel.Warning,
                                $"获取最新文件列表时，增量备份中，文件{incrementalFile.RawFileRelativePath}被新增，但先前版本的文件中已存在该文件",
                                snapshot);
                            fileRecords.Remove(incrementalFile.RawFileRelativePath);
                            Debug.Assert(false);
                        }

                        fileRecords.Add(incrementalFile.RawFileRelativePath, incrementalFile);
                        break;

                    case FileRecordType.Modified:
                        if (!fileRecords.ContainsKey(incrementalFile.RawFileRelativePath))
                        {
                            await LogAsync(LogLevel.Warning,
                                $"获取最新文件列表时，增量备份中，文件{incrementalFile.RawFileRelativePath}被修改，但不能在先前版本的文件中找到这一个文件",
                                snapshot);
                            Debug.Assert(false);
                        }

                        fileRecords[incrementalFile.RawFileRelativePath] = incrementalFile;
                        break;

                    case FileRecordType.Deleted:
                        if (!fileRecords.ContainsKey(incrementalFile.RawFileRelativePath))
                        {
                            await LogAsync(LogLevel.Warning,
                                $"获取最新文件列表时，增量备份中，文件{incrementalFile.RawFileRelativePath}被删除，但不能在先前版本的文件中找到这一个文件",
                                snapshot);
                            Debug.Assert(false);
                        }

                        fileRecords.Remove(incrementalFile.RawFileRelativePath);
                        break;
                }
            }
        }

        return fileRecords.Values;
    }

    public BackupFileEntity GetSameFile(DateTime time, long length, string sha1)
    {
        Initialize();
        //20251112：
        //一直发现每次整理文件会有很多丢失文件。检查后发现，可能是由于此处代码与整理文件代码筛选条件不一致导致的。
        //此处原本会搜索所有文件，即便快照已经删除；而整理文件处，不考虑。
        //这会导致在备份时，原本应当备份的文件，被误以为已有物理文件存在，因而跳过备份。
        var query = db.Files
            .Include(p => p.Snapshot)
            .Where(p => p.Snapshot.IsDeleted == false)
            .Where(p => p.Snapshot.EndTime > default(DateTime))
            .Where(p => p.BackupFileName != null)
            .Where(p => p.Time == time)
            .Where(p => p.Length == length);
        if (sha1 != null)
        {
            query = query.Where(p => p.Hash == sha1);
        }

        return query.FirstOrDefault();
    }

    public async Task<BackupFilesCheckResult> CheckFilesAsync(
        CancellationToken ct)
    {
        var lostFiles = new List<BackupFileEntity>();
        List<FileInfo> redundantFiles = null;
        var diskFileLength = Guid.NewGuid().ToString("N").Length; //32
        await Task.Run(() =>
        {
            var diskFiles = new DirectoryInfo(BackupTask.BackupDir)
                .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                .ApplyFilter(ct)
                .Where(p => p.Name.Length == diskFileLength)
                .ToList();
            ct.ThrowIfCancellationRequested();
            var query = db.Files
                .Include(p => p.Snapshot)
                .Where(p => p.IsDeleted == false)
                .Where(p => p.Snapshot.IsDeleted == false)
                .Where(p => p.Snapshot.EndTime > default(DateTime))
                .Where(p => p.BackupFileName != null)
                .Where(p => p.Type != FileRecordType.Deleted);
            var dbFiles = query.ToList();

            var diskFileName2FileInfo = diskFiles.ToDictionary(p => p.Name);
            var dbFileName2Entity =
                dbFiles.GroupBy(p => p.BackupFileName).ToDictionary(p => p.Key, p => p.ToList());

            foreach (var key in dbFileName2Entity.Keys)
            {
                if (!diskFileName2FileInfo.Remove(key))
                {
                    lostFiles.AddRange(dbFileName2Entity[key]);
                }
            }

            redundantFiles = diskFileName2FileInfo.Values.ToList();
        }, ct);
        return new BackupFilesCheckResult(redundantFiles, lostFiles);
    }

    public async Task DeleteRedundantFilesAndRemoveLostFileItemsAsync(BackupFilesCheckResult checkResult)
    {
        //20251112：
        //原本删除文件的基础上，新增了对丢失文件记录的处理，使其恢复到不包含物理文件的状态。
        foreach (var file in checkResult.RedundantFiles)
        {
            FileHelper.DeleteByConfig(file.FullName, "实时备份服务");
        }

        foreach (var item in checkResult.LostFileItems)
        {
            //物理备份文件丢失，那么备份记录中相关的内容也删除
            item.Hash = null;
            item.BackupFileName = null;
            db.Entry(item).State = EntityState.Modified;
        }

        await db.SaveChangesAsync();
    }
}