using System.Text.Json.Serialization;
using ArchiveMaster.Enums;

namespace ArchiveMaster.Models;

public record PhotoTags(
    List<string> ObjectTags,
    List<string> SceneTags,
    List<string> MoodTags,
    List<string> ColorTags,
    List<string> TechniqueTags,
    string OcrText,
    string Description)
{
    public ISet<string> GetAllTags()
    {
        HashSet<string> allTags = new(ObjectTags);
        allTags.UnionWith(SceneTags);
        allTags.UnionWith(MoodTags);
        allTags.UnionWith(ColorTags);
        allTags.UnionWith(TechniqueTags);
        return allTags;
    }

    [JsonIgnore]
    public int Count => ObjectTags.Count
                        + SceneTags.Count
                        + MoodTags.Count
                        + ColorTags.Count
                        + TechniqueTags.Count;

    public bool ContainsTag(string tag, TagType type)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;

        bool match = false;

        if (type.HasFlag(TagType.Object)) match |= ObjectTags.Contains(tag);
        if (type.HasFlag(TagType.Scene)) match |= SceneTags.Contains(tag);
        if (type.HasFlag(TagType.Mood)) match |= MoodTags.Contains(tag);
        if (type.HasFlag(TagType.Color)) match |= ColorTags.Contains(tag);
        if (type.HasFlag(TagType.Technique)) match |= TechniqueTags.Contains(tag);
        if (type.HasFlag(TagType.Text)) match |= OcrText != null && OcrText.Contains(tag);
        if (type.HasFlag(TagType.Description)) match |= Description != null && Description.Contains(tag);

        return match;
    }

    public bool Matches(string query, TagType type, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;

        bool ListMatch(List<string> list) => list.Any(t => t.Contains(query, comparison));
        bool TextMatch(string text) => text != null && text.Contains(query, comparison);

        bool isMatch = false;

        // 按位检查每一个标志位
        if (type.HasFlag(TagType.Object)) isMatch |= ListMatch(ObjectTags);
        if (type.HasFlag(TagType.Scene)) isMatch |= ListMatch(SceneTags);
        if (type.HasFlag(TagType.Mood)) isMatch |= ListMatch(MoodTags);
        if (type.HasFlag(TagType.Color)) isMatch |= ListMatch(ColorTags);
        if (type.HasFlag(TagType.Technique)) isMatch |= ListMatch(TechniqueTags);
        if (type.HasFlag(TagType.Text)) isMatch |= TextMatch(OcrText);
        if (type.HasFlag(TagType.Description)) isMatch |= TextMatch(Description);

        return isMatch;
    }
}