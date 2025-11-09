using System.ComponentModel;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum StructuralAdjustmentType
{
    [AiAgent("简化",
        "简化文本，保留基本意思，缩短语句",
        "请简化文本，保留基本意思，缩短语句。")]
    Simplification,

    [AiAgent("扩展",
        "扩展文本，增加细节和描述，丰富内容",
        "请扩展文本，增加细节和描述，丰富内容。")]
    Elongation,
    
    [AiAgent("续写",
        "根据文本的结尾进行续写",
        "请根据文本的结尾，继续写下去。")]
    Continuation,

    [AiAgent("重构",
        "使用不同的方式表达同样的意思",
        "请用不同的方式表达同样的意思。")]
    Reconstruction,
    
    [AiAgent("语音文本校对",
        "针对语音识别结果进行标点添加和错别字修正，优化可读性同时保持原意",
        "请对以下语音识别文本进行规范化处理：仅添加标点（句号、逗号、引号、顿号等），修正明显语音识别错误（通常是同音错别字），不改动原句结构、用词习惯或表达风格，不加词、删词，保留所有口语化、重复、情绪化表达，以忠实还原原始文本")]
    SpeechTextProofreading,
}