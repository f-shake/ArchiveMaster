namespace ArchiveMaster.ViewModels;

public class RebuildError(FileSystem.WriteOnceFile file, string error)
{
    public string Error { get; set; } = error;
    public FileSystem.WriteOnceFile File { get; set; } = file;
}