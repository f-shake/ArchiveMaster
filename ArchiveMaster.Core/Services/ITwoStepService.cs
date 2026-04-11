using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services;

public interface ITwoStepService : IService
{
    Task ExecuteAsync(CancellationToken ct = default);

    IEnumerable<SimpleFileInfo> GetExecutedFiles();

    IEnumerable<SimpleFileInfo> GetInitializedFiles();

    Task InitializeAsync(CancellationToken ct = default);
}