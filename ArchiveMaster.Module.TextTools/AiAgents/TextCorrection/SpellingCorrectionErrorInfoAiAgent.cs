namespace ArchiveMaster.AiAgents.TextCorrection;

public sealed class SpellingCorrectionErrorInfoAiAgent : AiAgentBase
{
    public override string Name => "错别字（错误反馈）";

    public override string Description =>
        "检测文本中的拼写错误，并返回错误信息和建议的修改";

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>(@"请检测文本中的错别字，并返回错误信息及建议的修改。
例如，用户输出“今天天汽真好，阳光明媚。我和麻麻去吃祸锅，吃了很多肉”，则输出为：
1.今天天“汽”真好：汽=>气（“汽”字不适用于“天气”这一语境，“汽”通常指的是气体或蒸汽，而“天气”指的是大气的状况）
2.我和“麻麻”去吃“祸”锅：麻麻=>妈妈（“麻麻”是口语化或儿童化的写法，但标准书面语中应写作“妈妈”），祸锅=>火锅（“祸”字意思是灾难、祸害，不符合语境；而“火锅”指的是一种常见的餐饮方式，应该用“火”字）

如果没有错误，则输出“没有错误”
注意：
1.只寻找错别字，如果你觉得建议后的文字和原文一样，则不要输出。
2.针对每一句话，如果你觉得是正确的，绝对不要输出。
3.输出为MarkDown的序号格式
4.每一行中的原文部分，应当包含标点之间的语段，不要过长");
    }
}