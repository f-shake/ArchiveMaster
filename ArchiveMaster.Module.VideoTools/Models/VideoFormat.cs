namespace ArchiveMaster.Models;

public record VideoFormat
{
    [VideoInfoFFprobeSource("nb_streams")]
    public int StreamCount { get; init; } = 0;

    [VideoInfoFFprobeSource("format_name")]
    public string FormatShortName { get; init; }

    [VideoInfoFFprobeSource("format_long_name")]
    public string FormatLongName { get; init; }

    [VideoInfoFFprobeSource("duration")]
    public double Duration { get; init; } = 0;

    [VideoInfoFFprobeSource("size")]
    public long Length { get; init; } = 0;

    [VideoInfoFFprobeSource("bit_rate")]
    public double BitRate { get; init; } = 0;
}