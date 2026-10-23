using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.FileTools;

public class DirStructureSyncServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public DirStructureSyncServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task Initialize_MatchesByAllSelectedProperties()
    {
        var time = new DateTime(2020, 1, 2, 3, 4, 5);
        string template = workspace.Dir("template");
        string source = workspace.Dir("source");
        workspace.Dir("template/sub");
        workspace.Dir("source/wrong");
        string templateFile = workspace.WriteText("template/sub/a.txt", "12345");
        string sourceFile = workspace.WriteText("source/wrong/a.txt", "12345");
        File.SetLastWriteTime(templateFile, time);
        File.SetLastWriteTime(sourceFile, time);
        var service = CreateService(template, source, workspace.Dir("target"));

        await service.InitializeAsync(CancellationToken.None);

        Assert.Empty(service.RightPositionFiles);
        Assert.Single(service.WrongPositionFiles);
        Assert.Equal(Path.Combine("sub", "a.txt"), service.WrongPositionFiles[0].Template.RelativePath);
    }

    [Fact]
    public async Task Execute_CopiesWrongPositionFileToTemplatePath()
    {
        var time = new DateTime(2020, 1, 2, 3, 4, 5);
        string template = workspace.Dir("template");
        string source = workspace.Dir("source");
        string target = workspace.Dir("target");
        workspace.Dir("template/sub");
        workspace.Dir("source/wrong");
        string templateFile = workspace.WriteText("template/sub/a.txt", "12345");
        string sourceFile = workspace.WriteText("source/wrong/a.txt", "12345");
        File.SetLastWriteTime(templateFile, time);
        File.SetLastWriteTime(sourceFile, time);
        var service = CreateService(template, source, target);
        await service.InitializeAsync(CancellationToken.None);
        service.ExecutingFiles = service.WrongPositionFiles;

        await service.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(target, "sub", "a.txt")));
    }

    [Fact]
    public void Check_MissingTemplateDirectory_ShouldThrow()
    {
        var config = new DirStructureSyncConfig
        {
            SourceDir = workspace.Dir("source"),
            TemplateDir = workspace.PathOf("missing"),
            TargetDir = workspace.Dir("target"),
            CompareName = true,
        };

        var exception = Assert.ThrowsAny<Exception>(() => config.Check());

        Assert.Contains("模板目录", exception.Message);
    }

    private DirStructureSyncService CreateService(string template, string source, string target)
    {
        return new DirStructureSyncService(appConfig)
        {
            Config = new DirStructureSyncConfig
            {
                TemplateDir = template,
                SourceDir = source,
                TargetDir = target,
                CompareName = true,
                CompareTime = true,
                CompareLength = true,
                Copy = true,
            },
        };
    }
}
