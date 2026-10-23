using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Tests.WriteOnceArchive;

public class WriteOnceArchiveTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public WriteOnceArchiveTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public void GetHashFileMap_GroupsDuplicateHashes()
    {
        var files = new[]
        {
            new WriteOnceFileInfo("a.txt", "HASH", 3, DateTime.Now),
            new WriteOnceFileInfo("b.txt", "HASH", 3, DateTime.Now),
            new WriteOnceFileInfo("c.txt", "OTHER", 4, DateTime.Now),
        };

        var (tree, map) = WriteOnceArchiveHelper.GetHashFileMap(files);

        Assert.Equal(3, tree.SubFileCount);
        Assert.IsType<List<WriteOnceFile>>(map["HASH"]);
        Assert.Equal(2, ((List<WriteOnceFile>)map["HASH"]).Count);
        Assert.IsType<WriteOnceFile>(map["OTHER"]);
    }

    [Fact]
    public async Task ReadPackageInfo_MissingFile_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            WriteOnceArchiveHelper.ReadPackageInfoAsync([workspace.Dir("package")], null));
    }

    [Fact]
    public async Task ReadPackageFiles_ReportsLostAndUnreferenced()
    {
        string package = workspace.Dir("package");
        string knownHash = Hash("known");
        string extraHash = Hash("extra");
        var info = new WriteOncePackageInfo(
            [new WriteOnceFileInfo("known.txt", knownHash, 5, DateTime.Now)],
            5,
            DateTime.Now,
            [knownHash],
            new FileFilterRule());
        await File.WriteAllTextAsync(Path.Combine(package, WriteOnceArchiveParameters.PackageInfoFileName),
            JsonSerializer.Serialize(info), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(package, extraHash), "extra", TestContext.Current.CancellationToken);

        var (tree, files, report) = await WriteOnceArchiveHelper.ReadPackageFilesAsync(
            info, [package], true, TestContext.Current.CancellationToken);

        Assert.Equal(1, tree.SubFileCount);
        Assert.Contains(files, p => p.ErrorNoPhysicalFile);
        Assert.Contains(files, p => p.ErrorNotInFileList);
        Assert.Equal(1, report.LostFiles.Count);
        Assert.Equal(1, report.UnreferencedFiles.Count);
    }

    [Fact]
    public async Task PackingAndVerify_CopyRoundTrip()
    {
        string source = workspace.Dir("source");
        workspace.WriteText("source/a.txt", "alpha");
        workspace.WriteText("source/b.txt", "beta");
        string target = workspace.Dir("target");
        var packing = new PackingService(appConfig)
        {
            Config = new PackingConfig
            {
                SourceDir = source,
                TargetDir = target,
                PackingType = PackingType.Copy,
                PackageSizeMB = 1,
            },
        };

        await packing.InitializeAsync(CancellationToken.None);
        await packing.ExecuteAsync(CancellationToken.None);

        string package = Path.Combine(target, "1");
        Assert.True(File.Exists(Path.Combine(package, WriteOnceArchiveParameters.PackageInfoFileName)));

        var verify = new VerifyService(appConfig)
        {
            Config = new VerifyConfig { PackageDir = package },
        };
        await verify.InitializeAsync(CancellationToken.None);
        await verify.ExecuteAsync(CancellationToken.None);

        Assert.All(verify.Files, file => Assert.Equal(ProcessStatus.Success, file.Status));
    }

    [Fact]
    public async Task Rebuild_RestoresOriginalContent()
    {
        string source = workspace.Dir("source");
        workspace.WriteText("source/dir/a.txt", "alpha");
        string target = workspace.Dir("target");
        var packing = new PackingService(appConfig)
        {
            Config = new PackingConfig
            {
                SourceDir = source,
                TargetDir = target,
                PackingType = PackingType.Copy,
                PackageSizeMB = 1,
            },
        };
        await packing.InitializeAsync(CancellationToken.None);
        await packing.ExecuteAsync(CancellationToken.None);
        string package = Path.Combine(target, "1");
        string restored = workspace.Dir("restored");
        var rebuild = new RebuildService(appConfig)
        {
            Config = new RebuildConfig
            {
                SourceDirs = package,
                TargetDir = restored,
            },
        };

        await rebuild.InitializeAsync(CancellationToken.None);
        await rebuild.ExecuteAsync(CancellationToken.None);

        Assert.Equal("alpha", File.ReadAllText(Path.Combine(restored, "dir", "a.txt")));
    }

    private static string Hash(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
