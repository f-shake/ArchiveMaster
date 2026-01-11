using System.Text.Json.Serialization;

namespace ArchiveMaster.AiAgents;

public abstract class AiAgentBase
{
    [JsonIgnore]
    public abstract string Description { get; }

    [JsonIgnore]
    public abstract string Name { get; }

    [JsonIgnore]
    public virtual bool CanUserSetExtraPrompt => true;

    public string ExtraPrompt { get; set; }

    public abstract ValueTask<string> BuildSystemPromptAsync(CancellationToken ct = default);
}