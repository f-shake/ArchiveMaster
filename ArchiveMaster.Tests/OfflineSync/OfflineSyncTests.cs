using System.Collections.ObjectModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Tests.OfflineSync;

public class OfflineSyncTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public OfflineSyncTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public void ZipService_RoundTripsObject()
    {
        string path = workspace.PathOf("data.zip");
        var file = new SyncFileInfo { Name = "a.txt", Path = workspace.PathOf("a.txt") };
        file.SetRelativePath("a.txt");
        var model = new Step1Model { Files = [file] };

        ZipService.WriteToZip(model, path);
        var restored = ZipService.ReadFromZip<Step1Model>(path);

        Assert.Single(restored.Files);
        Assert.Equal("a.txt", restored.Files[0].Name);
    }

    [Fact]
    public void ZipService_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ZipService.ReadFromZip<Step1Model>(workspace.PathOf("missing.zip")));
    }

    [Fact]
    public async Task Step1_CreatesSnapshotWithTopDirectory()
    {
        string dir = workspace.Dir("sync");
        workspace.Dir("sync/sub");
        workspace.WriteText("sync/a.txt", "a");
        workspace.WriteText("sync/sub/b.txt", "b");
        string snapshot = workspace.PathOf("snapshot.os1");
        var service = new Step1Service(appConfig)
        {
            Config = new OfflineSyncStep1Config
            {
                SyncDirs = new ObservableCollection<string> { dir },
                OutputFile = snapshot,
            },
        };

        await service.ExecuteAsync(CancellationToken.None);

        var model = ZipService.ReadFromZip<Step1Model>(snapshot);
        Assert.Equal(2, model.Files.Count);
        Assert.All(model.Files, file => Assert.Equal(dir, file.TopDirectory));
    }

    [Fact]
    public async Task Step1_DuplicateTopDirectoryName_Throws()
    {
        string first = workspace.Dir("a", "same");
        string second = workspace.Dir("b", "same");
        workspace.WriteText("a/same/a.txt", "a");
        workspace.WriteText("b/same/b.txt", "b");
        var service = new Step1Service(appConfig)
        {
            Config = new OfflineSyncStep1Config
            {
                SyncDirs = new ObservableCollection<string> { first, second },
                OutputFile = workspace.PathOf("snapshot.os1"),
            },
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteAsync(CancellationToken.None));

        Assert.Contains("重复", exception.Message);
    }

    [Fact]
    public async Task MatchLocalAndOffsiteDirs_MatchesByDirectoryName()
    {
        string localRoot = workspace.Dir("local-root");
        string offsite = workspace.Dir("a", "remote");
        workspace.Dir("local-root/remote");
        string file = workspace.WriteText("a/remote/a.txt", "a");
        string snapshot = DataFactory.WriteSnapshot(workspace.PathOf("snapshot.os1"),
            new SyncFileInfo(new FileInfo(file), offsite));

        var result = await Step2Service.MatchLocalAndOffsiteDirsAsync(snapshot, [localRoot]);

        Assert.Single(result);
        Assert.Equal(Path.Combine(localRoot, "remote"), result[0].LocalDir);
    }

    [Fact]
    public async Task Step2_Initialize_ComputesAddModifyDelete()
    {
        string remote = workspace.Dir("a", "remote");
        string local = workspace.Dir("b", "remote");
        var commonTime = new DateTime(2020, 1, 1, 10, 0, 0);
        string oldRemote = workspace.WriteText("a/remote/old.txt", "old");
        string modifiedRemote = workspace.WriteText("a/remote/modified.txt", "remote");
        string deletedRemote = workspace.WriteText("a/remote/deleted.txt", "deleted");
        string oldLocal = workspace.WriteText("b/remote/old.txt", "old");
        string modifiedLocal = workspace.WriteText("b/remote/modified.txt", "local");
        File.SetLastWriteTime(oldRemote, commonTime);
        File.SetLastWriteTime(oldLocal, commonTime);
        File.SetLastWriteTime(modifiedRemote, commonTime.AddMinutes(1));
        File.SetLastWriteTime(deletedRemote, commonTime);
        File.SetLastWriteTime(modifiedLocal, commonTime.AddMinutes(2));
        workspace.WriteText("b/remote/new.txt", "new");
        string snapshot = DataFactory.WriteSnapshot(workspace.PathOf("snapshot.os1"),
            new SyncFileInfo(new FileInfo(oldRemote), remote),
            new SyncFileInfo(new FileInfo(modifiedRemote), remote),
            new SyncFileInfo(new FileInfo(deletedRemote), remote));
        string patch = workspace.Dir("patch");
        var service = new Step2Service(appConfig)
        {
            Config = new OfflineSyncStep2Config
            {
                OffsiteSnapshot = snapshot,
                PatchDir = patch,
                MatchingDirs = new ObservableCollection<LocalAndOffsiteDir>
                {
                    new() { OffsiteDir = remote, LocalDir = local },
                },
                MaxTimeToleranceSecond = 0,
            },
        };

        await service.InitializeAsync(CancellationToken.None);

        Assert.Contains(service.UpdateFiles, p => p.Name == "new.txt" && p.UpdateType == FileUpdateType.Add);
        Assert.Contains(service.UpdateFiles, p => p.Name == "modified.txt" && p.UpdateType == FileUpdateType.Modify);
        Assert.Contains(service.UpdateFiles, p => p.Name == "deleted.txt" && p.UpdateType == FileUpdateType.Delete);
        var oldFileUpdates = service.UpdateFiles.Where(p => p.Name == "old.txt").ToList();
        Assert.True(oldFileUpdates.Count == 0,
            string.Join("; ", oldFileUpdates.Select(p =>
                $"{p.UpdateType} {p.Time:O} {p.Length} {p.Path} {p.RelativePath}")));
    }

    [Fact]
    public async Task Step3_InitializeAndExecute_AppliesAddPatch()
    {
        string targetDir = workspace.Dir("target");
        string patchDir = workspace.Dir("patch");
        workspace.WriteText("patch/temp.bin", "new content");
        var syncFile = new SyncFileInfo
        {
            Name = "a.txt",
            Path = Path.Combine(targetDir, "a.txt"),
            TopDirectory = targetDir,
            Length = "new content".Length,
            Time = new DateTime(2020, 1, 1, 10, 0, 0),
            UpdateType = FileUpdateType.Add,
            TempName = "temp.bin",
        };
        syncFile.SetRelativePath("a.txt");
        ZipService.WriteToZip(new Step2Model
        {
            Files = [syncFile],
            LocalDirectories = new Dictionary<string, List<string>>(),
        }, Path.Combine(patchDir, "file.os2"));
        var service = new Step3Service(appConfig)
        {
            Config = new OfflineSyncStep3Config { PatchDir = patchDir },
        };

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal("new content", File.ReadAllText(Path.Combine(targetDir, "a.txt")));
        Assert.Equal(new DateTime(2020, 1, 1, 10, 0, 0),
            File.GetLastWriteTime(Path.Combine(targetDir, "a.txt")));
    }
}
