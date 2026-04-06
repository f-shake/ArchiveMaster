namespace ArchiveMaster.Models;

public record PhotoTagCollection(List<PhotoTagItem> Photos);

public record PhotoTagItem(string RelativePath,PhotoTags Tags);

public record PhotoTags(
    List<string> ObjectTags,
    List<string> SceneTags,
    List<string> MoodTags,
    List<string> ColorTags,
    List<string> TechniqueTags,
    List<string> TextTags,
    string Description)
{
    public int Count => ObjectTags.Count 
                        + SceneTags.Count 
                        + MoodTags.Count 
                        + ColorTags.Count 
                        + TechniqueTags.Count 
                        + TextTags.Count;
}
