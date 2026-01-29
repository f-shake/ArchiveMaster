namespace ArchiveMaster.AiAgents.ContentTransformation;

public sealed class KeywordExtractionAiAgent : AiAgentBase
{
    public override string Name => "关键词提取";
    public override string Description => "提取文本中的三个关键词";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请提取文本中的三个关键词。");
    }
}