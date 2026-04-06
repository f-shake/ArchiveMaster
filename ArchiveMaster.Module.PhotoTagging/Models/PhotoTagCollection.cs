namespace ArchiveMaster.Models;

public record PhotoTagCollection(List<PhotoTag> Photos);

public record PhotoTag(string RelativePath, List<TagInfo> Tags);

public record TagInfo(string Tag, int Votes);