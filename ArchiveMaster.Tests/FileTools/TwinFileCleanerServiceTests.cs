using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;
using FzLib.Text;

namespace ArchiveMaster.Tests.FileTools;

public class TwinFileCleanerServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public TwinFileCleanerServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task Initialize_FindsAuxiliaryFileByMasterExtension()
    {
        string dir = workspace.Dir("source");
        workspace.WriteBytes("source/photo.DNG", [1, 2, 3]);
        workspace.WriteText("source/photo.JPG", "jpeg");
        var service = CreateService(dir);

        await service.InitializeAsync(CancellationToken.None);

        Assert.Single(service.DeletingFiles);
        Assert.Equal("photo.JPG", service.DeletingFiles[0].Name);
    }

    [Fact]
    public async Task Execute_DeletesCheckedAuxiliaryFiles()
    {
        string dir = workspace.Dir("source");
        workspace.WriteBytes("source/photo.DNG", [1, 2, 3]);
        string jpg = workspace.WriteText("source/photo.JPG", "jpeg");
        var service = CreateService(dir);

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.False(File.Exists(jpg));
        Assert.True(File.Exists(Path.Combine(dir, "photo.DNG")));
    }

    [Fact]
    public async Task Initialize_DoesNotDeleteMasterFile()
    {
        string dir = workspace.Dir("source");
        string dng = workspace.WriteBytes("source/photo.DNG", [1, 2, 3]);
        var service = CreateService(dir);

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(dng));
    }

    private TwinFileCleanerService CreateService(string dir)
    {
        return new TwinFileCleanerService(appConfig)
        {
            Config = new TwinFileCleanerConfig
            {
                Dir = dir,
                MasterExtensions = new ObservableStringList(["DNG"]),
                DeletingPatterns = new ObservableStringList(["{Name}.JPG"]),
            },
        };
    }
}
