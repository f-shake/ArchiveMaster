using ArchiveMaster.Enums;

namespace ArchiveMaster.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class AiAgentConfigAttribute(string name, AiAgentConfigType type) : Attribute
{
    public AiAgentConfigType Type { get; } = type;
    public string Name { get; } = name;
}