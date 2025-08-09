namespace ArchiveMaster.Models;

public record WriteOnceFileModel(string Path, string Hash, long Length, DateTime LastWriteTime);