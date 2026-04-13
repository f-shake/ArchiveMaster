using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services;

public interface IService
{
    event EventHandler<MessageUpdateEventArgs> MessageUpdate;

    event EventHandler<ProgressUpdateEventArgs> ProgressUpdate;
        
    public SimpleFileInfo CurrentProcessingFile { get; }
}