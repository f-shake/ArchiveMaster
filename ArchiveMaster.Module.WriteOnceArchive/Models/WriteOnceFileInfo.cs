namespace ArchiveMaster.Models;

public record WriteOnceFileInfo(string RelativePath, string Hash, long Length, DateTime LastWriteTime);