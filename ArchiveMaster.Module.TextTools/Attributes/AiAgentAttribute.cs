namespace ArchiveMaster.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class AiAgentAttribute(
    string name,
    string description,
    string systemPrompt)
    : Attribute
{
    public const string ReferenceTextPlaceholder = "{ref}";
    
    public const string ExtraInformationPlaceholder = "{info}";

    public string Description { get; init; } = description;

    public string Name { get; init; } = name;

    public bool NeedReferenceText { get; init; }

    public bool NeedExtraInformation { get; init; }

    public string ExtraInformationLabel { get; init; }

    public string SystemPrompt { get; init; } = systemPrompt;
}