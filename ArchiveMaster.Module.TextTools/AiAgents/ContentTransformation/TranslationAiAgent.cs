using ArchiveMaster.Attributes;
using ArchiveMaster.Enums;

namespace ArchiveMaster.AiAgents.ContentTransformation;

public sealed class TranslationAiAgent : AiAgentBase
{
    public override string Name => "翻译";
    public override string Description => "将文本翻译成指定语言";

    [AiAgentConfig("语言", AiAgentConfigType.Text)]
    public string Language { get; set; }

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>($"请将文本翻译成{Language}");
    }
}