namespace ArchiveMaster.AiAgents.TextEvaluation;

public sealed class ContentAndIdeasAiAgent : AiAgentBase
{
    public override string Name => "内容与观点";

    public override string Description =>
        "评价文本的内容是否完整、有深度，观点是否明确、有说服力";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请用一段话评价文本的内容是否完整、有深度，观点是否明确、有说服力。然后给出内容与观点得分（0-10分）。");
    }
}