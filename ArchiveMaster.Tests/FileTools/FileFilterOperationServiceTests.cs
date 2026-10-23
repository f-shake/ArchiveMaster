using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.FileTools;

public class FileFilterOperationServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public FileFilterOperationServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task Copy_PreservesDirectoryStructure()
    {
        string source = workspace.Dir("source");
        string target = workspace.Dir("target");
        workspace.Dir("source/sub");
        workspace.WriteText("source/sub/a.txt", "content");
        var service = CreateService(new FileFilterOperationConfig
        {
            SourceDirs = source,
            TargetDir = target,
            Type = FileFilterOperationType.Copy,
            TargetFileNameMode = FileFilterOperationTargetFileNameMode.PreserveDirectoryStructure,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal("content", File.ReadAllText(Path.Combine(target, "sub", "a.txt")));
        Assert.True(File.Exists(Path.Combine(source, "sub", "a.txt")));
    }

    [Fact]
    public async Task FlattenWithRelativePathNames_UsesDashSeparatedName()
    {
        string source = workspace.Dir("source");
        string target = workspace.Dir("target");
        workspace.Dir("source/sub");
        workspace.WriteText("source/sub/a.txt", "content");
        var service = CreateService(new FileFilterOperationConfig
        {
            SourceDirs = source,
            TargetDir = target,
            Type = FileFilterOperationType.Copy,
            TargetFileNameMode = FileFilterOperationTargetFileNameMode.FlattenWithRelativePathNames,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(target, "sub-a.txt")));
    }

    [Fact]
    public async Task Move_RemovesSourceFile()
    {
        string source = workspace.Dir("source");
        string target = workspace.Dir("target");
        workspace.WriteText("source/a.txt", "content");
        var service = CreateService(new FileFilterOperationConfig
        {
            SourceDirs = source,
            TargetDir = target,
            Type = FileFilterOperationType.Move,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(source, "a.txt")));
        Assert.True(File.Exists(Path.Combine(target, "a.txt")));
    }

    [Fact]
    public async Task Hash_WritesSha256ToFileMessage()
    {
        string source = workspace.Dir("source");
        workspace.WriteText("source/a.txt", "content");
        var service = CreateService(new FileFilterOperationConfig
        {
            SourceDirs = source,
            Type = FileFilterOperationType.Hash,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal(64, service.Files[0].Message.Length);
        Assert.All(service.Files[0].Message, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public async Task Delete_RemovesSelectedFile()
    {
        string source = workspace.Dir("source");
        string path = workspace.WriteText("source/a.txt", "content");
        var service = CreateService(new FileFilterOperationConfig
        {
            SourceDirs = source,
            Type = FileFilterOperationType.Delete,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Initialize_FiltersByMinLength()
    {
        string source = workspace.Dir("source");
        workspace.WriteFile("source/small.bin", 3);
        workspace.WriteFile("source/large.bin", 10);
        var service = CreateService(new FileFilterOperationConfig
        {
            SourceDirs = source,
            Type = FileFilterOperationType.Hash,
            MinLength = 5,
        });

        await service.InitializeAsync(CancellationToken.None);

        Assert.Single(service.Files);
        Assert.Equal("large.bin", service.Files[0].Name);
    }

    private FileFilterOperationService CreateService(FileFilterOperationConfig config)
    {
        return new FileFilterOperationService(appConfig) { Config = config };
    }
}
