using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.FileTools;

public class FileReadabilityScannerServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public FileReadabilityScannerServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task Execute_EmptyFile_IsWarned()
    {
        string dir = workspace.Dir("src");
        workspace.WriteFile("src/empty.bin", 0);
        var service = CreateService(dir);

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ProcessStatus.Warn, service.Files[0].Status);
        Assert.Contains("文件为空", service.Files[0].Message);
        Assert.True(service.Files[0].IsReadable);
    }

    [Fact]
    public async Task Execute_AllZeroFile_IsWarnedForLowBitRatio()
    {
        string dir = workspace.Dir("src");
        workspace.WriteFile("src/zero.bin", 1024);
        var service = CreateService(dir);

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ProcessStatus.Warn, service.Files[0].Status);
        Assert.True(service.Files[0].IsReadable);
        Assert.Equal(0, service.Files[0].BitOneCount);
    }

    [Fact]
    public async Task Execute_NormalFile_CountsBitsAndSucceeds()
    {
        string dir = workspace.Dir("src");
        workspace.WriteText("src/text.txt", "Hello, ArchiveMaster!");
        var service = CreateService(dir);

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ProcessStatus.Success, service.Files[0].Status);
        Assert.True(service.Files[0].IsReadable);
        Assert.NotNull(service.Files[0].BitOneCount);
    }

    private FileReadabilityScannerService CreateService(string dir)
    {
        return new FileReadabilityScannerService(appConfig)
        {
            Config = new FileReadabilityScannerConfig { Dir = dir },
        };
    }
}
