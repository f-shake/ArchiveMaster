using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Tests.PhotoTools;

public class PhotoGeoAndSlimmingTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly AppConfig appConfig;

    public PhotoGeoAndSlimmingTests()
    {
        appConfig = TestAppConfig.Create(workspace);
    }

    public void Dispose()
    {
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public void ParseGpx_ReadsBothNamespacedAndPlainTracks()
    {
        var points = new[]
        {
            (31.2, 121.4, new DateTime(2020, 1, 1, 10, 0, 0)),
            (31.3, 121.5, new DateTime(2020, 1, 1, 10, 5, 0)),
        };
        string namespaced = DataFactory.WriteGpx(workspace.PathOf("a.gpx"), points);
        string plain = DataFactory.WriteGpx(workspace.PathOf("b.gpx"), points, false);

        var first = PhotoGeoTaggingService.ParseGpx(namespaced);
        var second = PhotoGeoTaggingService.ParseGpx(plain);

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Equal(first[0].time, first[1].time.AddMinutes(-5));
    }

    [Fact]
    public async Task GeoTagging_MatchesPhotoTimeAndWritesGps()
    {
        string dir = workspace.Dir("photos");
        var time = new DateTime(2020, 1, 1, 10, 1, 0);
        string photo = ImageFactory.CreateJpeg(Path.Combine(dir, "photo.jpg"), exifTime: time);
        string gpx = DataFactory.WriteGpx(workspace.PathOf("track.gpx"),
        [
            (31.2, 121.4, time.AddMinutes(-1)),
            (31.3, 121.5, time.AddMinutes(1)),
        ]);
        var service = new PhotoGeoTaggingService(appConfig)
        {
            Config = new PhotoGeoTaggingConfig { Dir = dir, GpxFile = gpx, MaxTolerance = TimeSpan.FromMinutes(5) },
        };

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        var gps = ExifHelper.FindGps(photo);
        Assert.NotNull(gps);
        Assert.True(service.Files[0].IsChecked);
    }

    [Fact]
    public async Task RepairModifiedTime_UsesExifTimeWhenDifferenceExceedsTolerance()
    {
        string dir = workspace.Dir("photos");
        var exifTime = new DateTime(2020, 1, 2, 3, 4, 5);
        string photo = ImageFactory.CreateJpeg(Path.Combine(dir, "photo.jpg"), exifTime: exifTime);
        File.SetLastWriteTime(photo, exifTime.AddDays(1));
        var service = new RepairModifiedTimeService(appConfig)
        {
            Config = new RepairModifiedTimeConfig
            {
                Dir = dir,
                MaxDurationTolerance = TimeSpan.FromSeconds(1),
                ThreadCount = 1,
            },
        };

        await service.InitializeAsync(CancellationToken.None);
        Assert.Single(service.Files);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal(exifTime, File.GetLastWriteTime(photo), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GeoSorter_AssignsRegionAndMovesPhoto()
    {
        string dir = workspace.Dir("photos");
        string photo = ImageFactory.CreateJpeg(Path.Combine(dir, "photo.jpg"), latitude: 31.2, longitude: 121.4);
        string geojson = DataFactory.WriteGeoJsonSquare(workspace.PathOf("areas.geojson"), "name", "上海", 31.2, 121.4);
        var service = new PhotoGeoSorterService(appConfig)
        {
            Config = new PhotoGeoSorterConfig
            {
                Dir = dir,
                VectorFile = geojson,
                FieldName = "name",
            },
        };

        await service.InitializeAsync(CancellationToken.None);
        await service.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(dir, "上海", "photo.jpg")));
    }

    [Fact]
    public async Task Slimming_ClassifiesCompressCopySkipAndDelete()
    {
        string source = workspace.Dir("source");
        string target = workspace.Dir("target");
        workspace.WriteText("source/photo.jpg", "not a real image");
        workspace.WriteText("source/note.pdf", "pdf");
        workspace.WriteText("target/old.jpg", "old");
        workspace.WriteText("target/photo.jpg", "existing");
        File.SetLastWriteTime(Path.Combine(target, "photo.jpg"), File.GetLastWriteTime(Path.Combine(source, "photo.jpg")));
        var service = new PhotoSlimmingService(appConfig)
        {
            Config = new PhotoSlimmingConfig
            {
                SourceDir = source,
                DistDir = target,
                SkipIfExist = true,
                Thread = 1,
            },
        };

        await service.InitializeAsync(CancellationToken.None);

        Assert.Contains(service.Files, p => p.Name == "photo.jpg" && p.SlimmingTaskType == SlimmingTaskType.Skip);
        Assert.Contains(service.Files, p => p.Name == "note.pdf" && p.SlimmingTaskType == SlimmingTaskType.Copy);
        Assert.Contains(service.Files, p => p.Name == "old.jpg" && p.SlimmingTaskType == SlimmingTaskType.Delete);
    }

    [Fact]
    public async Task Slimming_DeepestLevelFlattensNestedPath()
    {
        string source = workspace.Dir("source");
        string target = workspace.Dir("target");
        workspace.Dir("source/a/b");
        workspace.WriteText("source/a/b/photo.jpg", "not a real image");
        var service = new PhotoSlimmingService(appConfig)
        {
            Config = new PhotoSlimmingConfig
            {
                SourceDir = source,
                DistDir = target,
                DeepestLevel = 1,
                Thread = 1,
            },
        };

        await service.InitializeAsync(CancellationToken.None);

        var file = Assert.Single(service.Files);
        Assert.EndsWith(Path.Combine("a", "b-photo.jpg"), file.DistFile.Path);
    }
}
