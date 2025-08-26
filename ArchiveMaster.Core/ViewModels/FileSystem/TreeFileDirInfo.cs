using System.ComponentModel;
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

    private bool? isTreeItemChecked = false;

    [property: JsonIgnore]
    [ObservableProperty]
    private TreeDirInfo parent;
    internal TreeFileDirInfo()
    {
    }

    internal TreeFileDirInfo(FileSystemInfo file, string topDir, TreeDirInfo parent, int depth, int index)
        : base(file, topDir)
    {
        Depth = depth;
        Index = index;
        Parent = parent;
    }

    internal TreeFileDirInfo(SimpleFileInfo file, TreeDirInfo parent, int depth, int index)
        : base(file)
    {
        ArgumentNullException.ThrowIfNull(file);
        Depth = depth;
        Index = index;
        Parent = parent;
        RawFileInfo = file;

        IsTreeItemChecked = file.IsChecked;
        file.PropertyChanged += File_PropertyChanged;
    }


    //兼容父类
    public new bool IsChecked
    {
        get => IsTreeItemChecked ?? false;
        set => IsTreeItemChecked = value;
    }

    //由于树节点的选中状态可能是三态的，因此单独设置一个属性
    public bool? IsTreeItemChecked
    {
        get => isTreeItemChecked;
        set
        {
            //此处仅处理交互（点击）导致的改变
            if (!SetProperty(ref isTreeItemChecked, value ?? false))
            {
                return;
            }

            //向上传播：通知父节点更新状态
            Parent?.UpdateCheckedStateFromChildren();

            //对于目录节点，向下传播：设置所有子节点的状态
            if (this is TreeDirInfo d)
            {
                d.PropagateCheckedStateToChildren();
            }

            //同步到原始数据
            if (RawFileInfo != null)
            {
                RawFileInfo.IsChecked = value ?? false;
            }
        }
    }


    /// <summary>
    /// 原始的文件信息对象
    /// </summary>
    public SimpleFileInfo RawFileInfo { get; }

    public bool IsLast()
    {
        if (Parent == null)
        {
            throw new InvalidOperationException("父节点为空，无法判断是否为最后一个");
        }

        return Index == Parent.Subs.Count - 1;
    }

    /// <summary>
    /// 设置树节点的选中状态，不触发向上或向下的传播，用于子类调用
    /// </summary>
    /// <param name="value"></param>
    protected internal void SetTreeItemChecked(bool? value)
    {
        SetProperty(ref isTreeItemChecked, value, nameof(IsTreeItemChecked));
        if (RawFileInfo != null)
        {
            //同步到原始数据
            RawFileInfo.IsChecked = value ?? false;
        }
    }
    private void File_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SimpleFileInfo.IsChecked))
        {
            //当原始数据的选中状态改变时，更新树节点的状态
            IsTreeItemChecked = RawFileInfo.IsChecked;
        }
    }
}