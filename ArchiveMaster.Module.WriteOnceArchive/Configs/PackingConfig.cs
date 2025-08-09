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
    private FileFilterRule filter=new FileFilterRule();

    [ObservableProperty]
    private PackingType packingType = PackingType.Copy;

    [ObservableProperty]
    private int packageSizeMB = 23500;

    [ObservableProperty]
    private string archivedFilesHashFile;
    
    [ObservableProperty]
    private string hashCacheFile;

    public override void Check()
    {
        CheckDir(SourceDir,"源目录");
        CheckEmpty(TargetDir,"目标目录");
        if (PackageSizeMB < 100)
        {
            throw new Exception("单盘容量过小");
        }
    }
}