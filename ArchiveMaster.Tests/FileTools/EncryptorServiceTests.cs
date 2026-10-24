using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.FileTools;

public class EncryptorServiceTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public EncryptorServiceTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public async Task EncryptThenDecrypt_RestoresContentAndFileName()
    {
        string raw = workspace.Dir("raw");
        string encrypted = workspace.Dir("encrypted");
        string restored = workspace.Dir("restored");
        workspace.WriteText("raw/hello.txt", "机密内容");

        var encrypt = CreateService(raw, encrypted, EncryptorConfig.EncryptorTaskType.Encrypt);
        await encrypt.InitializeAsync(CancellationToken.None);
        await encrypt.ExecuteAsync(CancellationToken.None);

        string encryptedFile = Path.Combine(encrypted, "hello.txt" + EncryptorService.EncryptedFileExtension);
        Assert.True(File.Exists(encryptedFile));
        Assert.NotEqual("机密内容", File.ReadAllText(encryptedFile));

        var decrypt = new EncryptorService(appConfig)
        {
            Config = new EncryptorConfig
            {
                Type = EncryptorConfig.EncryptorTaskType.Decrypt,
                EncryptedDir = encrypted,
                RawDir = restored,
                Password = new SecurePassword("pass"),
            },
        };
        await decrypt.InitializeAsync(CancellationToken.None);
        await decrypt.ExecuteAsync(CancellationToken.None);

        Assert.Equal("机密内容", File.ReadAllText(Path.Combine(restored, "hello.txt")));
    }

    [Fact]
    public async Task EncryptDirectoryStructure_StoresMetadataAndRestoresRelativePath()
    {
        string raw = workspace.Dir("raw");
        string encrypted = workspace.Dir("encrypted");
        string restored = workspace.Dir("restored");
        workspace.Dir("raw/sub");
        workspace.WriteText("raw/sub/data.txt", "data");

        var encrypt = new EncryptorService(appConfig)
        {
            Config = new EncryptorConfig
            {
                Type = EncryptorConfig.EncryptorTaskType.Encrypt,
                RawDir = raw,
                EncryptedDir = encrypted,
                Password = new SecurePassword("pass"),
                EncryptDirectoryStructure = true,
            },
        };
        await encrypt.InitializeAsync(CancellationToken.None);
        await encrypt.ExecuteAsync(CancellationToken.None);

        Assert.Contains(Directory.EnumerateFiles(encrypted), p =>
            p.EndsWith(EncryptorService.EncryptedFileMetadataExtension));

        var decrypt = new EncryptorService(appConfig)
        {
            Config = new EncryptorConfig
            {
                Type = EncryptorConfig.EncryptorTaskType.Decrypt,
                EncryptedDir = encrypted,
                RawDir = restored,
                Password = new SecurePassword("pass"),
                EncryptDirectoryStructure = true,
            },
        };
        await decrypt.InitializeAsync(CancellationToken.None);
        await decrypt.ExecuteAsync(CancellationToken.None);

        Assert.Equal("data", File.ReadAllText(Path.Combine(restored, "sub", "data.txt")));
    }

    [Fact]
    public async Task DeleteSourceFiles_RemovesPlainFileAfterEncryption()
    {
        string raw = workspace.Dir("raw");
        string encrypted = workspace.Dir("encrypted");
        string source = workspace.WriteText("raw/delete.txt", "x");

        var service = new EncryptorService(appConfig)
        {
            Config = new EncryptorConfig
            {
                Type = EncryptorConfig.EncryptorTaskType.Encrypt,
                RawDir = raw,
                EncryptedDir = encrypted,
                Password = new SecurePassword("pass"),
                DeleteSourceFiles = true,
            },
        };

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task Decrypt_IgnoresLeftoverTempFiles()
    {
        string raw = workspace.Dir("raw");
        string encrypted = workspace.Dir("encrypted");
        // 加密被强杀后可能留下未替换的半截密文；解密时不能把它当成完整文件
        workspace.WriteText("encrypted/leftover.txt.$ept$.tmp", "stale");
        var decrypt = new EncryptorService(appConfig)
        {
            Config = new EncryptorConfig
            {
                Type = EncryptorConfig.EncryptorTaskType.Decrypt,
                EncryptedDir = encrypted,
                RawDir = raw,
                Password = new SecurePassword("pass"),
            },
        };

        await decrypt.InitializeAsync(CancellationToken.None);

        Assert.Empty(decrypt.ProcessingFiles);
    }

    private EncryptorService CreateService(string raw, string encrypted,
        EncryptorConfig.EncryptorTaskType type)
    {
        return new EncryptorService(appConfig)
        {
            Config = new EncryptorConfig
            {
                Type = type,
                RawDir = raw,
                EncryptedDir = encrypted,
                Password = new SecurePassword("pass"),
                FilenameDuplicationPolicy = FilenameDuplicationPolicy.Overwrite,
            },
        };
    }
}
