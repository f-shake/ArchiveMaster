using ArchiveMaster.Attributes;
using ArchiveMaster.Enums;

namespace ArchiveMaster.AiAgents.Custom;

public class CustomAiAgent : AiAgentBase
{
    public override string Description => "自定义AI智能体";
    public override string Name => "自定义";

    [AiAgentConfig("自定义提示", AiAgentConfigType.Text)]
    public string CustomPrompt { get; set; }

    public override bool CanUserSetExtraPrompt => false;

    public override ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        return new ValueTask<string>(CustomPrompt);
    }
}