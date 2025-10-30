namespace ArchiveMaster.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class AiAgentAttribute(
    string name,
    string description,
    string systemPrompt,
    bool needReferenceText = false)
    : Attribute
{
    public const string ReferenceTextPlaceholder = "{ref}";

    public string Description { get; } = description;

    public string Name { get; } = name;

    public bool NeedReferenceText { get; } = needReferenceText;

    public string SystemPrompt { get; } = systemPrompt;
}