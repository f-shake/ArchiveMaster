using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class TreeFileDirInfo : FileSystem.SimpleFileInfo
{
    [ObservableProperty]
    private int depth;

    [property: JsonIgnore]
    [ObservableProperty]
    private int index;

    [property: JsonIgnore]
    [ObservableProperty]
    private TreeDirInfo parent;

    internal TreeFileDirInfo()
    {
        IsChecked = false;
    }

    internal TreeFileDirInfo(FileSystemInfo file, string topDir, TreeDirInfo parent, int depth, int index)
        : base(file, topDir)
    {
        Depth = depth;
        Index = index;
        Parent = parent;
        IsChecked = false;
    }

    internal TreeFileDirInfo(SimpleFileInfo file, TreeDirInfo parent, int depth, int index)
        : base(file)
    {
        Depth = depth;
        Index = index;
        Parent = parent;
        IsChecked = false;
        RawFileInfo = file;
    }

    public SimpleFileInfo RawFileInfo { get; }

    public bool IsLast()
    {
        return Index == Parent.Subs.Count - 1;
    }
}