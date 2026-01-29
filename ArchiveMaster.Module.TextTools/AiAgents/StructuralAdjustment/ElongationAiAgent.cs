namespace ArchiveMaster.AiAgents.StructuralAdjustment;

public sealed class ElongationAiAgent : AiAgentBase
{
    public override string Name => "扩展";

    public override string Description =>
        "扩展文本，增加细节和描述，丰富内容";


    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请扩展文本，增加细节和描述，丰富内容。");
    }
}