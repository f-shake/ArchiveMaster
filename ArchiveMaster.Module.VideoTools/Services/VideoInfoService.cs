using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class VideoInfoService(AppConfig appConfig) : TwoStepServiceBase<VideoInfoConfig>(appConfig)
    {
        public List<VideoInfoFileInfo> Files { get; private set; } = new List<VideoInfoFileInfo>();

        public static T ParseNumberFromJson<T>(JsonValue jValue) where T : INumber<T>
        {
            if (jValue.GetValueKind() == JsonValueKind.Number)
            {
                return jValue.GetValue<T>();
            }

            if (jValue.GetValueKind() == JsonValueKind.String)
            {
                var str = jValue.GetValue<string>();
                if (T.TryParse(str, null, out T result))
                {
                    return result;
                }
            }

            throw new JsonException($"无法将{jValue.GetValueKind()}类型转为{typeof(T).Name}");
        }
        
        private static T ParseVideoInfoPartJson<T>(JsonObject json)
        {
            var properties = typeof(T).GetProperties();

            var result = Activator.CreateInstance<T>();
            foreach (var p in properties)
            {
                if (p.GetCustomAttribute(typeof(VideoInfoFFprobeSourceAttribute), false) is not
                    VideoInfoFFprobeSourceAttribute attr)
                {
                    continue;
                }

                var key = attr.Key;
                if (!json.ContainsKey(key))
                {
                    continue;
                }

                var jValue = json[key].AsValue();
                object value = null;
                if (attr.SourceType == VideoInfoFFprobeSourceType.Fraction)
                {
                    if (!jValue.TryGetValue(out string fraction))
                    {
                        throw new JsonException("分数值不是字符串");
                    }

                    var parts = fraction.Split('/');
                    if (parts.Length != 2)
                    {
                        throw new Exception("分数值不是“分子/分母”格式");
                    }

                    if (!double.TryParse(parts[0], out double p1) || !double.TryParse(parts[1], out double p2))
                    {
                        throw new Exception("分数值不是数字的“分子/分母”格式");
                    }

                    value = p1 / p2;
                }
                else
                {
                    if (p.PropertyType == typeof(double))
                    {
                        value = ParseNumberFromJson<double>(jValue);
                    }
                    else if (p.PropertyType == typeof(int))
                    {
                        value = ParseNumberFromJson<int>(jValue);
                    }
                    else if (p.PropertyType == typeof(long))
                    {
                        value = ParseNumberFromJson<long>(jValue);
                    }
                    else if (p.PropertyType == typeof(string))
                    {
                        value = jValue.GetValue<string>();
                    }
                    else
                    {
                        continue;
                    }
                }

                p.SetValue(result, value);
            }

            return result;
        }

        private static VideoInfo GetVideoInfo(string path, string ffprobePath)
        {
            var result = ExecutableDependencyHelper.GetProgramOutput(ffprobePath,
                $"-v quiet -print_format json -show_format -show_streams \"{path}\"");
            try
            {
                var json = (JsonObject)JsonNode.Parse(result);
                var jFormat = (JsonObject)json["format"];
                var jStreams = (JsonArray)json["streams"];
                var format = ParseVideoInfoPartJson<VideoFormat>(jFormat);
                var streams = jStreams.Cast<JsonObject>().Select(ParseVideoInfoPartJson<VideoStream>).ToList();
                return new VideoInfo(format, streams, result);
            }
            catch (Exception ex)
            {
                throw new Exception($"FFprobe返回的JSON结构异常：{ex.Message}", ex);
            }
        }

        public override async Task ExecuteAsync(CancellationToken ct = default)
        {
            var ffprobePath = ExecutableDependencyHelper.GetFFprobeExePath();
            var files = Files.CheckedOnly().ToList();
            await Task.Run(() =>
            {
                TryForFiles(files,
                    (f, s) => { f.VideoInfo = GetVideoInfo(f.Path, ffprobePath); }, ct,
                    FilesLoopOptions.Builder().AutoApplyFileNumberProgress().AutoApplyStatus().Build());
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files;
        }

        public override async Task InitializeAsync(CancellationToken ct = default)
        {
            List<VideoInfoFileInfo> files = null;
            await Task.Run(() =>
            {
                files = new DirectoryInfo(Config.Dir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .ApplyFilter(ct, Config.Filter)
                    .Select(p => new VideoInfoFileInfo(p, Config.Dir))
                    .OrderBy(p => p.Time)
                    .ToList();
            }, ct);
            Files = files;
        }
    }
}