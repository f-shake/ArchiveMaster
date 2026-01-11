namespace ArchiveMaster.AiAgents.StructuralAdjustment;

public sealed class SpeechTextProofreadingAiAgent : AiAgentBase
{
    public override string Name => "语音文本校对";

    public override string Description =>
        "针对语音识别结果进行标点添加和错别字修正，优化可读性同时保持原意";


    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>(
            "请对以下语音识别文本进行规范化处理：仅添加标点（句号、逗号、引号、顿号等），修正明显语音识别错误（通常是同音错别字），不改动原句结构、用词习惯或表达风格，不加词、删词，保留所有口语化、重复、情绪化表达，以忠实还原原始文本");
    }
}