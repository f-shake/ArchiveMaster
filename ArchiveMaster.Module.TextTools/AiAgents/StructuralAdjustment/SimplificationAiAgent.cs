namespace ArchiveMaster.AiAgents.StructuralAdjustment;

public sealed class SimplificationAiAgent : AiAgentBase
{
    public override string Name => "简化";

    public override string Description =>
        "简化文本，保留基本意思，缩短语句";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请简化文本，保留基本意思，缩短语句。");
    }
}