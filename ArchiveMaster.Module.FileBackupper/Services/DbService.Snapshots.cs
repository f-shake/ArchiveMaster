using System.Collections.Concurrent;
using System.Diagnostics;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArchiveMaster.Services;

public partial class DbService
{
    public async Task<(List<BackupFileEntity> Created, List<BackupFileEntity> Modified, List<BackupFileEntity> Deleted)>
        GetSnapshotChanges(int snapshotId)
    {
        var allFiles = await db.Files
            .Include(p => p.Snapshot)
            .Where(p => p.Snapshot.Id == snapshotId)
            .ToListAsync();

        return (allFiles.Where(p => p.Type == FileRecordType.Created).ToList(),
            allFiles.Where(p => p.Type == FileRecordType.Modified).ToList(),
            allFiles.Where(p => p.Type == FileRecordType.Deleted).ToList());
    }

    public async Task<List<BackupSnapshotEntity>> GetSnapshotsAsync(SnapshotType? type = null,
        bool includeEmptySnapshot = false, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var query = GetSnapshotQuery(type, includeEmptySnapshot);

        query = query.OrderBy(p => p.BeginTime);
        var snapshots = await query.ToListAsync(ct);
        return snapshots;
    }

    private IQueryable<BackupSnapshotEntity> GetSnapshotQuery(SnapshotType? type, bool includeEmptySnapshot)
    {
        IQueryable<BackupSnapshotEntity> query = GetValidSnapshots();
        if (type.HasValue)
        {
            query = query.Where(p => p.Type == type);
        }

        if (!includeEmptySnapshot)
        {
            query = query.Where(p => p.CreatedFileCount + p.DeletedFileCount + p.ModifiedFileCount > 0);
        }

        return query;
    }

    private IQueryable<BackupSnapshotEntity> GetSnapshotQuery(SnapshotType[] types, bool includeEmptySnapshot)
    {
        IQueryable<BackupSnapshotEntity> query = GetValidSnapshots();
        if (types.Length > 0)
        {
            query = query.Where(p => types.Contains(p.Type));
        }

        if (!includeEmptySnapshot)
        {
            query = query.Where(p => p.CreatedFileCount + p.DeletedFileCount + p.ModifiedFileCount > 0);
        }

        return query;
    }

    public async Task<int> GetSnapshotCountAsync(SnapshotType? type = null,
        bool includeEmptySnapshot = false,
        Func<IQueryable<BackupSnapshotEntity>, IQueryable<BackupSnapshotEntity>> otherQueryAction = null,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var query = GetSnapshotQuery(type, includeEmptySnapshot);
        query = query.OrderBy(p => p.BeginTime);
        if (otherQueryAction != null)
        {
            query = otherQueryAction.Invoke(query);
        }

        return await query.CountAsync(cancellationToken: ct);
    }

    public async Task<BackupSnapshotEntity> GetLastSnapshotAsync(SnapshotType? type = null,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var query = GetSnapshotQuery(type, true);
        query = query.OrderByDescending(p => p.BeginTime);
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<BackupSnapshotEntity> GetLastSnapshotAsync(SnapshotType[] types,
        CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var query = GetSnapshotQuery(types, true);
        query = query.OrderByDescending(p => p.BeginTime);
        return await query.FirstOrDefaultAsync(ct);
    }

    private IQueryable<BackupSnapshotEntity> GetValidSnapshots()
    {
        return db.Snapshots
            .Where(p => p.EndTime != default)
            .Where(p => !p.IsDeleted);
    }

    public async Task DeleteSnapshotAsync(BackupSnapshotEntity snapshot)
    {
        await InitializeAsync();
        //下一个全量备份
        var nextFullSnapshot = await GetValidSnapshots()
            .Where(p => p.BeginTime > snapshot.BeginTime)
            .Where(p => p.Type == SnapshotType.Full || p.Type == SnapshotType.VirtualFull)
            .OrderBy(p => p.BeginTime)
            .FirstOrDefaultAsync();

        //当前快照和下一个全量备份之前的快照
        var query = GetValidSnapshots()
            .Where(p => p.BeginTime >= snapshot.BeginTime);
        if (nextFullSnapshot != null)
        {
            query = query.Where(p => p.BeginTime < nextFullSnapshot.BeginTime);
        }

        await query.ExecuteUpdateAsync(p => p.SetProperty(e => e.IsDeleted, true));
    }
}