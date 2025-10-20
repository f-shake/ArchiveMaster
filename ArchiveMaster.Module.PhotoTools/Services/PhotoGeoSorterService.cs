using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO.Converters;
using NetTopologySuite.IO.Esri;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class PhotoGeoSorterService(AppConfig appConfig)
        : TwoStepServiceBase<PhotoGeoSorterConfig>(appConfig)
    {
        public List<GpsFileInfo> Files { get; private set; }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files.Cast<SimpleFileInfo>();
        }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            return TryForFilesAsync(Files
                .CheckedOnly()
                .Where(p => p.IsMatched && !string.IsNullOrWhiteSpace(p.Region))
                .ToList(), (file, s) =>
            {
                NotifyMessage($"正在移动{s.GetFileNumberMessage()}：{file.Name}");
                var destDir = Path.Combine(Config.Dir, file.Region);
                Directory.CreateDirectory(destDir);
                var destPath = Path.Combine(destDir, file.Name);
                File.Move(file.Path, destPath);
            }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Build());
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            NotifyMessage($"正在解析矢量文件");
            var tree = Path.GetExtension(Config.VectorFile).ToLower() switch
            {
                ".shp" => ReadShapefile(),
                ".geojson" or ".json" => ReadGeoJson(),
                _ => throw new Exception($"矢量地理文件应当为Shapefile(*.shp)或GeoJSON(*.geojson)")
            };

            await Task.Run(() =>
            {
                NotifyMessage($"正在枚举文件");
                var files = new DirectoryInfo(Config.Dir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .ApplyFilter(ct, Config.Filter)
                    .Select(p => new GpsFileInfo(p, Config.Dir))
                    .ToList();

                TryForFiles(files, (f, s) =>
                {
                    NotifyMessage($"正在解析文件{s.GetFileNumberMessage()}：{f.Name}");
                    var gps = ExifHelper.FindGps(f.Path);
                    if (gps != null)
                    {
                        f.Longitude = gps.Value.lon;
                        f.Latitude = gps.Value.lat;
                        f.AlreadyHasGps = true;

                        Point point = new Point(f.Longitude.Value, f.Latitude.Value);
                        var candidates = tree.Query(point.EnvelopeInternal);
                        foreach (var candidate in candidates)
                        {
                            if (candidate.Geometry.Contains(point))
                            {
                                f.Region = FileNameHelper.GetValidFileName(candidate.Attributes[Config.FieldName]
                                    .ToString());
                                f.IsMatched = true;
                                f.IsChecked = true;
                                break;
                            }
                        }
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyFileNumberProgress().Build());

                Files = files;
            });
        }

        private STRtree<IFeature> CreateSpatialIndexFromFeatures(IEnumerable<IFeature> features)
        {
            var featureList = features.ToList();
            if (featureList.Count == 0)
            {
                throw new Exception("矢量文件中没有包含任何要素");
            }

            // 检查第一个要素的几何类型
            var firstGeometryType = featureList[0].Geometry.GeometryType;
            if (!firstGeometryType.Contains("Polygon")) // 包括 Polygon 和 MultiPolygon
            {
                throw new Exception($"矢量文件的几何类型应当为面(Polygon)或多面(MultiPolygon)，实际为{firstGeometryType}");
            }

            if (!featureList[0].Attributes.Exists(Config.FieldName))
            {
                throw new Exception($"没有名为{Config.FieldName}的字段");
            }

            var tree = new STRtree<IFeature>();
            foreach (var feature in featureList)
            {
                tree.Insert(feature.Geometry.EnvelopeInternal, feature);
            }

            tree.Build();
            return tree;
        }

        private STRtree<IFeature> ReadGeoJson()
        {
            var geoJson = File.ReadAllText(Config.VectorFile);
            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                Converters = { new GeoJsonConverterFactory() }
            };
            var features = JsonSerializer.Deserialize<FeatureCollection>(geoJson, options);
            return CreateSpatialIndexFromFeatures(features);
        }

        private STRtree<IFeature> ReadShapefile()
        {
            using var shapefile = Shapefile.OpenRead(Config.VectorFile);
            return CreateSpatialIndexFromFeatures(shapefile);
        }
    }
}