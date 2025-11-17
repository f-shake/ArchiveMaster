namespace ArchiveMaster.Models;

public record BackupFilesCheckResult(
    IList<FileInfo> RedundantFiles,
    IList<BackupFileEntity> LostFileItems
);