namespace ArchiveMaster.AiAgents.ContentTransformation;

public sealed class TitleGenerationAiAgent : AiAgentBase
{
    public override string Name => "标题生成";
    public override string Description => "为文本生成一个合适的标题";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请为文本生成一个合适的标题。");
    }
}