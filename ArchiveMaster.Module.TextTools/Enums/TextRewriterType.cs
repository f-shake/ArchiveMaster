using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum TextRewriterType
{
    [Description("润色")]
    Refinement,

    [Description("简化")]
    Simplification,

    [Description("扩展")]
    Elongation,

    [Description("正式化")]
    Formalization,

    [Description("口语化")]
    Casualization,

    [Description("重构")]
    Reconstruction,

    [Description("翻译")]
    Translation,

    [Description("摘要")]
    Summary,

    [Description("自定义")]
    Custom
}