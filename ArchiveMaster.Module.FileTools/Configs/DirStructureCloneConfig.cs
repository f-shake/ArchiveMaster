using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs;

public partial class DirStructureCloneConfig : ConfigBase
{
    [ObservableProperty]
    private string sourceDirOrFile;

    [ObservableProperty]
    private bool inputStructureFile;

    [ObservableProperty]
    private FileFilterRule filter = new FileFilterRule();

    [ObservableProperty]
    private string targetDirOrFile;

    [ObservableProperty]
    private bool exportStructureFile;

    public override void Check()
    {
        CheckEmpty(SourceDirOrFile, "源目录");
        if (!File.Exists(SourceDirOrFile) && !Directory.Exists(SourceDirOrFile))
        {
            throw new Exception($"源目录或文件不存在: {SourceDirOrFile}");
        }

        if (ExportStructureFile)
        {
            CheckEmpty(TargetDirOrFile, "输出结构文件");
        }
        else
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new Exception("稀疏文件目前仅支持Windows");
            }

            CheckEmpty(TargetDirOrFile, "输出目录");
        }
    }
}