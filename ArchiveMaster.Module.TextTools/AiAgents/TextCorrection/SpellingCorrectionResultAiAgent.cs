namespace ArchiveMaster.AiAgents.TextCorrection;

public sealed class SpellingCorrectionResultAiAgent : AiAgentBase
{
    public override string Name => "错别字（修正后文本）";

    public override string Description =>
        "修正文本中的拼写错误，并返回修正后的文本";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请修正文本中的错别字，并返回修正后的文本。");
    }
}