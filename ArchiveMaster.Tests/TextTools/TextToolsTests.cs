using System.Text;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Tests.TextTools;

public class TextToolsTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public TextToolsTests()
    {
        appConfig = TestAppConfig.Create(workspace);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public void TextEncryptor_RoundTripsAndKeepsPunctuation()
    {
        var encryptor = new TextEncryptor("password");
        const string plain = "你好, ArchiveMaster! 123";

        string encrypted = encryptor.Encrypt(plain);

        Assert.NotEqual(plain, encrypted);
        Assert.Equal(plain, encryptor.Decrypt(encrypted));
        Assert.Contains(",", encrypted);
        Assert.Contains(" ", encrypted);
        Assert.Contains("!", encrypted);
    }

    [Fact]
    public void TextEncryptor_SamePasswordDeterministic_DifferentPasswordDiffers()
    {
        var first = new TextEncryptor("password");
        var second = new TextEncryptor("password");
        var other = new TextEncryptor("other");

        Assert.Equal(first.Encrypt("abcdef"), second.Encrypt("abcdef"));
        Assert.NotEqual(first.Encrypt("abcdef"), other.Encrypt("abcdef"));
    }

    [Fact]
    public void TextEncryptor_EmptyPassword_IsSupported()
    {
        var encryptor = new TextEncryptor("");

        Assert.Equal("abc", encryptor.Decrypt(encryptor.Encrypt("abc")));
    }

    [Fact]
    public void MersenneTwister_SameSeedReproducesSequence()
    {
        var first = new MersenneTwister(123);
        var second = new MersenneTwister(123);

        Assert.Equal(Enumerable.Range(0, 10).Select(_ => first.Next()),
            Enumerable.Range(0, 10).Select(_ => second.Next()));
    }

    [Fact]
    public void UnicodeRange_AllRangesAreValidAndNonOverlapping()
    {
        foreach (var range in UnicodeRange.Ranges)
        {
            Assert.True(range.Start <= range.End);
            Assert.False(string.IsNullOrWhiteSpace(range.Name));
        }

        var ordered = UnicodeRange.Ranges.OrderBy(p => p.Start).ToList();
        for (int i = 1; i < ordered.Count; i++)
        {
            Assert.True(ordered[i].Start > ordered[i - 1].End,
                $"{ordered[i - 1].Name} 与 {ordered[i].Name} 重叠");
        }
    }

    [Fact]
    public async Task TextEncryptionService_EncryptsAndDecryptsMarkedSegments()
    {
        var service = CreateEncryptionService();

        string encrypted = await service.EncryptAsync("hello", TestContext.Current.CancellationToken);
        string decrypted = await service.DecryptAsync(encrypted, TestContext.Current.CancellationToken);

        Assert.StartsWith("##", encrypted);
        Assert.EndsWith("##", encrypted);
        Assert.Equal("hello", decrypted);
    }

    [Fact]
    public async Task TextEncryptionService_PlainTextDecryptsAsIs()
    {
        var service = CreateEncryptionService();

        Assert.Equal("plain text",
            await service.DecryptAsync("plain text", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TextEncryptionService_TooLongInput_Throws()
    {
        var service = CreateEncryptionService();

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.EncryptAsync(new string('a', TextEncryptionService.MaxLength + 1),
                TestContext.Current.CancellationToken));

        Assert.Contains("超过限制", exception.Message);
    }

    [Fact]
    public async Task EncodingConverter_AsciiIsNotSelected()
    {
        string dir = workspace.Dir("source");
        workspace.WriteText("source/ascii.txt", "plain ascii", Encoding.ASCII);
        var service = new EncodingConverterService(appConfig)
        {
            Config = new EncodingConverterConfig { Dir = dir, TargetEncoding = "UTF-8" },
        };

        await service.InitializeAsync(CancellationToken.None);

        Assert.False(service.Files[0].IsChecked);
    }

    [Fact]
    public async Task EncodingConverter_ConvertsToGb18030InAnotherDirectory()
    {
        string source = workspace.Dir("source");
        string target = workspace.Dir("target");
        workspace.WriteText("source/chinese.txt", "中文内容", new UTF8Encoding(false));
        var service = new EncodingConverterService(appConfig)
        {
            Config = new EncodingConverterConfig
            {
                Dir = source,
                TargetEncoding = "GB18030",
                SaveToAnotherDir = true,
                AnotherDir = target,
            },
        };

        await service.InitializeAsync(CancellationToken.None);
        Assert.True(service.Files[0].IsChecked);
        await service.ExecuteAsync(CancellationToken.None);

        string converted = File.ReadAllText(Path.Combine(target, "chinese.txt"), Encoding.GetEncoding("GB18030"));
        Assert.Equal("中文内容", converted);
    }

    [Fact]
    public async Task TextSource_InlineTextSupportsPerParagraphAndCombined()
    {
        var source = new TextSource { FromFile = false, Text = "第一行\n第二行" };

        var paragraphs = new List<string>();
        await foreach (var part in source.GetPlainTextAsync(TextSourceReadUnit.PerParagraph,
                           TestContext.Current.CancellationToken))
        {
            paragraphs.Add(part.Text);
        }

        Assert.Equal(["第一行", "第二行"], paragraphs);
        Assert.Equal("第一行\n第二行",
            await source.GetCombinedPlainTextAsync(TestContext.Current.CancellationToken));
    }

    private TextEncryptionService CreateEncryptionService()
    {
        return new TextEncryptionService(new TextEncryptionConfig
        {
            Password = new SecurePassword("password"),
        }, appConfig);
    }
}
