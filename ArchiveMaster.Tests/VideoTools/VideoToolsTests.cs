using System.Text.Json;
using System.Text.Json.Nodes;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Tests.VideoTools;

public class VideoToolsTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public VideoToolsTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public void ParseNumberFromJson_AcceptsNumbersAndNumericStrings()
    {
        Assert.Equal(3, VideoInfoService.ParseNumberFromJson<int>(JsonValue.Create(3)));
        Assert.Equal(42, VideoInfoService.ParseNumberFromJson<int>(JsonValue.Create("42")));
        Assert.Equal(1.5, VideoInfoService.ParseNumberFromJson<double>(JsonValue.Create(1.5)));
    }

    [Fact]
    public void ParseNumberFromJson_InvalidString_Throws()
    {
        Assert.Throws<JsonException>(() =>
            VideoInfoService.ParseNumberFromJson<int>(JsonValue.Create("not-a-number")));
    }

    [Theory]
    [InlineData("video", StreamCodecType.Video)]
    [InlineData("audio", StreamCodecType.Audio)]
    [InlineData("subtitle", StreamCodecType.Subtitle)]
    [InlineData("data", StreamCodecType.Data)]
    [InlineData("attachment", StreamCodecType.Attachment)]
    [InlineData("unknown", StreamCodecType.Unknown)]
    public void VideoStream_CodecTypeMapping(string name, StreamCodecType expected)
    {
        var stream = new VideoStream { CodecTypeName = name };

        Assert.Equal(expected, stream.CodecType);
    }

    [Fact]
    public void VideoStream_SizeAndFrameRate()
    {
        var stream = new VideoStream { Width = 1920, Height = 1080, TargetFrameRate = 29.97 };

        Assert.Equal("1920 × 1080", stream.Size);
        Assert.Equal(29.97, stream.TargetFrameRate);
        Assert.Equal("", new VideoStream().Size);
    }

    [Fact]
    public async Task TimeAssService_ExportsDialogueLines()
    {
        string output = workspace.PathOf("subtitle.ass");
        var service = new TimeAssService(appConfig)
        {
            Config = new TimeAssConfig
            {
                CombineIntoSingleFile = true,
                ExportFile = output,
                Format = new TimeAssFormat { Interval = 1000 },
            },
        };
        service.Files.Add(new TimeAssVideoFileInfo
        {
            Name = "video.mp4",
            Path = workspace.PathOf("video.mp4"),
            VideoLength = TimeSpan.FromSeconds(3),
            StartTime = new DateTime(2020, 1, 1, 10, 0, 0),
            Ratio = 1,
        });

        await service.ExecuteAsync(CancellationToken.None);

        string text = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
        Assert.Contains("Dialogue: 3,00:00:00:00,00:00:01:00", text);
        Assert.Contains("00:00:02:00,00:00:03:00", text);
        Assert.Contains("10:00:00", text);
    }

    [Fact]
    public async Task TimeAssService_MissingVideoLength_Throws()
    {
        var service = new TimeAssService(appConfig)
        {
            Config = new TimeAssConfig { CombineIntoSingleFile = true, ExportFile = workspace.PathOf("x.ass") },
        };
        service.Files.Add(new TimeAssVideoFileInfo
        {
            Name = "video.mp4",
            Path = workspace.PathOf("video.mp4"),
            StartTime = DateTime.Now,
        });

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.ExecuteAsync(CancellationToken.None));

        Assert.Contains("视频长度", exception.Message);
    }

    [Fact]
    public async Task ExportCsv_WritesOneRowPerSelectedStream()
    {
        string output = workspace.PathOf("videos.csv");
        var format = new VideoFormat
        {
            StreamCount = 2,
            FormatShortName = "mp4",
            FormatLongName = "MPEG-4",
            Duration = 60,
            TotalBitRate = 1000,
        };
        var service = new VideoInfoService(appConfig);
        var file = new VideoInfoFileInfo(new FileInfo(workspace.PathOf("v.mp4")), workspace.Root)
        {
            VideoInfo = new VideoInfo(format,
            [
                new VideoStream { CodecTypeName = "video", Width = 1920, Height = 1080 },
                new VideoStream { CodecTypeName = "audio", Channels = 2 },
            ], "{}"),
        };
        service.Files.Add(file);

        await service.ExportCsvAsync(output, false);
        string single = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);

        Assert.Contains("文件名", single);
        Assert.Contains("v.mp4", single);

        await service.ExportCsvAsync(output, true);
        string all = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(3, all.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }
}
