using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.FileTools;

public class DuplicateFileCleanupServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public DuplicateFileCleanupServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task Initialize_FindsDuplicateWithSameNameLengthAndTime()
    {
        string reference = workspace.Dir("reference");
        string cleaning = workspace.Dir("cleaning");
        string referenceFile = workspace.WriteText("reference/a.txt", "same");
        string cleaningFile = workspace.WriteText("cleaning/a.txt", "same");
        var time = new DateTime(2020, 1, 1, 10, 0, 0);
        File.SetLastWriteTime(referenceFile, time);
        File.SetLastWriteTime(cleaningFile, time);
        var service = new DuplicateFileCleanupService(appConfig)
        {
            Config = new DuplicateFileCleanupConfig
            {
                ReferenceDir = reference,
                CleaningDir = cleaning,
                CompareName = true,
                CompareLength = true,
                CompareTime = true,
                TimeToleranceSecond = 0,
            },
        };

        await service.InitializeAsync(CancellationToken.None);

        Assert.NotEmpty(service.GetInitializedFiles());
    }
}
