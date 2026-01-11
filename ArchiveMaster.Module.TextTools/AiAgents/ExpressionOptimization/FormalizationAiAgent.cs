namespace ArchiveMaster.AiAgents.ExpressionOptimization;

public sealed class FormalizationAiAgent : AiAgentBase
{
    public override string Name => "正式化";

    public override string Description => "将文本转化为更正式的表达方式";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请将文本转化为更正式的表达方式。");
    }
}