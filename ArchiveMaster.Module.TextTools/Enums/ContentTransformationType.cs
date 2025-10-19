using System.ComponentModel;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum ContentTransformationType
{
    [Description("翻译")]
    [AiPrompt("请将文本翻译成指定语言。")]
    Translation,

    [Description("摘要生成")]
    [AiPrompt("请对文本进行摘要，形成一段连续完整的话，保留原文的主要意思。")]
    Summary,
    
    [Description("关键词提取")]
    [AiPrompt("请提取文本中的三个关键词。")]
    KeywordExtraction,
    
    [Description("标题生成")]
    [AiPrompt("请为文本生成一个合适的标题。")]
    TitleGeneration,
}