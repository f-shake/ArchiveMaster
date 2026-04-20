namespace ArchiveMaster.Models;

public record VideoInfo(VideoFormat Format, List<VideoStream> Streams, string RawJson);