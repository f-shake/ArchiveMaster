using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.FileTools;

public class TimeClassifyServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public TimeClassifyServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task Initialize_SplitsFilesByInterval()
    {
        string dir = workspace.Dir("src");
        var start = new DateTime(2020, 1, 1, 10, 0, 0);
        SetTime(workspace.WriteText("src/a.txt", "a"), start);
        SetTime(workspace.WriteText("src/b.txt", "b"), start.AddMinutes(30));
        SetTime(workspace.WriteText("src/c.txt", "c"), start.AddHours(2));

        var service = new TimeClassifyService(appConfig)
        {
            Config = new TimeClassifyConfig { Dir = dir, MinTimeInterval = TimeSpan.FromHours(1) },
        };

        await service.InitializeAsync(CancellationToken.None);

        Assert.Equal(2, service.TargetDirs.Count);
        Assert.Equal(2, service.TargetDirs[0].Subs.Count);
        Assert.Single(service.TargetDirs[1].Subs);
    }

    [Fact]
    public async Task IntervalEqual_DoesNotStartNewGroup()
    {
        string dir = workspace.Dir("src");
        var start = new DateTime(2020, 1, 1, 10, 0, 0);
        SetTime(workspace.WriteText("src/a.txt", "a"), start);
        SetTime(workspace.WriteText("src/b.txt", "b"), start.AddMinutes(30));

        var service = new TimeClassifyService(appConfig)
        {
            Config = new TimeClassifyConfig { Dir = dir, MinTimeInterval = TimeSpan.FromMinutes(30) },
        };

        await service.InitializeAsync(CancellationToken.None);

        Assert.Single(service.TargetDirs);
        Assert.Equal(2, service.TargetDirs[0].Subs.Count);
    }

    [Fact]
    public async Task Execute_MovesFilesIntoDatedDirectories()
    {
        string dir = workspace.Dir("src");
        var time = new DateTime(2020, 2, 3, 4, 5, 6);
        SetTime(workspace.WriteText("src/a.txt", "a"), time);
        var service = new TimeClassifyService(appConfig)
        {
            Config = new TimeClassifyConfig { Dir = dir, MinTimeInterval = TimeSpan.FromHours(1) },
        };

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        string targetDir = Path.Combine(dir, "20200203-040506");
        Assert.True(File.Exists(Path.Combine(targetDir, "a.txt")));
        Assert.False(File.Exists(Path.Combine(dir, "a.txt")));
    }

    [Fact]
    public async Task Initialize_EmptyDirectory_ThrowsChineseMessage()
    {
        string dir = workspace.Dir("empty");
        var service = new TimeClassifyService(appConfig)
        {
            Config = new TimeClassifyConfig { Dir = dir },
        };

        var exception = await Assert.ThrowsAsync<Exception>(() => service.InitializeAsync(CancellationToken.None));

        Assert.Contains("目录为空", exception.Message);
    }

    private static void SetTime(string path, DateTime time)
    {
        File.SetLastWriteTime(path, time);
    }
}
