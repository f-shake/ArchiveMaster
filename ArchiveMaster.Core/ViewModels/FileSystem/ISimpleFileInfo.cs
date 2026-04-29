namespace ArchiveMaster.ViewModels.FileSystem;

public interface ISimpleFileInfo
{
    public string Name { get; }
    public string Path { get; }
    public string RelativePath { get; }
    public string TopDirectory { get; }
    public bool IsDir { get; }
    public long Length { get; }
    public DateTime Time { get; }
}