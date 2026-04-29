namespace ArchiveMaster.Models;

public record VideoFormat : IVideoFormat
{
    [VideoInfoFFprobeSource("nb_streams")]
    public int StreamCount { get; init; } = -1;

    [VideoInfoFFprobeSource("format_name")]
    public string FormatShortName { get; init; }

    [VideoInfoFFprobeSource("format_long_name")]
    public string FormatLongName { get; init; }

    [VideoInfoFFprobeSource("duration")]
    public double Duration { get; init; } = double.NaN;

    [VideoInfoFFprobeSource("bit_rate")]
    public double TotalBitRate { get; init; } = double.NaN;
}