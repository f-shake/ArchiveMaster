using ArchiveMaster.Enums;

namespace ArchiveMaster.Models;

public class VideoInfoFFprobeSourceAttribute(
    string key,
    VideoInfoFFprobeSourceType sourceType = VideoInfoFFprobeSourceType.Raw)
    : Attribute
{
    public string Key { get; } = key;
    public VideoInfoFFprobeSourceType SourceType { get; } = sourceType;
}