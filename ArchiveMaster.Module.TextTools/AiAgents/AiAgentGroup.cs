namespace ArchiveMaster.AiAgents;

public class AiAgentGroup(string key, string name)
{
    public string Key { get; } = key;
    public string Name { get; } = name;
    public List<AiAgentBase> Agents { get; } = new List<AiAgentBase>();
}