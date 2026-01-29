namespace ArchiveMaster.AiAgents.StructuralAdjustment;

public sealed class ContinuationAiAgent : AiAgentBase
{
    public override string Name => "续写";

    public override string Description =>
        "根据文本的结尾进行续写";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请根据文本的结尾，继续写下去。");
    }
}