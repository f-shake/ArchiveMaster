using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Microsoft.EntityFrameworkCore;

namespace ArchiveMaster.Services;

public class RestoreService(BackupTask task)
{
    public async Task<TreeDirInfo> GetSnapshotFileTreeAsync(int snapshotId, CancellationToken ct = default)
    {
        await using var db = new DbService(task);
        TreeDirInfo tree = null;
        await Task.Run(async () =>
        {
            var fileRecords =await db.GetLatestFilesAsync(snapshotId);

            tree = TreeDirInfo.CreateEmptyTree();

            foreach (var record in fileRecords.Select(p => new BackupFile(p)))
            {
                tree.AddFile(record);
            }
        }, ct);
        return tree;
    }
}