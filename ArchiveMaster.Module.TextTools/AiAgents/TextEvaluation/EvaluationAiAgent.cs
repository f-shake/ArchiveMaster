namespace ArchiveMaster.AiAgents.TextEvaluation;

public sealed class EvaluationAiAgent : AiAgentBase
{
    public override string Name => "综合评价";
    public override string Description => "对文本进行综合评价，包括逻辑与结构、表达与用词、风格与语气、内容与观点";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>("请用一段话综合评价文本的整体质量，同时简述主要优点与不足。然后给出对文章的评价分（0-10分）");
    }
}