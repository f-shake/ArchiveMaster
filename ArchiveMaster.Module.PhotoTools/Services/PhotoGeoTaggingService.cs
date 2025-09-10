using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class PhotoGeoTaggingService(AppConfig appConfig) : TwoStepServiceBase<PhotoGeoTaggingConfig>(appConfig)
    {
        public List<GpsFileInfo> Files { get; private set; }

        /// <summary>
        /// 独立GPX解析方法
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static List<(double lat, double lon, DateTime time)> ParseGpx(string filePath)
        {
            var doc = XDocument.Load(filePath);

            // 尝试带命名空间查找 <trk>
            XNamespace ns = "http://www.topografix.com/GPX/1/0";
            var trk = doc.Root.Element(ns + "trk") ?? doc.Root.Element("trk"); // 如果带命名空间找不到，再尝试不带命名空间

            if (trk == null) return new List<(double, double, DateTime)>();

            // 查找 <trkseg>（带/不带命名空间）
            var trksegElements = trk.Elements(ns + "trkseg").Any()
                ? trk.Elements(ns + "trkseg")
                : trk.Elements("trkseg");

            return trksegElements
                .SelectMany(seg =>
                {
                    // 查找 <trkpt>（带/不带命名空间）
                    var trkptElements = seg.Elements(ns + "trkpt").Any()
                        ? seg.Elements(ns + "trkpt")
                        : seg.Elements("trkpt");

                    return trkptElements.Select(pt =>
                    {
                        // 查找 <time>（带/不带命名空间）
                        var timeElement = pt.Element(ns + "time") ?? pt.Element("time");

                        return (
                            lat: (double)pt.Attribute("lat"),
                            lon: (double)pt.Attribute("lon"),
                            time: (DateTime)timeElement
                        );
                    });
                })
                .OrderBy(p => p.time) // 按时间排序
                .ToList();
        }

        public override Task ExecuteAsync(CancellationToken ct = default)
        {
            var files = Files.Where(p => p.IsMatched && p.IsChecked).ToList();
            return TryForFilesAsync(files,
                (file, state) =>
                {
                    if (!file.Latitude.HasValue || !file.Longitude.HasValue || !file.ExifTime.HasValue)
                    {
                        throw new Exception("数据存在问题，经纬度或时间为空");
                    }

                    ExifHelper.WriteGpsToImage(file.Path, file.Latitude.Value, file.Longitude.Value);
                    File.SetLastWriteTime(file.Path, file.ExifTime.Value);
                },
                ct, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().AutoApplyStatus().Build());
        }


        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files.Cast<SimpleFileInfo>();
        }

        public override async Task InitializeAsync(CancellationToken ct = default)
        {
            NotifyProgressIndeterminate();

            List<GpsFileInfo> files = null;
            List<GpsFileInfo> results = new List<GpsFileInfo>();

            // 读取并解析GPX文件
            NotifyMessage("正在解析GPX轨迹");
            List<(double lat, double lon, DateTime time)> gpxPoints = new List<(double, double, DateTime)>();
            if (string.IsNullOrWhiteSpace(Config.GpxFile))
            {
                throw new Exception($"GPX文件不存在");
            }

            await Task.Run(() =>
            {
                var gpxFiles = FileNameHelper.GetFileNames(Config.GpxFile);
                int index = 0;
                foreach (var gpxFile in gpxFiles)
                {
                    NotifyProgress(1.0 * (index++) / gpxFiles.Length);
                    NotifyMessage($"正在解析GPX轨迹（{index}/{gpxFiles.Length}）：{Path.GetFileName(gpxFile)}");
                    if (!File.Exists(gpxFile))
                    {
                        throw new Exception($"GPX文件{gpxFile}不存在");
                    }

                    try
                    {
                        var gpx = ParseGpx(gpxFile);
                        if (gpx.Count == 0)
                        {
                            continue;
                            //throw new Exception("GPX文件未包含有效轨迹点");
                        }

                        gpxPoints.AddRange(gpx);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"GPX文件 {gpxFile}解析失败：（{ex.Message}）", ex);
                    }
                }

                // 按时间排序GPX点
                gpxPoints = [.. gpxPoints.OrderBy(p => p.time)];
            }, ct);

            // 5. 处理照片文件
            NotifyMessage("正在查找文件");
            NotifyProgressIndeterminate();
            await Task.Run(() =>
            {
                files = new DirectoryInfo(Config.Dir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .ApplyFilter(ct, Config.Filter)
                    .Select(f => new GpsFileInfo(f, Config.Dir))
                    .ToList();
            }, ct);

            NotifyMessage($"正在处理照片");

            await TryForFilesAsync(files, (file, state) =>
            {
                NotifyMessage($"正在处理照片 {state.GetFileNumberMessage()}");

                results.Add(file);

                // 5.1 获取照片Exif时间
                DateTime? exifTime = ExifHelper.FindExifTime(file.Path);
                if (!exifTime.HasValue)
                {
                    file.IsMatched = false;
                    return;
                }

                file.ExifTime = exifTime.Value;
                file.AlreadyHasGps = ExifHelper.FindGps(file.Path) != null;

                DateTime offsetTime = file.ExifTime.Value +
                                      (Config.InverseTimeOffset ? Config.TimeOffset : -Config.TimeOffset);

                // 5.2 匹配最近的GPX点
                var (matched, lat, lon, gpsTime) = FindClosestGpxPoint(gpxPoints, offsetTime);

                // 5.3 检查时间容差
                if (matched && Math.Abs((gpsTime - offsetTime).TotalSeconds) <=
                    Config.MaxTolerance.TotalSeconds)
                {
                    file.Latitude = lat;
                    file.Longitude = lon;
                    file.GpsTime = gpsTime;
                    file.IsMatched = !file.AlreadyHasGps;
                }
                else
                {
                    file.IsMatched = false;
                }
            }, ct, FilesLoopOptions.Builder().AutoApplyFileNumberProgress().Build());

            foreach (var file in files)
            {
                file.IsChecked = file.CanCheck = file.IsMatched;
            }

            Files = results;
        }
        /// <summary>
        /// 双指针查找最近点
        /// </summary>
        /// <param name="points"></param>
        /// <param name="targetTime"></param>
        /// <returns></returns>
        private (bool matched, double lat, double lon, DateTime time) FindClosestGpxPoint(
            List<(double lat, double lon, DateTime time)> points, DateTime targetTime)
        {
            if (points.Count == 0) return (false, 0, 0, default);

            int left = 0, right = points.Count - 1;
            int closestIndex = 0;
            double minDiff = double.MaxValue;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                var diff = Math.Abs((points[mid].time - targetTime).TotalSeconds);

                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestIndex = mid;
                }

                if (points[mid].time < targetTime)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            var closest = points[closestIndex];
            return (true, closest.lat, closest.lon, closest.time);
        }
    }
}