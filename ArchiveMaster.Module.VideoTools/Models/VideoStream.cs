using ArchiveMaster.Enums;

namespace ArchiveMaster.Models;

public record VideoStream
{
    [VideoInfoFFprobeSource("index")]
    public int Index { get; init; } = -1;

    [VideoInfoFFprobeSource("codec_type")]
    public string CodecTypeName { get; init; }

    public StreamCodecType CodecType
    {
        get
        {
            return CodecTypeName switch
            {
                "video" => StreamCodecType.Video,
                "audio" => StreamCodecType.Audio,
                "subtitle" => StreamCodecType.Subtitle,
                "data" => StreamCodecType.Data,
                "attachment" => StreamCodecType.Attachment,
                _ => StreamCodecType.Unknown
            };
        }
    }

    [VideoInfoFFprobeSource("codec_name")]
    public string CodecShortName { get; init; }

    [VideoInfoFFprobeSource("codec_long_name")]
    public string CodecLongName { get; init; }

    [VideoInfoFFprobeSource("duration")]
    public double Duration { get; init; } = double.NaN;

    [VideoInfoFFprobeSource("profile")]
    public string Profile { get; init; }

    [VideoInfoFFprobeSource("width")]
    public int Width { get; init; } = -1;

    [VideoInfoFFprobeSource("height")]
    public int Height { get; init; } = -1;

    public string Size => Width <= 0 || Height <= 0 ? "" : $"{Width} × {Height}";

    [VideoInfoFFprobeSource("pix_fmt")]
    public string Format { get; init; }

    [VideoInfoFFprobeSource("color_range")]
    public string ColorRange { get; init; }

    [VideoInfoFFprobeSource("color_space")]
    public string ColorSpace { get; init; }

    [VideoInfoFFprobeSource("r_frame_rate", VideoInfoFFprobeSourceType.Fraction)]
    public double TargetFrameRate { get; init; } = double.NaN;

    [VideoInfoFFprobeSource("avg_frame_rate", VideoInfoFFprobeSourceType.Fraction)]
    public double ActualFrameRate { get; init; } = double.NaN;

    [VideoInfoFFprobeSource("nb_frames")]
    public int FrameCount { get; init; } = -1;

    [VideoInfoFFprobeSource("bit_rate")]
    public double BitRate { get; init; } = double.NaN;

    [VideoInfoFFprobeSource("channels")]
    public int Channels { get; init; } = -1;

    [VideoInfoFFprobeSource("channel_layout")]
    public string ChannelLayout { get; init; }
    
    [VideoInfoFFprobeSource("sample_rate")]
    public int SampleRate { get; init; }
}