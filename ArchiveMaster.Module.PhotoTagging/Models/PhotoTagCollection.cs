namespace ArchiveMaster.Models;

public class PhotoTagCollection
{
    public List<PhotoTag> Photos { get; set; } = new List<PhotoTag>();
}

public record PhotoTag(string RelativePath, HashSet<string> Tags);