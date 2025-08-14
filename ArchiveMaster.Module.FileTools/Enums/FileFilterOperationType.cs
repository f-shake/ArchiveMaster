using System.ComponentModel;

namespace ArchiveMaster.Enums;
public enum FileFilterOperationType
{
    [Description("复制")]
    Copy,

    [Description("移动")]
    Move,

    [Description("删除")]
    Delete
}
