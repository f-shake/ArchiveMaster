using ArchiveMaster.Enums;
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
    private double packageSizeGB = 23.5;

    [ObservableProperty]
    private string previousPackageInfoFiles;

    [ObservableProperty]
    private string password;

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
        if (PackageSizeGB < 0.1)
        {
            throw new Exception("单文件包容量过小");
        }
    }
}