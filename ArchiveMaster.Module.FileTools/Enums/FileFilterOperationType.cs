using System.ComponentModel;

namespace ArchiveMaster.Enums;
public enum FileFilterOperationType
{
    [Description("复制")]
    Copy,

    [Description("移动")]
    Move,

    [Description("硬链接")]
    HardLink,
    
    [Description("符号链接")]
    SymbolLink,
    
    [Description("删除")]
    Delete,
    
    [Description("计算Hash")]
    Hash,
}
