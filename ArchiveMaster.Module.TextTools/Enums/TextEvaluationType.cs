using System.ComponentModel;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum TextEvaluationType
{
    [Description("综合评价")]
    [AiPrompt("请用一段话综合评价文本的整体质量，同时简述主要优点与不足。然后给出对文章的评价分（0-10分）")]
    Evaluation,

    [Description("逻辑与结构")]
    [AiPrompt("请用一段话评价文本的逻辑与结构是否清晰、有条理。然后给出逻辑与结构得分（0-10分）。")]
    LogicAndStructure,

    [Description("表达与用词")]
    [AiPrompt("请用一段话评价文本的表达是否自然、准确，用词是否恰当。然后给出表达与用词得分（0-10分）。")]
    ExpressionAndDiction,

    [Description("风格与语气")]
    [AiPrompt("请用一段话评价文本的风格与语气是否与预期场合或受众匹配。然后给出风格与语气得分（0-10分）。")]
    StyleAndTone,

    [Description("内容与观点")]
    [AiPrompt("请用一段话评价文本的内容是否完整、有深度，观点是否明确、有说服力。然后给出内容与观点得分（0-10分）。")]
    ContentAndIdeas
}
