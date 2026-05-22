using System.Text.Json.Serialization;

namespace ArchiveMaster.AiAgents;

public abstract class AiAgentBase
{
    [JsonIgnore]
    public virtual bool CanUserSetExtraPrompt => true;

    [JsonIgnore]
    public abstract string Description { get; }

    public string ExtraPrompt { get; set; }

    [JsonIgnore]
    public abstract string Name { get; }
    public abstract ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default);

    public virtual ValueTask<string> PostProcessAsync(ValueTask<string> assistantResponse, CancellationToken ct = default)
    {
        return assistantResponse;
    }
}