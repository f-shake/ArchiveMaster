namespace ArchiveMaster.AiAgents.TextCorrection;

public sealed class SentenceCorrectionResultAiAgent : AiAgentBase
{
    public override string Name => "语段（修正后文本）";
    public override string Description => "修正文本中的不通顺或不合适的语段，并返回修正后的文本";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请修正文本中不通顺或不合适的语段，并返回修正后的文本。");
    }
}