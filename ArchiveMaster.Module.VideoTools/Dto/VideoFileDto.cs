using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;
using CsvHelper.Configuration.Attributes;

namespace ArchiveMaster.Dto;

public class VideoFileDto(SimpleFileInfo file, VideoFormat format)
    : ISimpleFileInfo, IVideoFormat
{
    [Name("文件名")]
    public string Name => file.Name;

    [Name("文件路径")]
    public string Path => file.Path;

    [Name("文件修改时间")]
    public DateTime Time => file.Time;

    [Name("文件大小")]
    public long Length => file.Length;

    [Ignore]
    public string RelativePath => file.RelativePath;

    [Ignore]
    public string TopDirectory => file.TopDirectory;

    [Ignore]
    public bool IsDir => file.IsDir;

    [Name("流数量")]
    public int StreamCount => format.StreamCount;

    [Name("格式名称（短）")]
    public string FormatShortName => format.FormatShortName;

    [Name("格式名称（长）")]
    public string FormatLongName => format.FormatLongName;

    [Name("总码率")]
    public double TotalBitRate => format.TotalBitRate;

    [Name("时长")]
    public double Duration => format.Duration;
}