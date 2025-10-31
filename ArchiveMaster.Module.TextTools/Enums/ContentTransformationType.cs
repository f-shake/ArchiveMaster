using System.ComponentModel;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum ContentTransformationType
{
    [AiAgent("翻译",
        "将文本翻译成指定语言",
        $"请将文本翻译成{AiAgentAttribute.ExtraInformationPlaceholder}",
        NeedExtraInformation = true,
        ExtraInformationLabel = "目标语言")]
    Translation,

    [AiAgent("摘要生成",
        "将文本进行摘要，形成一段连续完整的话，保留原文的主要意思",
        "请对文本进行摘要，形成一段连续完整的话，保留原文的主要意思。")]
    Summary,

    [AiAgent("关键词提取",
        "提取文本中的三个关键词",
        "请提取文本中的三个关键词。")]
    KeywordExtraction,

    [AiAgent("标题生成",
        "为文本生成一个合适的标题",
        "请为文本生成一个合适的标题。")]
    TitleGeneration,

    [AiAgent("文段仿写",
        "根据提供的参考文段，仿照其格式和内容，对文本源进行改写，形成新的文本",
        $"请根据参考文段，对文本进行仿写。参考文段从#####开始，以$$$$$结束。\n#####\n{AiAgentAttribute.ReferenceTextPlaceholder}\n$$$$$",
        NeedReferenceText = true)]
    TextImitation,
}