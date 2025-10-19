using System.ComponentModel;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum StructuralAdjustmentType
{
    [Description("简化")]
    [AiPrompt("请简化文本，保留基本意思，缩短语句。")]
    Simplification,

    [Description("扩展")]
    [AiPrompt("请扩展文本，增加细节和描述，丰富内容。")]
    Elongation,
    
    [Description("续写")]
    [AiPrompt("请根据文本的结尾，继续写下去。")]
    Continuation,

    [Description("重构")]
    [AiPrompt("请用不同的方式表达同样的意思。")]
    Reconstruction
}