using System.ComponentModel;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum TextGenerationCategory
{
    [Description("表达优化")]
    ExpressionOptimization,

    [Description("结构调整")]
    StructuralAdjustment,

    [Description("内容转换")]
    ContentTransformation,

    [Description("文本评价")]
    TextEvaluation,

    [Description("自定义")]
    Custom
}