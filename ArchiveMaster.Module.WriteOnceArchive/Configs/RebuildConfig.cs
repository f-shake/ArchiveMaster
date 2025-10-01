using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class RebuildConfig : ConfigBase
{
    [ObservableProperty]
    private string sourceDirs;

    [ObservableProperty]
    private string targetDir;

    [ObservableProperty]
    private string packageInfoFile;

    [ObservableProperty]
    private bool skipIfExisted = true;

    [ObservableProperty]
    private int maxTimeToleranceSecond = 2;

    [ObservableProperty]
    private bool checkOnly;

    [ObservableProperty]
    private SecurePassword password = new();

    public override void Check()
    {
        CheckEmpty(SourceDirs, "源目录");
        CheckEmpty(TargetDir, "目标目录");
        CheckFile(PackageInfoFile, "包信息文件", true);
    }
}