using ArchiveMaster.Attributes;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.AiAgents.ContentTransformation;

public sealed class TextImitationAiAgent : AiAgentBase
{
    public override string Name => "文段仿写";

    public override string Description => "根据提供的参考文段，仿照其格式和内容，对文本源进行改写，形成新的文本";

    [AiAgentConfig("参考文段", AiAgentConfigType.TextSource)]
    public TextSource ReferenceText { get; set; } = new TextSource();

    public override async ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        var referenceText = await ReferenceText.GetCombinedPlainTextAsync(ct);
        return $"请根据参考文段，对文本进行仿写。参考文段从#####开始，以$$$$$结束。\n#####\n{referenceText}\n$$$$$";
    }
}