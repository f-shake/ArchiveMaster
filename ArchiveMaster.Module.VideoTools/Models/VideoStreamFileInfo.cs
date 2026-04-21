using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels.FileSystem;
using CsvHelper.Configuration.Attributes;

namespace ArchiveMaster.Models;

public class VideoStreamFileInfo(SimpleFileInfo file, VideoFormat format, VideoStream stream)
    : ISimpleFileInfo, IVideoFormat, IVideoStream
{
    [Name("文件名")]
    public string Name => file.Name;

    [Name("文件路径")]
    public string Path => file.Path;

    [Name("文件修改时间")]
    public DateTime Time => file.Time;

    [Name("文件大小")]
    public long Length => file.Length;

    [Name("流数量")]
    public int StreamCount => format.StreamCount;

    [Name("格式名称（短）")]
    public string FormatShortName => format.FormatShortName;

    [Name("格式名称（长）")]
    public string FormatLongName => format.FormatLongName;

    [Name("总码率")]
    public double TotalBitRate => format.TotalBitRate;

    [Name("流索引")]
    public int Index => stream.Index;

    [Name("编解码类型")]
    public string CodecTypeName => stream.CodecTypeName;

    [Name("编解码器类型")]
    public StreamCodecType CodecType => stream.CodecType;

    [Name("编解码器短名称")]
    public string CodecShortName => stream.CodecShortName;

    [Name("编解码器长名称")]
    public string CodecLongName => stream.CodecLongName;

    [Name("时长")]
    public double Duration => stream.Duration;

    [Name("编码配置")]
    public string Profile => stream.Profile;

    [Name("宽度")]
    public int Width => stream.Width;

    [Name("高度")]
    public int Height => stream.Height;

    [Name("像素格式")]
    public string Format => stream.Format;

    [Name("色彩范围")]
    public string ColorRange => stream.ColorRange;

    [Name("色彩空间")]
    public string ColorSpace => stream.ColorSpace;

    [Name("目标帧率")]
    public double TargetFrameRate => stream.TargetFrameRate;

    [Name("实际帧率")]
    public double ActualFrameRate => stream.ActualFrameRate;

    [Name("帧总数")]
    public int FrameCount => stream.FrameCount;

    [Name("码率")]
    public double BitRate => stream.BitRate;

    [Name("通道数")]
    public int Channels => stream.Channels;

    [Name("通道布局")]
    public string ChannelLayout => stream.ChannelLayout;

    [Name("采样率")]
    public int SampleRate => stream.SampleRate;

    [Ignore]
    public string RelativePath => file.RelativePath;

    [Ignore]
    public string TopDirectory => file.TopDirectory;

    [Ignore]
    public bool IsDir => file.IsDir;
}