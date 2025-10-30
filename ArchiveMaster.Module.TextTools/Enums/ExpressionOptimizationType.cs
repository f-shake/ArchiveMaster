using System.ComponentModel;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum ExpressionOptimizationType
{
    [AiAgent("润色",
        "对文本进行润色，使其更符合语言习惯和风格",
        "请优化语言表达，使文本更流畅和优美。")]
    Refinement,

    [AiAgent("正式化",
        "将文本转化为更正式的表达方式",
        "请将文本转化为更正式的表达方式。")]
    Formalization,

    [AiAgent("口语化",
        "将文本转化为更口语化的表达方式",
        "请将文本转化为更口语化的表达方式。")]
    Casualization
}