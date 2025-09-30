using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs;

public partial class PackingConfig : ConfigBase
{
    [ObservableProperty]
    private string sourceDir;

    [ObservableProperty]
    private string targetDir;

    [ObservableProperty]
    private FileFilterRule filter = new FileFilterRule();

    [ObservableProperty]
    private PackingType packingType = PackingType.Copy;

    [ObservableProperty]
    private long packageSizeMB = 23500;

    [ObservableProperty]
    private string previousPackageInfoFiles;

    [ObservableProperty]
    private SecurePassword password = new();

    [ObservableProperty]
    private string hashCacheFile;

    partial void OnTargetDirChanged(string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(HashCacheFile) ||
            Path.Combine(oldValue, WriteOnceArchiveParameters.HashCacheFileName) == HashCacheFile)
        {
            HashCacheFile = Path.Combine(newValue, WriteOnceArchiveParameters.HashCacheFileName);
        }
    }

    public override void Check()
    {
        CheckDir(SourceDir, "源目录");
        CheckEmpty(TargetDir, "目标目录");
        if (PackageSizeMB < 0.1)
        {
            throw new Exception("单文件包容量过小");
        }
    }
}