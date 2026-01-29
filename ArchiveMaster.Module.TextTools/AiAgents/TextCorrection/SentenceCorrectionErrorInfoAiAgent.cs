namespace ArchiveMaster.AiAgents.TextCorrection;

public sealed class SentenceCorrectionErrorInfoAiAgent : AiAgentBase
{
    public override string Name => "语段（错误反馈）";
    public override string Description => "检测文本中的不通顺或不合适的语段，并返回错误信息和修改建议";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>(@"请检测文本中不通顺或不合适的语段，并返回错误信息及修改建议。
例如，用户输出：“我去商店买了一些东西，觉得天气很好，但是却没有去。突然觉得有点饿，于是回家吃饭，结果又忘记了买东西。”，则输出为：
1.我去商店买了一些东西，觉得天气很好，但是却没有去：这句话有些矛盾，“觉得天气很好”和“但是却没有去”之间缺少逻辑上的连接，可以考虑改为：“我去商店买了一些东西，路上天气很好，但我突然改变了主意，决定没有去。”
2.突然觉得有点饿，于是回家吃饭，结果又忘记了买东西：这段话可以稍微调整语气和顺序，使其更加连贯：“我突然觉得饿，于是回家吃饭，结果忘记了买东西。”

如果没有错误，则输出“没有错误”
注意：
1. 只检查语段是否不通顺，且要明确指出哪里不合适。
2. 如果语段已经通顺并且逻辑清晰，则不输出任何错误反馈。
3. 针对每一段不通顺或有逻辑问题的语句，输出详细修改建议。
4. 输出应为MarkDown的序号格式。
5. 每一行中的原文部分应当包含标点之间的语段，不要过长。");
    }
}