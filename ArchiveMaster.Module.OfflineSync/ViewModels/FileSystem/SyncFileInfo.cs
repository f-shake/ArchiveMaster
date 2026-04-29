using ArchiveMaster.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class SyncFileInfo : SimpleFileInfo
{
    /// <summary>
    /// 对于 <see cref="UpdateType"/>为<see cref="FileUpdateType.Move"/> 类型的对象，表示异地的相对路径
    /// </summary>
    [ObservableProperty]
    private string oldRelativePath;

    /// <summary>
    /// 生成补丁时文件所使用的临时名称
    /// </summary>
    [ObservableProperty]
    private string tempName;

    /// <summary>
    /// 文件更新类型
    /// </summary>
    [ObservableProperty]
    private FileUpdateType updateType;

    public SyncFileInfo()
    {
    }

    public SyncFileInfo(FileInfo file, string topDir) : base(file, topDir)
    {
    }
    public string ParentDirName => TopDirectory == null ? "" : System.IO.Path.GetFileName(TopDirectory);
}