namespace ArchiveMaster.AiAgents.ContentTransformation;

public sealed class SummaryAiAgent : AiAgentBase
{
    public override string Name => "摘要生成";
    public override string Description => "将文本进行摘要，形成一段连续完整的话，保留原文的主要意思";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请对文本进行摘要，形成一段连续完整的话，保留原文的主要意思。");
    }
}