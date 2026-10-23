using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs;

public partial class DirStructureSyncConfig : ConfigBase
{
    [ObservableProperty]
    private string templateDir;

    [ObservableProperty]
    private string sourceDir;

    [ObservableProperty]
    private string targetDir;

    [ObservableProperty]
    private bool compareTime;

    [ObservableProperty]
    private bool compareLength;

    [ObservableProperty]
    private bool compareName;

    [ObservableProperty]
    private FileFilterRule filter = new FileFilterRule();

    [ObservableProperty]
    private int maxTimeToleranceSecond;

    [ObservableProperty]
    private bool copy;

    public override void Check()
    {
        CheckDir(SourceDir, "源目录");
        CheckDir(TemplateDir, "模板目录");
        CheckEmpty(TargetDir, "目标目录");
        if (!(CompareName || CompareLength || CompareTime))
        {
            throw new ArgumentException("至少选择一个比较类型");
        }
    }

    partial void OnSourceDirChanged(string oldValue, string newValue)
    {
        if (string.IsNullOrWhiteSpace(TargetDir) || oldValue == TargetDir)
        {
            TargetDir = newValue;
        }
    }
}