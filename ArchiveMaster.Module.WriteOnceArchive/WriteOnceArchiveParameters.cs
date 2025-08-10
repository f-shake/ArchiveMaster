using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster;

public static class WriteOnceArchiveParameters
{
    /// <summary>
    /// 记录整个源目录的文件结构，格式为<see cref="WriteOncePackageInfo"/>的JSON序列化
    /// </summary>
    public const string PackageInfoFileName = "package_info.wpk";

    /// <summary>
    /// 记录每个文件的Hash值，格式为每行一个文件，每行格式为：<see cref="SimpleFileInfo"/>的HashCode + '\t' + Hash值
    /// </summary>
    public const string HashCacheFileName = "caches.whc";

    public const FileHashHelper.HashAlgorithmType HashType = FileHashHelper.HashAlgorithmType.SHA256;
    
    public const string EncryptedFileSuffix = "_enc";
}