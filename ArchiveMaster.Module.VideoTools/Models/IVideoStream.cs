using ArchiveMaster.Enums;

namespace ArchiveMaster.Models;

public interface IVideoStream
{
    int Index { get; }
    string CodecTypeName { get; }
    StreamCodecType CodecType { get; }
    string CodecShortName { get; }
    string CodecLongName { get; }
    double Duration { get; }
    string Profile { get; }
    int Width { get; }
    int Height { get; }
    string Format { get; }
    string ColorRange { get; }
    string ColorSpace { get; }
    double TargetFrameRate { get; }
    double ActualFrameRate { get; }
    int FrameCount { get; }
    double BitRate { get; }
    int Channels { get; }
    string ChannelLayout { get; }
    int SampleRate { get; }
}