namespace ArchiveMaster.AiAgents.TextEvaluation;

public sealed class LogicAndStructureAiAgent : AiAgentBase
{
    public override string Name => "逻辑与结构";

    public override string Description =>
        "评价文本的逻辑与结构是否清晰、有条理";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请用一段话评价文本的逻辑与结构是否清晰、有条理。然后给出逻辑与结构得分（0-10分）。");
    }
}