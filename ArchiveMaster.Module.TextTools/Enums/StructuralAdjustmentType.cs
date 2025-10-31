using System.ComponentModel;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum StructuralAdjustmentType
{
    [AiAgent("简化",
        "将文本简化，保留基本意思，缩短语句",
        "请简化文本，保留基本意思，缩短语句。")]
    Simplification,

    [AiAgent("扩展",
        "扩展文本，增加细节和描述，丰富内容",
        "请扩展文本，增加细节和描述，丰富内容。")]
    Elongation,
    
    [AiAgent("续写",
        "根据文本的结尾，继续写下去",
        "请根据文本的结尾，继续写下去。")]
    Continuation,

    [AiAgent("重构",
        "使用不同的方式表达同样的意思",
        "请用不同的方式表达同样的意思。")]
    Reconstruction
}