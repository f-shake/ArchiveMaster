namespace ArchiveMaster.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class AiPromptAttribute(string systemPrompt) : Attribute
{
    public string SystemPrompt { get; } = systemPrompt;
}