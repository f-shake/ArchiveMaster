using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum ProcessStatus
{
    [Description("就绪")]
    Ready,
    
    [Description("警告")]
    Warn,
    
    [Description("错误")]
    Error,
    
    [Description("处理中")]
    Processing,
    
    [Description("完成")]
    Success,
    
    [Description("跳过")]
    Skip,
}