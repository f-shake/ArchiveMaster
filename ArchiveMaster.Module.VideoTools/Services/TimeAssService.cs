using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class TimeAssService(AppConfig appConfig) : TwoStepServiceBase<TimeAssConfig>(appConfig)
    {
        public const string ASS_AUTHOR = "Author=f-shake";
        public const string ASS_FILE = "Files";
        public const string ASS_FORMAT = "Format";
        public const string ASS_SOFTWARE = "Software=ArchiveMaster";

        public List<TimeAssVideoFileInfo> Files { get; private set; } = new List<TimeAssVideoFileInfo>();

        public static TimeSpan? GetVideoLength(string path)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                FileName = "ffprobe",
                Arguments =
                    $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{path}\""
            };

            using Process p = new Process();
            p.StartInfo = startInfo;
            p.Start();
            p.WaitForExit();
            var output = p.StandardOutput.ReadToEnd().Trim();
            if (double.TryParse(output, out double length))
            {
                return TimeSpan.FromSeconds(length);
            }
            else
            {
                return null;
            }
        }

        private void Export( TimeAssVideoFileInfo file, string exportPath)
        {
            Export([file], exportPath);
        }

        private void Export(IList<TimeAssVideoFileInfo> files, string path)
        {
            Debug.Assert(!string.IsNullOrEmpty(path));
            StringBuilder outputs = GetAssHead(Config.Format, files);

            string timespanFormat = "hh\\:mm\\:ss\\:ff";
            var interval = TimeSpan.FromMilliseconds(Config.Format.Interval);
            TimeSpan totalTime = TimeSpan.Zero;
            foreach (var file in files)
            {
                TimeSpan currentTime = TimeSpan.Zero;
                while (true)
                {
                    var nextTime = currentTime.Add(interval);
                    if (nextTime > file.VideoLength)
                    {
                        nextTime = file.VideoLength.Value;
                    }

                    outputs.Append($"Dialogue: 3,")
                        .Append((totalTime + currentTime).ToString(timespanFormat))
                        .Append(',')
                        .Append((totalTime + nextTime).ToString(timespanFormat))
                        .Append(",Default,,0000,0000,0000,,")
                        .Append((file.StartTime.Value + currentTime * file.Ratio).ToString(Config.Format.TimeFormat))
                        .AppendLine();
                    if (nextTime >= file.VideoLength)
                    {
                        break;
                    }

                    currentTime = nextTime;
                }

                totalTime += file.VideoLength.Value;
                file.Success();
            }

            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            File.WriteAllText(path, outputs.ToString());
        }

        private static string GetAssFileName(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".ass");
        }

        private static StringBuilder GetAssHead(TimeAssFormat format, IList<TimeAssVideoFileInfo> files)
        {
            int size = format.Size;
            int marginV = format.VerticalMargin;
            int marginH = format.HorizontalMargin;
            int al = format.HorizontalAlignment + format.VerticalAlignment * 3 + 1;
            int bw = format.BorderWidth;
            var c = $"&H{(255 - format.TextColor.A):X2}{format.TextColor.B:X2}{format.TextColor.G:X2}{format.TextColor.R:X2}";
            var bc = $"&H{(255 - format.BorderColor.A):X2}{format.BorderColor.B:X2}{format.BorderColor.G:X2}{format.BorderColor.R:X2}";
            int bold = format.Bold ? 1 : 0;
            int italic = format.Italic ? 1 : 0;
            int underline = format.Underline ? 1 : 0;

            StringBuilder outputs = new StringBuilder();
            outputs.AppendLine("[Script Info]")
                .AppendLine("; " + ASS_SOFTWARE)
                .AppendLine("; " + ASS_AUTHOR)
                .AppendLine($"; {ASS_FILE}={JsonSerializer.Serialize(files)}")
                .AppendLine($"; {ASS_FORMAT}={JsonSerializer.Serialize(format)}")
                .AppendLine("ScriptType: v4.00+")
                .AppendLine("Collisions: Normal")
                .AppendLine("PlayResX: 1920")
                .AppendLine("PlayResY: 1080")
                .AppendLine()
                .AppendLine("[V4+ Styles]")
                .AppendLine(
                    "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding")
                .AppendLine(
                    $"Style: Default, Microsoft YaHei, {size}, {c}, {c}, {bc}, &H00000000, {bold}, {italic}, {underline}, 0, 100, 100, 0.00, 0.00, 1, {bw}, 0, {al}, {marginH}, {marginH}, {marginV}, 0")                .AppendLine()
                .AppendLine("[Events]")
                .AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
            return outputs;
        }

        private static (List<TimeAssVideoFileInfo> files, TimeAssFormat format) ImportFromAss(string path)
        {
            string assText = File.ReadAllText(path);
            if (!assText.Contains(ASS_SOFTWARE))
            {
                throw new Exception("非本软件生成的ASS不可导入");
            }

            var match = Regex.Match(assText, $@"{ASS_FILE}=(?<Json>.+){Environment.NewLine}");
            if (match == null || match.Success == false)
            {
                throw new Exception("ASS格式错误");
            }

            string json = match.Groups["Json"].Value;

            var files = JsonSerializer.Deserialize<List<TimeAssVideoFileInfo>>(json);

            match = Regex.Match(assText, $@"{ASS_FORMAT}=(?<Json>.+){Environment.NewLine}");
            if (match == null || match.Success == false)
            {
                throw new Exception("ASS格式错误");
            }

            json = match.Groups["Json"].Value;

            var format = JsonSerializer.Deserialize<TimeAssFormat>(json);
            return (files, format);
        }


        public async override Task ExecuteAsync(CancellationToken ct = default)
        {
            //检查文件是否有开始时间和视频长度
            foreach (var file in Files)
            {
                if (!file.StartTime.HasValue)
                {
                    throw new Exception($"文件{file.Path}没有开始时间");
                }

                if (!file.VideoLength.HasValue)
                {
                    throw new Exception($"文件{file.Path}没有视频长度");
                }
            }

            //检查文件是否有重叠
            if (Config.CombineIntoSingleFile)
            {
                DateTime lastTime = DateTime.MinValue;
                foreach (var file in Files)
                {
                    if (file.StartTime.Value < lastTime)
                    {
                        throw new Exception($"文件{file.Path}开始时间小于上一个文件的结束时间");
                    }
                    lastTime =file.EndTime.Value;
                }

                await Task.Run(() =>
                {
                    Export(Files, Config.ExportFile);
                }, ct);
            }
            else
            {
                await Task.Run(() =>
                {
                    foreach (var file in Files)
                    {
                        Export(file, GetAssFileName(file.Path));
                    }
                }, ct);
            }
            
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files;
        }

        public override async Task InitializeAsync(CancellationToken token = default)
        {
            var files = FileNameHelper.GetFileNames(Config.Files)
                .Select(p => new TimeAssVideoFileInfo(p))
                .OrderBy(p=>p.Time)
                .ToList();
            await TryForFilesAsync(files,
                (f, s) =>
                {
                    f.VideoLength = GetVideoLength(f.Path);
                    if (f.VideoLength != null)
                    {
                        f.StartTime = f.Time - f.VideoLength;
                    }
                }, token,
                FilesLoopOptions.Builder().AutoApplyFileNumberProgress().Build());
            Files = files.ToList();
        }
    }
}