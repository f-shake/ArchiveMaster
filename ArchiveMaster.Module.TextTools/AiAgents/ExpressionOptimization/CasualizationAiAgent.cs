namespace ArchiveMaster.AiAgents.ExpressionOptimization;

public sealed class CasualizationAiAgent : AiAgentBase
{
    public override string Name => "口语化";

    public override string Description => "将文本转化为更口语化的表达方式";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请将文本转化为更口语化的表达方式。");
    }
}