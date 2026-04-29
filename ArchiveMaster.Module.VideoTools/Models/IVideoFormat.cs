namespace ArchiveMaster.Models;

public interface IVideoFormat
{
    int StreamCount { get; }
    string FormatShortName { get; }
    string FormatLongName { get; }
    double Duration { get; }
    double TotalBitRate { get; }
}