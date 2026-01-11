namespace ArchiveMaster.AiAgents.TextEvaluation;

public sealed class ExpressionAndDictionAiAgent : AiAgentBase
{
    public override string Name => "表达与用词";

    public override string Description =>
        "评价文本的表达是否自然、准确，用词是否恰当";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("评价文本的表达是否自然、准确，用词是否恰当。");
    }
}