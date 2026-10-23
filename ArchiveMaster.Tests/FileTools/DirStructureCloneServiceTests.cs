using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.FileTools;

public class DirStructureCloneServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public DirStructureCloneServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task Export_WritesTreeJsonWithRelativePaths()
    {
        string source = workspace.Dir("source");
        workspace.Dir("source/sub");
        workspace.WriteText("source/sub/a.txt", "content");
        string json = workspace.PathOf("structure.json");
        var service = new DirStructureCloneService(appConfig)
        {
            Config = new DirStructureCloneConfig
            {
                SourceDirOrFile = source,
                TargetDirOrFile = json,
                ExportStructureFile = true,
            },
        };

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        string content = await File.ReadAllTextAsync(json, TestContext.Current.CancellationToken);
        Assert.Contains("sub", content);
        Assert.Contains("a.txt", content);
    }

    [Fact]
    public async Task Import_ReadsTreeJsonAndExportsSameStructure()
    {
        string source = workspace.Dir("source");
        workspace.Dir("source/sub");
        workspace.WriteText("source/sub/a.txt", "content");
        string json = workspace.PathOf("structure.json");
        var export = new DirStructureCloneService(appConfig)
        {
            Config = new DirStructureCloneConfig
            {
                SourceDirOrFile = source,
                TargetDirOrFile = json,
                ExportStructureFile = true,
            },
        };
        await export.InitializeAsync(CancellationToken.None);
        await export.ExecuteAsync(CancellationToken.None);

        string copy = workspace.PathOf("structure-copy.json");
        var import = new DirStructureCloneService(appConfig)
        {
            Config = new DirStructureCloneConfig
            {
                SourceDirOrFile = json,
                InputStructureFile = true,
                TargetDirOrFile = copy,
                ExportStructureFile = true,
            },
        };
        await import.InitializeAsync(CancellationToken.None);
        await import.ExecuteAsync(CancellationToken.None);

        Assert.Contains("a.txt", await File.ReadAllTextAsync(copy, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Import_FlattenReturnsTreeFiles()
    {
        string source = workspace.Dir("source");
        workspace.Dir("source/sub");
        workspace.WriteText("source/sub/a.txt", "content");
        string json = workspace.PathOf("structure.json");
        var export = new DirStructureCloneService(appConfig)
        {
            Config = new DirStructureCloneConfig
            {
                SourceDirOrFile = source,
                TargetDirOrFile = json,
                ExportStructureFile = true,
            },
        };
        await export.InitializeAsync(CancellationToken.None);
        await export.ExecuteAsync(CancellationToken.None);

        var import = new DirStructureCloneService(appConfig)
        {
            Config = new DirStructureCloneConfig
            {
                SourceDirOrFile = json,
                InputStructureFile = true,
            },
        };
        await import.InitializeAsync(CancellationToken.None);

        Assert.Single(import.GetInitializedFiles());
    }
}
