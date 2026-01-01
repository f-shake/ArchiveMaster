using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum SlimmingTaskType
{
    [Description("复制")]
    Copy,

    [Description("压缩")]
    Compress,

    [Description("跳过")]
    Skip,

    [Description("删除")]
    Delete
}