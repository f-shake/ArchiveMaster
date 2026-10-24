using System.Security.Cryptography;
using System.Text;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Tests.Core;

public class HelperTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Theory]
    [InlineData("<NameExt>", "photo.jpg")]
    [InlineData("<Name>", "photo")]
    [InlineData("<Ext>", "jpg")]
    public void FilePlaceholderReplacer_SimplePlaceholders(string template, string expected)
    {
        var file = new SimpleFileInfo
        {
            Name = "photo.jpg",
            Path = workspace.PathOf("photo.jpg"),
        };

        Assert.Equal(expected, new FilePlaceholderReplacer(template).GetTargetName(file));
    }

    [Fact]
    public void FilePlaceholderReplacer_SubNameAndNoPattern()
    {
        var file = new SimpleFileInfo { Name = "photo.jpg", Path = workspace.PathOf("photo.jpg") };

        Assert.Equal("hot", new FilePlaceholderReplacer("<Name-Left-1-3>").GetTargetName(file));
        Assert.Equal("pho", new FilePlaceholderReplacer("<Name-Right-2-3>").GetTargetName(file));
        Assert.Equal("literal", new FilePlaceholderReplacer("literal").GetTargetName(file));
        Assert.Equal("<Unknown>", new FilePlaceholderReplacer("<Unknown>").GetTargetName(file));
        Assert.Equal("xhotx", new FilePlaceholderReplacer("x<Name-Left-1-3>x").GetTargetName(file,
            value => value.ToUpperInvariant() switch { "HOT" => "hot", _ => value }));
    }

    [Fact]
    public void FilePlaceholderReplacer_WritesDateFromFile()
    {
        string path = workspace.WriteText("dated.txt", "x");
        var time = new DateTime(2020, 1, 2, 3, 4, 5);
        File.SetLastWriteTime(path, time);
        var file = new SimpleFileInfo { Name = "dated.txt", Path = path };

        Assert.Equal("20200102", new FilePlaceholderReplacer("<LastWriteTime-yyyyMMdd>").GetTargetName(file));
    }

    [Fact]
    public void TableTextConverter_CsvQuotesCommaField()
    {
        var converter = new TableTextConverter(typeof(Row), null);
        var rows = new[] { new Row { Name = "a,b", Age = 3 } };

        string result = converter.ConvertToTableText(rows, TableTextType.Csv);

        Assert.Contains("\"a,b\"", result);
    }

    [Fact]
    public void TableTextConverter_CsvQuotesFieldContainingQuote()
    {
        var converter = new TableTextConverter(typeof(Row), null);
        var rows = new[] { new Row { Name = "a", Age = 3, Note = "x\"y" } };

        string result = converter.ConvertToTableText(rows, TableTextType.Csv);

        // RFC 4180：字段内出现双引号时，整个字段加引号，内部的引号写成两个。
        Assert.Contains("\"x\"\"y\"", result);
    }

    [Fact]
    public void TableTextConverter_CsvDoesNotEndWithComma()
    {
        var converter = new TableTextConverter(typeof(Row), null);
        var rows = new[]
        {
            new Row { Name = "a", Age = 3, Note = "n" },
            new Row { Name = "b", Age = 4, Note = "m" },
        };

        string result = converter.ConvertToTableText(rows, TableTextType.Csv);
        string[] lines = result.TrimEnd('\r', '\n').Split(Environment.NewLine);

        Assert.All(lines.Skip(1), line => Assert.False(line.EndsWith(',')));
    }

    [Fact]
    public void TableTextConverter_PropertySelection_UsesFieldName()
    {
        var converter = new TableTextConverter(typeof(Row), [new PropertyFieldInfo("Name", "姓名")]);
        var rows = new[] { new Row { Name = "张三", Age = 20 } };

        string result = converter.ConvertToTableText(rows, TableTextType.Csv);

        Assert.StartsWith("姓名", result);
        Assert.Contains("张三", result);
        Assert.DoesNotContain("Age", result);
    }

    [Fact]
    public void TableTextConverter_JsonAndXml_ContainData()
    {
        var rows = new[] { new Row { Name = "A", Age = 1 } };
        var converter = new TableTextConverter(typeof(Row), null);

        Assert.Contains("\"Name\": \"A\"", converter.ConvertToTableText(rows, TableTextType.Json));
        Assert.Contains("<Name>A</Name>", converter.ConvertToTableText(rows, TableTextType.Xml));
    }

    [Fact]
    public void PagedList_CalculatesPageCount()
    {
        Assert.Equal(0, new PagedList<int>([], 0, 10, 0).PageCount);
        Assert.Equal(1, new PagedList<int>([1], 0, 10, 1).PageCount);
        Assert.Equal(4, new PagedList<int>(Enumerable.Range(0, 35).ToList(), 0, 10, 35).PageCount);
    }

    [Theory]
    [InlineData(0, 16 * 1024)]
    [InlineData(1024 * 1024 - 1, 16 * 1024)]
    [InlineData(1024 * 1024, 1024 * 1024)]
    [InlineData(32 * 1024 * 1024 - 1, 1024 * 1024)]
    [InlineData(32 * 1024 * 1024, 4 * 1024 * 1024)]
    public void FileHelper_GetOptimalBufferSize(long length, int expected)
    {
        Assert.Equal(expected, FileHelper.GetOptimalBufferSize(length));
    }

    [Fact]
    public void FileHelper_GetNoDuplicateName_AppendsIndex()
    {
        string file = workspace.WriteText("a.txt", "x");
        string dir = workspace.Dir("folder");

        Assert.Equal(workspace.PathOf("b.txt"), FileHelper.GetNoDuplicateFile(workspace.PathOf("b.txt")));
        Assert.Equal(workspace.PathOf("a (2).txt"), FileHelper.GetNoDuplicateFile(file));
        Assert.Equal(workspace.PathOf("folder (2)"), FileHelper.GetNoDuplicateDirectory(dir));
    }

    [Theory]
    [InlineData(FilenameCasePolicy.Ignore, true)]
    [InlineData(FilenameCasePolicy.Sensitive, false)]
    public void FileHelper_GetStringComparer_RespectsPolicy(FilenameCasePolicy policy, bool ignoreCase)
    {
        GlobalConfigs.Instance.FileNameCase = policy;

        bool equal = FileHelper.GetStringComparer().Equals("A", "a");

        Assert.Equal(ignoreCase, equal);
    }

    [Fact]
    public void SecurePasswordStore_RoundTripsAndRejectsWrongMasterPassword()
    {
        string encrypted = SecurePasswordStoreService.SavePassword("secret", "master");

        Assert.Equal("secret", SecurePasswordStoreService.LoadPassword(encrypted, "master"));
        Assert.ThrowsAny<CryptographicException>(() =>
            SecurePasswordStoreService.LoadPassword(encrypted, "wrong"));
    }

    [Fact]
    public void AesHelper_DerivesStableKeyPerPassword()
    {
        using Aes first = AesHelper.GetDefaultAes("password");
        using Aes second = AesHelper.GetDefaultAes("password");
        using Aes other = AesHelper.GetDefaultAes("other");

        Assert.Equal(first.Key, second.Key);
        Assert.NotEqual(first.Key, other.Key);
    }

    [Fact]
    public void DateTimeExtension_TruncatesToSecond()
    {
        var value = new DateTime(2020, 1, 2, 3, 4, 5, 987);
        Assert.Equal(new DateTime(2020, 1, 2, 3, 4, 5), value.TruncateToSecond());
        Assert.Equal(DateTime.MinValue, DateTime.MinValue.TruncateToSecond());
        Assert.Equal(DateTime.MaxValue, DateTime.MaxValue.TruncateToSecond());
    }

    [Fact]
    public void FileEnumerateExtension_ApplyFilter_SkipsRecycleBinAndHonorsCancellation()
    {
        var items = new[] { workspace.PathOf("a.txt"), workspace.PathOf("$RECYCLE.BIN", "b.txt") };
        Assert.Single(items.ApplyFilter(CancellationToken.None).ToList());

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            items.ApplyFilter(cts.Token).ToList());
    }

    [Fact]
    public void FileHelper_IsGpsTempFile_MatchesMarker()
    {
        Assert.True(FileHelper.IsGpsTempFile("IMG_001.gps.tmp.jpg"));
        Assert.True(FileHelper.IsGpsTempFile("IMG_001.GPS.TMP.JPG"));
        Assert.False(FileHelper.IsGpsTempFile("IMG_001.jpg"));
    }

    [Fact]
    public void FileHelper_IsPathInside_RejectsTraversalAndPrefixConfusion()
    {
        Assert.True(FileHelper.IsPathInside(workspace.Root, workspace.PathOf("a", "b.txt")));
        Assert.False(FileHelper.IsPathInside(workspace.Root, workspace.PathOf("..", "evil.txt")));
        Assert.False(FileHelper.IsPathInside(workspace.PathOf("a"), workspace.PathOf("a.txt")));
    }

    [Fact]
    public void FileHelper_DeleteByConfig_DirectlyDeletes()
    {
        string path = workspace.WriteText("delete-me.txt", "x");
        FileHelper.DeleteByConfig(path, "测试");

        Assert.False(File.Exists(path));
    }

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string Note { get; set; } = "";
    }
}
