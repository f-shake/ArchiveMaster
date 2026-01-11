namespace ArchiveMaster.AiAgents.StructuralAdjustment;

public sealed class ReconstructionAiAgent : AiAgentBase
{
    public override string Name => "重构";

    public override string Description =>
        "使用不同的方式表达同样的意思";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请用不同的方式表达同样的意思。");
    }
}