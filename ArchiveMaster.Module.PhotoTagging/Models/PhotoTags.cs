using System.Text.Json.Serialization;
using ArchiveMaster.Enums;

namespace ArchiveMaster.Models;

public record PhotoTags(
    List<string> ObjectTags,
    List<string> SceneTags,
    List<string> MoodTags,
    List<string> ColorTags,
    List<string> TechniqueTags,
    List<string> TextTags,
    string Description)
{
    [JsonIgnore]
    public int Count => ObjectTags.Count
                        + SceneTags.Count
                        + MoodTags.Count
                        + ColorTags.Count
                        + TechniqueTags.Count
                        + TextTags.Count;

    public bool Contains(string tag, TagType type)
    {
        return type switch
        {
            TagType.All => ObjectTags.Contains(tag)
                           || SceneTags.Contains(tag)
                           || MoodTags.Contains(tag)
                           || ColorTags.Contains(tag)
                           || TechniqueTags.Contains(tag)
                           || TextTags.Contains(tag)
                           || Description == tag,
            TagType.Object => ObjectTags.Contains(tag),
            TagType.Scene => SceneTags.Contains(tag),
            TagType.Mood => MoodTags.Contains(tag),
            TagType.Color => ColorTags.Contains(tag),
            TagType.Technique => TechniqueTags.Contains(tag),
            TagType.Text => TextTags.Contains(tag),
            TagType.Description => Description.Contains(tag),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public bool Matches(string query, TagType type, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        bool ListMatch(List<string> list) => list.Any(t => t.Contains(query, comparison));

        return type switch
        {
            TagType.All => ListMatch(ObjectTags)
                           || ListMatch(SceneTags)
                           || ListMatch(MoodTags)
                           || ListMatch(ColorTags)
                           || ListMatch(TechniqueTags)
                           || ListMatch(TextTags)
                           || Description.Contains(query, comparison),
            TagType.Object => ListMatch(ObjectTags),
            TagType.Scene => ListMatch(SceneTags),
            TagType.Mood => ListMatch(MoodTags),
            TagType.Color => ListMatch(ColorTags),
            TagType.Technique => ListMatch(TechniqueTags),
            TagType.Text => ListMatch(TextTags),
            TagType.Description => Description.Contains(query, comparison),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}