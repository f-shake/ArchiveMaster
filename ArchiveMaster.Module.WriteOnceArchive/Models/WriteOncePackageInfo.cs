using FzLib.IO;

namespace ArchiveMaster.Models;

public record WriteOncePackageInfo(
    List<WriteOnceFileInfo> AllFiles,
    long TotalLength,
    DateTime PackageTime,
    List<string> Hashes,
    FileFilterRule Filter);