using System.ComponentModel;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum ExpressionOptimizationType
{
    [Description("润色")]
    [AiPrompt("请优化语言表达，使文本更流畅和优美。")]
    Refinement,

    [Description("正式化")]
    [AiPrompt("请将文本转化为更正式的表达方式。")]
    Formalization,

    [Description("口语化")]
    [AiPrompt("请将文本转化为更口语化的表达方式。")]
    Casualization
}