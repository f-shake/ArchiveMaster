using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.FileTools;

public class LinkDeduplicationServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public LinkDeduplicationServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task Initialize_GroupsFilesWithSameHash()
    {
        string dir = workspace.Dir("source");
        workspace.WriteText("source/a.txt", "same content");
        workspace.WriteText("source/b.txt", "same content");
        workspace.WriteText("source/c.txt", "different content");
        var service = new LinkDeduplicationService(appConfig)
        {
            Config = new LinkDeduplicationConfig { Dir = dir },
        };

        await service.InitializeAsync(CancellationToken.None);

        Assert.Single(service.TreeRoot.SubDirs);
        Assert.Equal(2, service.TreeRoot.SubDirs[0].SubFileCount);
        Assert.Equal(2, service.TreeRoot.SubDirs[0].SubFiles.Count);
    }

    [Fact]
    public async Task Initialize_AllowsDifferentTimesOnlyWhenConfigured()
    {
        string dir = workspace.Dir("source");
        string a = workspace.WriteText("source/a.txt", "same content");
        string b = workspace.WriteText("source/b.txt", "same content");
        File.SetLastWriteTime(a, new DateTime(2020, 1, 1));
        File.SetLastWriteTime(b, new DateTime(2021, 1, 1));
        var strict = new LinkDeduplicationService(appConfig)
        {
            Config = new LinkDeduplicationConfig { Dir = dir, AllowDifferentTime = false },
        };

        await strict.InitializeAsync(CancellationToken.None);

        Assert.False(strict.TreeRoot.SubDirs[0].IsChecked);
        Assert.Contains("修改日期不同", strict.TreeRoot.SubDirs[0].Message);

        // 分组未勾选时，执行阶段不能创建硬链接；否则 b 的修改时间会被 a 覆盖。
        await strict.ExecuteAsync(CancellationToken.None);

        Assert.Equal(new DateTime(2020, 1, 1), File.GetLastWriteTime(a));
        Assert.Equal(new DateTime(2021, 1, 1), File.GetLastWriteTime(b));
    }

    [Fact]
    public async Task Initialize_AllowsDifferentTimesWhenConfigured()
    {
        string dir = workspace.Dir("source");
        string a = workspace.WriteText("source/a.txt", "same content");
        string b = workspace.WriteText("source/b.txt", "same content");
        File.SetLastWriteTime(a, new DateTime(2020, 1, 1));
        File.SetLastWriteTime(b, new DateTime(2021, 1, 1));
        var service = new LinkDeduplicationService(appConfig)
        {
            Config = new LinkDeduplicationConfig { Dir = dir, AllowDifferentTime = true },
        };

        await service.InitializeAsync(CancellationToken.None);

        Assert.True(service.TreeRoot.SubDirs[0].IsChecked);
    }

    [Fact]
    public async Task Execute_ReplacesDuplicatesWithHardLinks()
    {
        string dir = workspace.Dir("source");
        string a = workspace.WriteText("source/a.txt", "same content");
        string b = workspace.WriteText("source/b.txt", "same content");
        File.SetLastWriteTime(a, new DateTime(2020, 1, 1));
        File.SetLastWriteTime(b, new DateTime(2020, 1, 1));
        var service = new LinkDeduplicationService(appConfig)
        {
            Config = new LinkDeduplicationConfig { Dir = dir },
        };

        await service.InitializeAsync(CancellationToken.None);
        var group = Assert.Single(service.TreeRoot.SubDirs);
        Assert.True(group.IsChecked);

        await service.ExecuteAsync(CancellationToken.None);

        // 覆盖建链接、删除原文件、把临时链接改回原名这一整条路径
        Assert.All(group.SubFiles, file => Assert.Equal(ProcessStatus.Success, file.Status));
        Assert.Equal("same content", File.ReadAllText(a));
        Assert.Equal("same content", File.ReadAllText(b));
        Assert.Empty(Directory.EnumerateFiles(dir, "*.lnk"));
    }
}
