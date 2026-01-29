namespace ArchiveMaster.AiAgents.ExpressionOptimization;

public sealed class RefinementAiAgent : AiAgentBase
{
    public override string Name => "润色";

    public override string Description => "对文本进行润色，使其更符合语言习惯和风格";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请优化语言表达，使文本更流畅和优美。");
    }
}