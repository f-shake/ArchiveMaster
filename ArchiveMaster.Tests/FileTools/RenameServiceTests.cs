using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.FileTools;

public class RenameServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public RenameServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task ReplaceMatched_RenamesOnlyMatchedPart()
    {
        string dir = workspace.Dir("src");
        workspace.WriteText("src/abc.txt", "x");
        var service = CreateService(dir, new RenameConfig
        {
            Dir = dir,
            SearchPattern = "b",
            ReplacePattern = "X",
            RenameMode = RenameMode.ReplaceMatched,
            SearchMode = SearchMode.Contain,
            IncludeSubDirs = false,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(dir, "aXc.txt")));
        Assert.False(File.Exists(Path.Combine(dir, "abc.txt")));
    }

    [Theory]
    [InlineData(RenameMode.ReplaceExtension, "dat", "abc.dat")]
    [InlineData(RenameMode.ReplaceName, "renamed", "renamed.txt")]
    [InlineData(RenameMode.ReplaceAll, "renamed.txt", "renamed.txt")]
    public async Task RenameModes_ProduceExpectedName(RenameMode mode, string replacePattern, string expected)
    {
        string dir = workspace.Dir("src");
        workspace.WriteText("src/abc.txt", "x");
        var service = CreateService(dir, new RenameConfig
        {
            Dir = dir,
            SearchPattern = "abc",
            ReplacePattern = replacePattern,
            RenameMode = mode,
            SearchMode = SearchMode.EqualWithName,
            IncludeSubDirs = false,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(dir, expected)));
    }

    [Fact]
    public async Task RegexSearch_ReplacesMatchedText()
    {
        string dir = workspace.Dir("src");
        workspace.WriteText("src/photo-001.jpg", "x");
        var service = CreateService(dir, new RenameConfig
        {
            Dir = dir,
            SearchPattern = @"\d+",
            ReplacePattern = "number",
            RenameMode = RenameMode.ReplaceMatched,
            SearchMode = SearchMode.Regex,
            IncludeSubDirs = false,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(dir, "photo-number.jpg")));
    }

    [Fact]
    public async Task ManualMapping_RenamesUsingTabSeparatedLines()
    {
        string dir = workspace.Dir("src");
        string path = workspace.WriteText("src/old.txt", "x");
        var service = CreateService(dir, new RenameConfig
        {
            Dir = dir,
            Manual = true,
            ManualMaps = $"{path}\tnew.txt",
            IncludeSubDirs = false,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(dir, "new.txt")));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task IncludeSubDirectories_RenamesNestedFiles()
    {
        string dir = workspace.Dir("src");
        workspace.Dir("src/sub");
        workspace.WriteText("src/sub/abc.txt", "x");
        var service = CreateService(dir, new RenameConfig
        {
            Dir = dir,
            SearchPattern = "abc",
            ReplacePattern = "xyz",
            RenameMode = RenameMode.ReplaceMatched,
            SearchMode = SearchMode.Contain,
            IncludeSubDirs = true,
        });

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(dir, "sub", "xyz.txt")));
    }

    private RenameService CreateService(string dir, RenameConfig config)
    {
        return new RenameService(appConfig) { Config = config };
    }
}
