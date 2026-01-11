namespace ArchiveMaster.AiAgents.TextEvaluation;

public sealed class StyleAndToneAiAgent : AiAgentBase
{
    public override string Name => "风格与语气";

    public override string Description =>
        "评价文本的风格与语气是否与预期场合或受众匹配";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请用一段话评价文本的风格与语气是否与预期场合或受众匹配。然后给出风格与语气得分（0-10分）。");
    }
}